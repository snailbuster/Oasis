using System.Collections.Generic;
using UnityEngine;

namespace HeadingToOasis.Battle
{
    /// <summary>
    /// IMGUI 战斗 HUD。
    /// 顶部 - 双方信息条
    /// 中部 - 战斗舞台（小人 + 棋盘）
    /// 下部 - 我方卡片（左大）+ 敌方卡片（右小）
    /// 卡片显示规则：
    ///   * 默认：显示卡组堆叠（仅模板背景，不展示卡牌细节）
    ///   * 出牌瞬间：显示被抽出的单张卡牌，带"抽卡 + 使用"动画
    ///   * 动画结束：隐藏，回到堆叠
    /// </summary>
    public class BattleHUD : MonoBehaviour
    {
        public BattleManager manager;
        public int boardCells = 6;

        [Header("Card Frames")]
        public Texture2D attackCardBg;   // card_template_red
        public Texture2D thoughtCardBg;  // card_template_yellow

        GUIStyle _labelStyle, _smallStyle, _bigStyle, _titleStyle, _boxStyle, _btnStyle, _centerLabel, _smallCenter, _smallRich;
        Texture2D _hpBg, _hpFg, _stamFg, _attackRing, _thoughtRing, _whiteTex, _panelTex, _stageBg, _playerColor, _enemyColor;

        Vector2 _logScroll;
        bool _logCollapsed = true;

        // 角色攻击动画状态（小人冲刺/抖动）
        struct AttackAnim { public float startTime; public CardType cardType; }
        readonly Dictionary<Combatant, AttackAnim> _anims = new Dictionary<Combatant, AttackAnim>();

        // 卡牌"抽卡 + 使用"动画状态（每个 Deck 一条）
        struct CardAnim { public float startTime; public CardData card; }
        readonly Dictionary<RuntimeDeck, CardAnim> _cardAnims = new Dictionary<RuntimeDeck, CardAnim>();
        const float CARD_ANIM_DURATION = 1.4f;

        bool _hooked = false;

        void Awake()
        {
            if (manager == null) manager = FindObjectOfType<BattleManager>();
            EnsureTextures();
        }

        void EnsureTextures()
        {
            if (_hpBg == null) _hpBg = MakeTex(new Color(0.15f, 0.05f, 0.05f, 0.9f));
            if (_hpFg == null) _hpFg = MakeTex(new Color(0.85f, 0.2f, 0.2f, 1f));
            if (_stamFg == null) _stamFg = MakeTex(new Color(0.2f, 0.7f, 0.95f, 1f));
            if (_attackRing == null) _attackRing = MakeTex(new Color(0.95f, 0.3f, 0.3f, 1f));
            if (_thoughtRing == null) _thoughtRing = MakeTex(new Color(0.95f, 0.85f, 0.2f, 1f));
            if (_whiteTex == null) _whiteTex = MakeTex(Color.white);
            if (_panelTex == null) _panelTex = MakeTex(new Color(0, 0, 0, 0.6f));
            if (_stageBg == null) _stageBg = MakeTex(new Color(0.08f, 0.1f, 0.18f, 0.8f));
            if (_playerColor == null) _playerColor = MakeTex(new Color(0.35f, 0.7f, 1f, 1f));
            if (_enemyColor == null) _enemyColor = MakeTex(new Color(1f, 0.45f, 0.4f, 1f));
        }

        void OnEnable() { TryHook(); }
        void OnDisable() { TryUnhook(); }

        void TryHook()
        {
            if (_hooked) return;
            if (manager == null) manager = FindObjectOfType<BattleManager>();
            if (manager == null) return;
            manager.OnCardFired += HandleCardFired;
            _hooked = true;
        }
        void TryUnhook()
        {
            if (!_hooked || manager == null) return;
            manager.OnCardFired -= HandleCardFired;
            _hooked = false;
        }

