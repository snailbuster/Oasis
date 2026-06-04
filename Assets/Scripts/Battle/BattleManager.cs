using System;
using UnityEngine;

namespace HeadingToOasis.Battle
{
    /// <summary>
    /// 战斗主循环：tick 双方两个卡组的倒计时，触发出牌。
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        [Header("Combatants")]
        public Combatant player;
        public Combatant enemy;

        [Header("Battle")]
        public BattleState state = BattleState.Idle;
        public float battleTime;
        public bool autoStart = false;

        /// <summary>每当一张卡牌成功打出（已扣体力）就会触发，(出牌方, 卡牌)。</summary>
        public event Action<Combatant, CardData> OnCardFired;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (autoStart) StartBattle();
        }

        public void StartBattle()
        {
            if (player == null || enemy == null)
            {
                Debug.LogError("[BattleManager] 玩家或敌人未指定!");
                return;
            }
            BattleLog.Clear();
            BattleLog.Log("=== 战斗开始 ===");

            player.position = 2;
            enemy.position = 4;
            player.Opponent = enemy;
            enemy.Opponent = player;
            player.Init();
            enemy.Init();

            ApplyPassives(player);
            ApplyPassives(enemy);

            battleTime = 0;
            state = BattleState.Running;
        }

        void ApplyPassives(Combatant c)
        {
            foreach (var p in c.passiveCards)
            {
                if (p == null) continue;
                BattleLog.Log($"[被动] {c.displayName}: {p.cardName}");
                EffectExecutor.Execute(p, c, c.Opponent);
            }
        }

        void Update()
        {
            if (state != BattleState.Running) return;

            float dt = Time.deltaTime;
            battleTime += dt;

            player.TickBuffs(dt);
            enemy.TickBuffs(dt);

            TickDeck(player, player.attackDeck);
            TickDeck(player, player.thoughtDeck);
            TickDeck(enemy, enemy.attackDeck);
            TickDeck(enemy, enemy.thoughtDeck);

            if (!player.IsAlive || !enemy.IsAlive)
            {
                state = BattleState.Ended;
                string winner = player.IsAlive ? player.displayName : enemy.displayName;
                BattleLog.Log($"=== 战斗结束 - {winner} 胜利 ===");
            }
        }

        void TickDeck(Combatant owner, RuntimeDeck deck)
        {
            if (deck == null) return;
            deck.Tick(Time.deltaTime);
            if (!deck.ReadyToFire) return;

            var card = deck.TopCard;
            if (card == null)
            {
                deck.ResetTimer();
                return;
            }

            if (owner.TrySpendStamina(card.staminaCost))
            {
                EffectExecutor.Execute(card, owner, owner.Opponent);
                var played = deck.DrawTop();
                deck.Discard(played);
                OnCardFired?.Invoke(owner, card);
            }
            else
            {
                BattleLog.Log($"{owner.displayName} 体力不足，跳过 [{card.cardName}]，自动休息");
                owner.RecoverStamina(10);
                deck.ResetTimer();
            }
        }

        public void Restart()
        {
            StartBattle();
        }
    }
}
