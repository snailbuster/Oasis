using System.Collections.Generic;
using UnityEngine;

namespace HeadingToOasis.Battle
{
    public enum GameFlowState
    {
        MainMenu,
        DeckPreview,
        CardLibrary,
        Battle
    }

    /// <summary>
    /// 全局流程控制器：主菜单 → 卡组预览 → 战斗。
    /// 主菜单和卡组预览都用 IMGUI 绘制；战斗时把控制权交给 BattleHUD。
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        [Header("Refs")]
        public BattleManager battleManager;
        public BattleHUD battleHUD;

        [Header("State")]
        public GameFlowState state = GameFlowState.MainMenu;

        GUIStyle _titleStyle, _subTitleStyle, _btnStyle, _bigBtnStyle, _labelStyle, _smallStyle, _bigStyle, _boxStyle, _hintStyle, _factionStyle;
        Texture2D _bgTex, _panelTex, _whiteTex;
        Vector2 _previewScrollLeft, _previewScrollRight;
        Vector2 _libraryScroll;

        // 简易黑场转场
        bool _transitioning;
        float _transitionStartTime;
        GameFlowState _transitionTarget;
        bool _transitionMidApplied;
        const float TRANSITION_DURATION = 0.5f;

        void Awake()
        {
            if (battleManager == null) battleManager = FindObjectOfType<BattleManager>();
            if (battleHUD == null) battleHUD = FindObjectOfType<BattleHUD>();

            // 主菜单/预览阶段不渲染战斗 HUD
            if (battleHUD != null) battleHUD.enabled = false;

            _bgTex = MakeTex(new Color(0.06f, 0.07f, 0.12f, 1f));
            _panelTex = MakeTex(new Color(0, 0, 0, 0.55f));
            _whiteTex = MakeTex(Color.white);
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
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 56,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                _subTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.7f, 0.8f, 1f) }
                };
                _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
                _bigBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
                _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.8f, 0.85f, 0.9f) }, wordWrap = true };
                _bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                _boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _panelTex, textColor = Color.white } };
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.6f) }
                };
                _factionStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }
        }

        void OnGUI()
        {
            EnsureStyles();
            float W = Screen.width;
            float H = Screen.height;

            UpdateTransition();

            // 主菜单/预览阶段绘制全屏底色
            if (state != GameFlowState.Battle)
            {
                GUI.DrawTexture(new Rect(0, 0, W, H), _bgTex);
            }

            switch (state)
            {
                case GameFlowState.MainMenu:
                    DrawMainMenu(W, H);
                    break;
                case GameFlowState.DeckPreview:
                    DrawDeckPreview(W, H);
                    break;
                case GameFlowState.CardLibrary:
                    DrawCardLibrary(W, H);
                    break;
                case GameFlowState.Battle:
                    if (GUI.Button(new Rect(W - 110, 16, 90, 26), "返回菜单", _btnStyle))
                    {
                        BackToMenu();
                    }
                    break;
            }

            DrawTransitionOverlay(W, H);
        }

        // ============== 转场 ==============
        public void BeginTransitionTo(GameFlowState target)
        {
            _transitioning = true;
            _transitionStartTime = Time.realtimeSinceStartup;
            _transitionTarget = target;
            _transitionMidApplied = false;
        }

        void UpdateTransition()
        {
            if (!_transitioning) return;
            float t = Time.realtimeSinceStartup - _transitionStartTime;
            if (!_transitionMidApplied && t >= TRANSITION_DURATION * 0.5f)
            {
                _transitionMidApplied = true;
                switch (_transitionTarget)
                {
                    case GameFlowState.MainMenu: BackToMenu(); break;
                    case GameFlowState.DeckPreview: EnterDeckPreview(); break;
                    case GameFlowState.CardLibrary: EnterCardLibrary(); break;
                    case GameFlowState.Battle: EnterBattle(); break;
                }
            }
            if (t >= TRANSITION_DURATION) _transitioning = false;
        }

        void DrawTransitionOverlay(float W, float H)
        {
            if (!_transitioning) return;
            float t = Time.realtimeSinceStartup - _transitionStartTime;
            float halfDur = TRANSITION_DURATION * 0.5f;
            float a = t < halfDur ? (t / halfDur) : (1f - (t - halfDur) / halfDur);
            a = Mathf.Clamp01(a);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0, 0, W, H), _whiteTex);
            GUI.color = prev;
        }

        void Update()
        {
            // 转场期间持续重绘以保证黑场刷新
            // （IMGUI Repaint 通常每帧自动触发，这里仅留空以确保 MonoBehaviour 帧驱动）
        }

        // ============== 主菜单 ==============
        void DrawMainMenu(float W, float H)
        {
            // Title
            GUI.Label(new Rect(0, H * 0.18f, W, 90), "驶向绿洲", _titleStyle);
            GUI.Label(new Rect(0, H * 0.30f, W, 30), "Heading to the Oasis", _subTitleStyle);

            float bw = 280, bh = 60;
            float bx = W * 0.5f - bw * 0.5f;
            float by = H * 0.46f;

            if (GUI.Button(new Rect(bx, by, bw, bh), "战斗测试", _bigBtnStyle))
            {
                EnterDeckPreview();
            }

            if (GUI.Button(new Rect(bx, by + 76, bw, bh), "卡牌预览", _bigBtnStyle))
            {
                BeginTransitionTo(GameFlowState.CardLibrary);
            }

            GUI.color = new Color(1, 1, 1, 0.45f);
            GUI.Button(new Rect(bx, by + 152, bw, bh), "局外构筑（待开发）", _bigBtnStyle);
            GUI.Button(new Rect(bx, by + 228, bw, bh), "设置（待开发）", _bigBtnStyle);
            GUI.color = Color.white;

            if (GUI.Button(new Rect(bx, by + 304, bw, bh * 0.7f), "退出", _btnStyle))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        // ============== 卡组预览 ==============
        void DrawDeckPreview(float W, float H)
        {
            if (battleManager == null || battleManager.player == null || battleManager.enemy == null)
            {
                GUI.Label(new Rect(0, H * 0.5f, W, 40),
                    "BattleManager 未配置玩家或敌人", _titleStyle);
                if (GUI.Button(new Rect(W * 0.5f - 80, H - 60, 160, 36), "返回主菜单", _btnStyle))
                    BackToMenu();
                return;
            }

            GUI.Label(new Rect(0, 24, W, 50), "战斗准备", new GUIStyle(_titleStyle) { fontSize = 36 });
            GUI.Label(new Rect(0, 70, W, 24), "下方为双方卡组构成 - 点击下方按钮或点击空白区域开始战斗", _hintStyle);

            float topY = 110;
            float bottomBtnH = 90;
            float panelW = (W - 60) * 0.5f;
            float panelH = H - topY - bottomBtnH - 20;

            // 我方
            DrawDeckPanel(battleManager.player, "我方",
                new Color(0.3f, 0.7f, 1f),
                new Rect(20, topY, panelW, panelH),
                ref _previewScrollLeft, isPlayer: true);

            // 敌方
            DrawDeckPanel(battleManager.enemy, "敌方",
                new Color(1f, 0.4f, 0.3f),
                new Rect(40 + panelW, topY, panelW, panelH),
                ref _previewScrollRight, isPlayer: false);

            // 底部按钮
            float bw = 280, bh = 56;
            if (GUI.Button(new Rect(W * 0.5f - bw - 12, H - bottomBtnH + 12, bw, bh), "开始战斗 →", _bigBtnStyle))
            {
                EnterBattle();
            }
            if (GUI.Button(new Rect(W * 0.5f + 12, H - bottomBtnH + 12, bw, bh), "返回主菜单", _bigBtnStyle))
            {
                BackToMenu();
            }

            // 点击空白处也可跳过
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && Event.current.mousePosition.y < H - bottomBtnH - 6
                && Event.current.mousePosition.y > topY)
            {
                // 不在面板按钮区，且确实是空白点击：跳过
                EnterBattle();
                Event.current.Use();
            }
        }

        void DrawDeckPanel(Combatant c, string faction, Color tint, Rect rect, ref Vector2 scroll, bool isPlayer)
        {
            GUI.Box(rect, GUIContent.none, _boxStyle);
            // 顶部色条
            GUI.color = tint;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 4), _whiteTex);
            GUI.color = Color.white;

            float pad = 14;
            float x = rect.x + pad;
            float y = rect.y + 14;
            float w = rect.width - pad * 2;

            GUI.Label(new Rect(x, y, w, 30), $"{faction}  {c.displayName}", _factionStyle);
            y += 34;
            GUI.Label(new Rect(x, y, w, 20),
                $"HP {c.maxHP}    体力 {c.maxStamina}    起始位置 {(isPlayer ? 2 : 4)}",
                _labelStyle);
            y += 26;

            float scrollHeaderY = y;
            Rect scrollArea = new Rect(rect.x + 6, scrollHeaderY, rect.width - 12, rect.yMax - scrollHeaderY - 8);

            // 计算内容高度
            float contentH = ComputeDeckContentHeight(c);
            Rect contentRect = new Rect(0, 0, scrollArea.width - 18, contentH);
            scroll = GUI.BeginScrollView(scrollArea, scroll, contentRect);

            float cy = 4;
            cy = DrawSectionLabel("【进攻牌组】", cy, contentRect.width);
            cy = DrawCardListRows(c.attackCards, cy, contentRect.width, new Color(0.95f, 0.3f, 0.3f));

            cy += 6;
            cy = DrawSectionLabel("【思考牌组】", cy, contentRect.width);
            cy = DrawCardListRows(c.thoughtCards, cy, contentRect.width, new Color(0.95f, 0.85f, 0.2f));

            if (c.passiveCards != null && CountValid(c.passiveCards) > 0)
            {
                cy += 6;
                cy = DrawSectionLabel("【被动卡】", cy, contentRect.width);
                cy = DrawCardListRows(c.passiveCards, cy, contentRect.width, new Color(0.6f, 0.9f, 0.6f));
            }

            GUI.EndScrollView();
        }

        int CountValid(List<CardData> cards)
        {
            if (cards == null) return 0;
            int n = 0;
            foreach (var c in cards) if (c != null) n++;
            return n;
        }

        float ComputeDeckContentHeight(Combatant c)
        {
            float h = 0;
            h += 26; // attack header
            h += CountValid(c.attackCards) * 60;
            h += 6 + 26;
            h += CountValid(c.thoughtCards) * 60;
            if (c.passiveCards != null && CountValid(c.passiveCards) > 0)
            {
                h += 6 + 26;
                h += CountValid(c.passiveCards) * 60;
            }
            h += 12;
            return h;
        }

        float DrawSectionLabel(string text, float y, float w)
        {
            GUI.Label(new Rect(6, y, w - 12, 22), text, _bigStyle);
            return y + 24;
        }

        float DrawCardListRows(List<CardData> cards, float y, float w, Color tint)
        {
            if (cards == null) return y;
            foreach (var card in cards)
            {
                if (card == null) continue;
                Rect r = new Rect(6, y, w - 12, 56);
                GUI.Box(r, GUIContent.none, _boxStyle);
                // 左侧色条
                GUI.color = tint;
                GUI.DrawTexture(new Rect(r.x, r.y, 4, r.height), _whiteTex);
                GUI.color = Color.white;

                GUI.Label(new Rect(r.x + 12, r.y + 4, r.width - 16, 22),
                    $"{card.cardName}    <color=#5ad9ff>费{card.staminaCost}</color> · <color=#ffd966>速{card.speedValue:F1}s</color> · <color=#ff9090>伤{card.baseDamage}</color>",
                    new GUIStyle(_bigStyle) { richText = true, fontSize = 14 });
                GUI.Label(new Rect(r.x + 12, r.y + 28, r.width - 16, 24), card.description, _smallStyle);

                y += 60;
            }
            return y;
        }

        // ============== 卡牌预览（卡牌库）==============
        void DrawCardLibrary(float W, float H)
        {
            GUI.Label(new Rect(0, 24, W, 50), "卡牌预览", new GUIStyle(_titleStyle) { fontSize = 36 });
            GUI.Label(new Rect(0, 70, W, 24), "下方为本场全部卡牌（点击右下按钮返回主菜单）", _hintStyle);

            // 收集所有卡牌（去重）
            var cards = new List<CardData>();
            void AddRange(List<CardData> src)
            {
                if (src == null) return;
                foreach (var c in src) if (c != null && !cards.Contains(c)) cards.Add(c);
            }
            if (battleManager != null)
            {
                if (battleManager.player != null)
                {
                    AddRange(battleManager.player.attackCards);
                    AddRange(battleManager.player.thoughtCards);
                    AddRange(battleManager.player.passiveCards);
                }
                if (battleManager.enemy != null)
                {
                    AddRange(battleManager.enemy.attackCards);
                    AddRange(battleManager.enemy.thoughtCards);
                    AddRange(battleManager.enemy.passiveCards);
                }
            }

            // 排版：网格 —— 单张卡按 420x588（600x840 的 70%）渲染
            float topY = 110;
            float bottomBtnH = 90;
            float pad = 24f;
            float cardW = 420f;
            float cardH = 588f; // 5:7
            float gapX = 24f;
            float gapY = 32f;
            float areaW = W - pad * 2;
            int cols = Mathf.Max(1, Mathf.FloorToInt((areaW + gapX) / (cardW + gapX)));
            int rows = (cards.Count + cols - 1) / cols;
            float contentH = rows * (cardH + gapY) + 16f;

            Rect scrollArea = new Rect(pad, topY, areaW, H - topY - bottomBtnH - 20);
            Rect content = new Rect(0, 0, areaW - 18, contentH);
            _libraryScroll = GUI.BeginScrollView(scrollArea, _libraryScroll, content);

            Texture2D atkBg = battleHUD != null ? battleHUD.attackCardBg : null;
            Texture2D thtBg = battleHUD != null ? battleHUD.thoughtCardBg : null;

            float gridStartX = (content.width - (cols * cardW + (cols - 1) * gapX)) * 0.5f;
            for (int i = 0; i < cards.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float cx = gridStartX + col * (cardW + gapX);
                float cy = 8f + row * (cardH + gapY);
                Rect cardRect = new Rect(cx, cy, cardW, cardH);
                BattleHUD.DrawCardFace(cardRect, cards[i], atkBg, thtBg, true);
            }
            GUI.EndScrollView();

            // 底部按钮
            float bw = 280, bh = 56;
            if (GUI.Button(new Rect(W * 0.5f - bw * 0.5f, H - bottomBtnH + 12, bw, bh), "返回主菜单", _bigBtnStyle))
            {
                BeginTransitionTo(GameFlowState.MainMenu);
            }
        }

        // ============== 切换 ==============
        public void EnterDeckPreview()
        {
            state = GameFlowState.DeckPreview;
            if (battleHUD != null) battleHUD.enabled = false;
        }

        public void EnterCardLibrary()
        {
            state = GameFlowState.CardLibrary;
            if (battleHUD != null) battleHUD.enabled = false;
        }

        public void EnterBattle()
        {
            state = GameFlowState.Battle;
            if (battleHUD != null) battleHUD.enabled = true;
            if (battleManager != null) battleManager.StartBattle();
        }

        public void BackToMenu()
        {
            state = GameFlowState.MainMenu;
            if (battleHUD != null) battleHUD.enabled = false;
            if (battleManager != null) battleManager.state = BattleState.Idle;
        }
    }
}