        void HandleCardFired(Combatant who, CardData card)
        {
            if (who == null || card == null) return;
            _anims[who] = new AttackAnim { startTime = Time.time, cardType = card.cardType };

            // 找到对应卡组并记录动画。注意：BattleManager 在调用 OnCardFired 前已经执行了 DrawTop+Discard，
            // 所以传入的 card 就是被抽走那张牌（之前的顶卡）。
            RuntimeDeck deck = card.cardType == CardType.Attack ? who.attackDeck : who.thoughtDeck;
            if (deck != null)
                _cardAnims[deck] = new CardAnim { startTime = Time.time, card = card };
        }

        Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void EnsureStyles()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }, wordWrap = true };
            _bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _panelTex, textColor = Color.white } };
            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            _centerLabel = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter };
            _smallCenter = new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleCenter };
            _smallRich = new GUIStyle(_labelStyle) { richText = true };
        }

        void OnGUI()
        {
            EnsureTextures();
            EnsureStyles();
            if (!_hooked) TryHook();
            if (manager == null) return;
            var p = manager.player;
            var e = manager.enemy;
            if (p == null || e == null) return;

            float W = Screen.width;
            float H = Screen.height;

            // ========== 顶部双方信息条（左/右靠边）==========
            float topW = Mathf.Min(420f, W * 0.32f);
            float topH = 96f;
            float topY = 14f;
            float topMargin = 24f;
            DrawCombatantPanel(p, new Rect(topMargin, topY, topW, topH), new Color(0.35f, 0.7f, 1f));
            DrawCombatantPanel(e, new Rect(W - topMargin - topW, topY, topW, topH), new Color(1f, 0.45f, 0.4f));

            // 战斗时间居中
            string stateTxt = manager.state == BattleState.Running ? $"战斗中  {manager.battleTime:F1}s" : (manager.state == BattleState.Ended ? "战斗结束" : "");
            GUI.Label(new Rect(W * 0.5f - 100, topY + 8, 200, 22), stateTxt,
                new GUIStyle(_bigStyle) { alignment = TextAnchor.MiddleCenter });

            // ========== 中部战斗舞台（小人 + 棋盘）==========
            float stageW = Mathf.Min(820f, W - 60f);
            float stageH = 170f;
            float stageY = topY + topH + 14f;
            Rect stage = new Rect(W * 0.5f - stageW * 0.5f, stageY, stageW, stageH);
            DrawStage(p, e, stage);

            // ========== 下部：我方左大卡，敌方右小卡 ==========
            float cardsY = stageY + stageH + 28f;
            float playerCardW = 260f, playerCardH = 364f;
            float enemyCardW = 150f, enemyCardH = 210f;
            float cardGap = 14f;
            float speedH = 18f;

            // 我方（左）：进攻 + 思考
            float pX = topMargin;
            DrawDeckSlot(p.attackDeck, "我方·进攻", _attackRing,
                new Rect(pX, cardsY, playerCardW, playerCardH), speedH, true);
            DrawDeckSlot(p.thoughtDeck, "我方·思考", _thoughtRing,
                new Rect(pX + playerCardW + cardGap, cardsY, playerCardW, playerCardH), speedH, true);

            // 敌方（右）：思考 + 进攻（贴右）
            float eRight = W - topMargin;
            float eAttackX = eRight - enemyCardW;
            float eThoughtX = eAttackX - cardGap - enemyCardW;
            DrawDeckSlot(e.attackDeck, "敌方·进攻", _attackRing,
                new Rect(eAttackX, cardsY, enemyCardW, enemyCardH), speedH, false);
            DrawDeckSlot(e.thoughtDeck, "敌方·思考", _thoughtRing,
                new Rect(eThoughtX, cardsY, enemyCardW, enemyCardH), speedH, false);

            // ========== 中下：日志 ==========
            DrawLog(W, H);

            if (manager.state == BattleState.Ended)
            {
                if (GUI.Button(new Rect(W * 0.5f - 80, H - 60, 160, 36), "重新开始", _btnStyle)) manager.Restart();
            }
        }

        // ============================================================
        //                    顶部信息条
        // ============================================================
        void DrawCombatantPanel(Combatant c, Rect rect, Color tint)
        {
            GUI.Box(rect, GUIContent.none, _boxStyle);
            float pad = 10;
            float x = rect.x + pad, y = rect.y + pad, w = rect.width - pad * 2;

            GUI.color = tint;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3), _whiteTex);
            GUI.color = Color.white;

            GUI.Label(new Rect(x, y, w, 24), $"{c.displayName}  位置 {c.position}", _titleStyle);
            y += 26;

            GUI.DrawTexture(new Rect(x, y, w, 14), _hpBg);
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01((float)c.hp / c.maxHP), 14), _hpFg);
            GUI.Label(new Rect(x + 4, y - 4, w, 22), $"HP  {c.hp} / {c.maxHP}", _labelStyle);
            y += 18;

            GUI.DrawTexture(new Rect(x, y, w, 10), _hpBg);
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01((float)c.stamina / c.maxStamina), 10), _stamFg);
            GUI.Label(new Rect(x + 4, y - 6, w, 22), $"体力  {c.stamina} / {c.maxStamina}", _smallStyle);
            y += 14;

            string buffLine = "";
            if (c.buffs != null && c.buffs.Count > 0)
            {
                foreach (var b in c.buffs)
                {
                    string dur = b.duration < 0 ? "∞" : b.duration.ToString("F1") + "s";
                    buffLine += $"[{b.type} {b.value:F0}/{dur}] ";
                }
            }
            else buffLine = "无 Buff";
            GUI.Label(new Rect(x, y, w, rect.height - (y - rect.y) - 4), buffLine, _smallStyle);
        }

        // ============================================================
        //                    战斗舞台
        // ============================================================
        void DrawStage(Combatant p, Combatant e, Rect rect)
        {
            GUI.DrawTexture(rect, _stageBg);
            GUI.Box(rect, GUIContent.none, _boxStyle);

            int dist = Mathf.Abs(p.position - e.position);
            GUI.Label(new Rect(rect.x, rect.y + 6, rect.width, 20),
                $"距离 {dist}（{(dist <= 1 ? "近身" : "远距")}）", _centerLabel);

            float boardH = 44f;
            float boardY = rect.yMax - boardH - 12f;
            float pad = 24f;
            float cellW = (rect.width - pad * 2) / boardCells;
            for (int i = 1; i <= boardCells; i++)
            {
                Rect cell = new Rect(rect.x + pad + (i - 1) * cellW, boardY, cellW - 4, boardH);
                GUI.DrawTexture(cell, _hpBg);
                GUI.Label(new Rect(cell.x, cell.y + cell.height * 0.5f - 9, cell.width, 18), $"{i}", _centerLabel);
            }

            float charSize = 64f;
            float charY = boardY - charSize - 6f;

            DrawCharacter(p, rect, pad, cellW, charY, charSize, _playerColor, true);
            DrawCharacter(e, rect, pad, cellW, charY, charSize, _enemyColor, false);
        }

        void DrawCharacter(Combatant c, Rect stage, float pad, float cellW, float charY, float charSize, Texture2D bodyTex, bool isPlayer)
        {
            float cellCenterX = stage.x + pad + (c.position - 0.5f) * cellW;
            float offsetX = 0f;
            float scaleY = 1f;

            if (_anims.TryGetValue(c, out var a))
            {
                float t = Time.time - a.startTime;
                float dur = 0.5f;
                if (t < 0f) t = 0f;
                if (t < dur)
                {
                    float k = t < 0.15f ? (t / 0.15f) : Mathf.Max(0f, 1f - (t - 0.15f) / 0.35f);
                    int dir = c.Opponent != null ? (c.Opponent.position > c.position ? 1 : -1) : (isPlayer ? 1 : -1);
                    if (a.cardType == CardType.Attack)
                    {
                        offsetX = dir * k * 36f;
                    }
                    else
                    {
                        offsetX = Mathf.Sin(t * 40f) * 4f * k;
                        scaleY = 1f - 0.06f * k;
                    }
                }
                else
                {
                    _anims.Remove(c);
                }
            }

            float w = charSize;
            float h = charSize * scaleY;
            Rect body = new Rect(cellCenterX - w * 0.5f + offsetX, charY + (charSize - h), w, h);

            GUI.DrawTexture(body, bodyTex);

            float eyeY = body.y + h * 0.28f;
            float eyeSize = 7f;
            float eyeOffset = w * 0.18f;
            GUI.DrawTexture(new Rect(body.center.x - eyeOffset - eyeSize * 0.5f, eyeY, eyeSize, eyeSize), _hpBg);
            GUI.DrawTexture(new Rect(body.center.x + eyeOffset - eyeSize * 0.5f, eyeY, eyeSize, eyeSize), _hpBg);
            float mouthY = body.y + h * 0.62f;
            GUI.DrawTexture(new Rect(body.x + 14f, mouthY, w - 28f, 4f), _hpBg);

            if (c.Opponent != null)
            {
                int dir = c.Opponent.position > c.position ? 1 : -1;
                float armLen = 14f;
                float armY = body.y + h * 0.5f;
                Rect arm = dir > 0
                    ? new Rect(body.xMax, armY, armLen, 5f)
                    : new Rect(body.x - armLen, armY, armLen, 5f);
                GUI.DrawTexture(arm, _whiteTex);
            }

            GUI.Label(new Rect(body.x - 20f, body.y - 18f, w + 40f, 16f), c.displayName, _smallCenter);
        }

        // ============================================================
        //                    卡片槽位（堆叠 / 动画）
        // ============================================================
        void DrawDeckSlot(RuntimeDeck deck, string title, Texture2D timerFg, Rect cardRect, float speedH, bool large)
        {
            // 速度条
            Rect timerRect = new Rect(cardRect.x, cardRect.y - speedH - 4, cardRect.width, speedH);
            GUI.DrawTexture(timerRect, _hpBg);
            CardType deckType = deck != null ? deck.deckType : CardType.Attack;
            if (deck != null)
            {
                float prog = deck.Progress01();
                GUI.DrawTexture(new Rect(timerRect.x, timerRect.y, timerRect.width * prog, timerRect.height), timerFg);
                string txt = $"{title}   {Mathf.Max(0, deck.currentTimer):F1}s / {deck.currentSpeed:F1}s   ×{deck.drawPile.Count}";
                GUI.Label(new Rect(timerRect.x + 6, timerRect.y - 1, timerRect.width, timerRect.height + 2), txt, _smallStyle);
            }

            if (deck == null)
            {
                DrawCardBack(cardRect, deckType);
                return;
            }

            // 是否处于"抽出展示"动画
            bool animActive = false;
            CardAnim anim = default;
            if (_cardAnims.TryGetValue(deck, out anim))
            {
                float at = Time.time - anim.startTime;
                if (at >= 0f && at <= CARD_ANIM_DURATION) animActive = true;
                else _cardAnims.Remove(deck);
            }

            // 始终绘制堆叠（动画期间作为底层）
            int stackCount = Mathf.Clamp(deck.drawPile.Count - (animActive ? 1 : 0), 0, 5);
            DrawStack(cardRect, deckType, stackCount);

            if (animActive)
            {
                DrawDrawnCardAnim(cardRect, anim, large);
            }
        }

        // 画一摞背面卡（仅模板，无任何细节）
        void DrawStack(Rect frontRect, CardType type, int stackCount)
        {
            // 至少画 1 张作为"卡组占位"
            int n = Mathf.Max(1, stackCount);
            // 越靠后偏移越大、颜色越暗
            float maxOffset = 10f;
            float step = stackCount > 1 ? maxOffset / Mathf.Min(4, stackCount - 1) : 0f;
            for (int i = n - 1; i >= 0; i--)
            {
                float ox = -i * step;
                float oy = i * step;
                Rect r = new Rect(frontRect.x + ox, frontRect.y + oy, frontRect.width, frontRect.height);
                float dim = i == 0 ? 1f : Mathf.Lerp(1f, 0.55f, i / 4f);
                GUI.color = new Color(dim, dim, dim, 1f);
                DrawCardBack(r, type);
                GUI.color = Color.white;
            }
        }

        // 单张卡背：仅绘制对应模板（去掉所有数字/文字/立绘），以"未抽取"视觉呈现
        void DrawCardBack(Rect rect, CardType type)
        {
            Texture2D bg = type == CardType.Attack ? attackCardBg : thoughtCardBg;
            if (bg != null)
            {
                GUI.DrawTexture(rect, bg, ScaleMode.StretchToFill, true);
                // 半透明遮罩，提示是"卡背"未翻开
                GUI.color = new Color(0f, 0f, 0f, 0.32f);
                GUI.DrawTexture(rect, _whiteTex);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = type == CardType.Attack ? new Color(0.6f, 0.18f, 0.18f) : new Color(0.7f, 0.55f, 0.1f);
                GUI.DrawTexture(rect, _whiteTex);
                GUI.color = Color.white;
                GUI.Box(rect, GUIContent.none, _boxStyle);
            }
        }

        // 抽出展示 + 使用 + 隐藏 动画
        void DrawDrawnCardAnim(Rect baseRect, CardAnim anim, bool large)
        {
            float t = Time.time - anim.startTime;
            float d = CARD_ANIM_DURATION;            // 1.4
            // 0~0.25 : 缩放 0.85→1.10 + 上抬 12px
            // 0.25~1.00 : 缩放 1.10→1.00（轻微回弹）
            // 1.00~1.40 : 透明度 1→0
            float scale = 1f;
            float liftY = 0f;
            float alpha = 1f;
            if (t < 0.25f)
            {
                float k = t / 0.25f;
                scale = Mathf.Lerp(0.85f, 1.10f, k);
                liftY = Mathf.Lerp(0f, -12f, k);
            }
            else if (t < 1.00f)
            {
                float k = (t - 0.25f) / 0.75f;
                scale = Mathf.Lerp(1.10f, 1.00f, k);
                liftY = -12f + (12f * k * 0.4f); // 微下落
            }
            else
            {
                float k = (t - 1.00f) / (d - 1.00f);
                alpha = 1f - k;
                scale = Mathf.Lerp(1.00f, 0.95f, k);
                liftY = -12f * (1f - k);
            }

            // 围绕中心缩放
            float w = baseRect.width * scale;
            float h = baseRect.height * scale;
            Rect r = new Rect(baseRect.center.x - w * 0.5f, baseRect.center.y - h * 0.5f + liftY, w, h);

            Color prev = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            DrawCardFace(r, anim.card, attackCardBg, thoughtCardBg, large);
            GUI.color = prev;
        }

        // ============================================================
        //               单卡完整正面绘制（静态，可被预览界面复用）
        // ============================================================
        public static void DrawCardFace(Rect cardRect, CardData card, Texture2D attackBg, Texture2D thoughtBg, bool large)
        {
            if (card == null) return;
            CardType cardType = card.cardType;
            Texture2D bg = cardType == CardType.Attack ? attackBg : thoughtBg;
            if (bg != null)
            {
                GUI.DrawTexture(cardRect, bg, ScaleMode.StretchToFill, true);
            }
            else
            {
                Color tint = cardType == CardType.Attack ? new Color(0.6f, 0.18f, 0.18f) : new Color(0.7f, 0.55f, 0.1f);
                Color prev = GUI.color;
                GUI.color = tint;
                GUI.DrawTexture(cardRect, Texture2D.whiteTexture);
                GUI.color = prev;
            }

            float w = cardRect.width;
            float h = cardRect.height;

            // 模板的关键区域比例（基于 600x840 的 5:7 卡牌模板视觉对位）
            // 三个数字徽章圆心：分别落在左上(蓝)/左下(红)/右下(绿)三个圆形印刷区域内
            float badgeR = Mathf.Min(w, h) * 0.085f;
            Vector2 costCenter = new Vector2(cardRect.x + w * 0.07f, cardRect.y + h * 0.055f);
            Vector2 dmgCenter = new Vector2(cardRect.x + w * 0.07f, cardRect.y + h * 0.945f);
            Vector2 speedCenter = new Vector2(cardRect.x + w * 0.93f, cardRect.y + h * 0.945f);

            // 字体随卡牌高度缩放（基准 360px）；预览 840px 时会自然放大
            float fontScale = h / 360f;

            // 卡名（顶部居中横幅）—— 比之前下移一些，落在标题条内
            Rect nameRect = new Rect(cardRect.x + w * 0.22f, cardRect.y + h * 0.072f, w * 0.56f, h * 0.075f);
            int nameFontSize = Mathf.RoundToInt((large ? 22 : 13) * fontScale);
            if (nameFontSize < 9) nameFontSize = 9;
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = nameFontSize,
                normal = { textColor = new Color(0.15f, 0.07f, 0.05f) },
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            GUI.Label(nameRect, card.cardName, nameStyle);

            // 立绘 —— 拉伸覆盖中间立绘区域（不保持纵横比，按要求填满）
            Rect artRect = new Rect(cardRect.x + w * 0.105f, cardRect.y + h * 0.155f, w * 0.79f, h * 0.485f);
            if (card.cardArt != null && card.cardArt.texture != null)
            {
                var sp = card.cardArt;
                Rect tr = sp.textureRect;
                Texture tex = sp.texture;
                Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height,
                                   tr.width / tex.width, tr.height / tex.height);
                // 直接拉伸填满，最大化覆盖中间立绘区域
                GUI.DrawTextureWithTexCoords(artRect, tex, uv);
            }
            else
            {
                Color prev = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.18f);
                GUI.DrawTexture(artRect, Texture2D.whiteTexture);
                GUI.color = prev;
                var phStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = large ? 14 : 10,
                    normal = { textColor = new Color(0.4f, 0.3f, 0.25f, 0.9f) },
                    fontStyle = FontStyle.Italic
                };
                GUI.Label(artRect, "立绘", phStyle);
            }

            // 描述（立绘下方的黑色描述框内）—— 整体下移、白色字体
            Rect descRect = new Rect(cardRect.x + w * 0.13f, cardRect.y + h * 0.695f, w * 0.74f, h * 0.18f);
            int descFont = Mathf.Max(8, Mathf.RoundToInt((large ? 13 : 9) * fontScale));
            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = descFont,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip
            };
            GUI.Label(descRect, card.description, descStyle);

            // 角标数字
            int badgeFont = Mathf.Max(10, Mathf.RoundToInt((large ? 20 : 12) * fontScale));
            DrawBadgeTextStatic(costCenter, badgeR, card.staminaCost.ToString(), badgeFont, Color.white);
            if (card.baseDamage > 0)
                DrawBadgeTextStatic(dmgCenter, badgeR, card.baseDamage.ToString(), badgeFont, Color.white);
            DrawBadgeTextStatic(speedCenter, badgeR, card.speedValue.ToString("0.#"), badgeFont, Color.white);
        }

        static void DrawBadgeTextStatic(Vector2 center, float radius, string text, int fontSize, Color color)
        {
            Rect r = new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);
            var st = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = color }
            };
            var shadow = new GUIStyle(st);
            shadow.normal.textColor = new Color(0, 0, 0, 0.9f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, shadow);
            GUI.Label(new Rect(r.x - 1, r.y + 1, r.width, r.height), text, shadow);
            GUI.Label(r, text, st);
        }

        // ============================================================
        //                    战斗日志
        // ============================================================
        void DrawLog(float W, float H)
        {
            float btnW = 110f;
            float btnH = 28f;

            if (_logCollapsed)
            {
                Rect btn = new Rect(W * 0.5f - btnW * 0.5f, H - btnH - 14f, btnW, btnH);
                if (GUI.Button(btn, "展开战斗日志", _btnStyle)) _logCollapsed = false;
                return;
            }

            float boxW = Mathf.Min(560f, W - 120f);
            float boxH = 240f;
            Rect rect = new Rect(W * 0.5f - boxW * 0.5f, H - boxH - 14f, boxW, boxH);
            GUI.Box(rect, GUIContent.none, _boxStyle);
            GUI.Label(new Rect(rect.x + 10, rect.y + 6, 200, 22), "战斗日志", _bigStyle);
            if (GUI.Button(new Rect(rect.xMax - 80f - 8f, rect.y + 6f, 80f, 22f), "收起", _btnStyle))
                _logCollapsed = true;

            var inner = new Rect(rect.x + 8, rect.y + 32, rect.width - 16, rect.height - 40);
            var content = new Rect(0, 0, inner.width - 20, BattleLog.History.Count * 18 + 8);
            _logScroll = GUI.BeginScrollView(inner, _logScroll, content);
            float ly = 4;
            for (int i = 0; i < BattleLog.History.Count; i++)
            {
                GUI.Label(new Rect(4, ly, content.width - 8, 20), BattleLog.History[i], _smallStyle);
                ly += 18;
            }
            GUI.EndScrollView();
            if (Event.current.type == EventType.Repaint)
                _logScroll.y = Mathf.Max(0, content.height - inner.height);
        }
    }
}
