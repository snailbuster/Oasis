using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeadingToOasis.Battle
{
    /// <summary>
    /// 战斗参与者：玩家或敌人。包含资源、位置、卡组、Buff。
    /// </summary>
    public class Combatant : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Player";
        public bool isPlayer = true;

        [Header("Resources")]
        public int maxHP = 100;
        public int hp = 100;
        public int maxStamina = 50;
        public int stamina = 30;

        [Header("Position (1-6)")]
        [Range(1, 6)] public int position = 2;

        [Header("Decks (assign CardData assets)")]
        public List<CardData> attackCards = new List<CardData>();
        public List<CardData> thoughtCards = new List<CardData>();
        public List<CardData> passiveCards = new List<CardData>();

        [HideInInspector] public RuntimeDeck attackDeck;
        [HideInInspector] public RuntimeDeck thoughtDeck;
        [HideInInspector] public List<Buff> buffs = new List<Buff>();

        public Combatant Opponent { get; set; }
        public event Action OnStateChanged;

        public void Init()
        {
            hp = maxHP;
            buffs.Clear();
            attackDeck = new RuntimeDeck(attackCards, CardType.Attack);
            thoughtDeck = new RuntimeDeck(thoughtCards, CardType.Thought);
            OnStateChanged?.Invoke();
        }

        public int Distance => Opponent ? Mathf.Abs(position - Opponent.position) : 0;
        public bool IsClose => Distance <= 1;
        public bool IsAlive => hp > 0;

        public void TakeDamage(int amount)
        {
            // 闪避 buff 优先
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].type == BuffType.EvadeNext)
                {
                    bool dodge = UnityEngine.Random.value < buffs[i].value;
                    if (dodge)
                    {
                        BattleLog.Log($"{displayName} 闪避了攻击！");
                        RecoverStamina((int)buffs[i].secondary);
                        buffs.RemoveAt(i);
                        OnStateChanged?.Invoke();
                        return;
                    }
                    buffs.RemoveAt(i);
                }
            }

            int finalDamage = amount;
            // 受到伤害+X debuff
            foreach (var b in buffs)
            {
                if (b.type == BuffType.IncomingDamageBuff) finalDamage += (int)b.value;
            }

            // 护甲吸收
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].type == BuffType.Armor && finalDamage > 0)
                {
                    int absorb = Mathf.Min((int)buffs[i].value, finalDamage);
                    finalDamage -= absorb;
                    buffs[i].value -= absorb;
                    if (buffs[i].value <= 0) buffs.RemoveAt(i);
                }
            }

            hp = Mathf.Max(0, hp - finalDamage);
            BattleLog.Log($"{displayName} 受到 {finalDamage} 伤害，剩余HP {hp}");
            OnStateChanged?.Invoke();
        }

        public void Heal(int amount)
        {
            hp = Mathf.Min(maxHP, hp + amount);
            OnStateChanged?.Invoke();
        }

        public bool TrySpendStamina(int amount)
        {
            if (stamina < amount) return false;
            stamina -= amount;
            OnStateChanged?.Invoke();
            return true;
        }

        public void RecoverStamina(int amount)
        {
            stamina = Mathf.Min(maxStamina, stamina + amount);
            OnStateChanged?.Invoke();
        }

        public void AddBuff(Buff buff)
        {
            buffs.Add(buff);
            OnStateChanged?.Invoke();
        }

        public int GetOutgoingDamageBonus()
        {
            int bonus = 0;
            foreach (var b in buffs)
            {
                if (b.type == BuffType.OutgoingDamageBuff) bonus += (int)b.value;
                if (b.type == BuffType.NextAttackDamageBuff) bonus += (int)b.value;
            }
            return bonus;
        }

        public void ConsumeNextAttackBonus()
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].type == BuffType.NextAttackDamageBuff)
                    buffs.RemoveAt(i);
            }
        }

        public void TickBuffs(float dt)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                var b = buffs[i];
                // Bleed 持续伤害（每 1 秒结算一次）
                if (b.type == BuffType.Bleed)
                {
                    b.tickAccum += dt;
                    while (b.tickAccum >= 1f && b.duration > 0)
                    {
                        b.tickAccum -= 1f;
                        hp = Mathf.Max(0, hp - (int)b.value);
                        BattleLog.Log($"{displayName} 流血损失 {(int)b.value} HP");
                        OnStateChanged?.Invoke();
                    }
                }
                // 倒计时（duration < 0 表示永久）
                if (b.duration > 0)
                {
                    b.duration -= dt;
                    if (b.duration <= 0)
                    {
                        buffs.RemoveAt(i);
                        OnStateChanged?.Invoke();
                    }
                }
            }
        }

        public void MoveToward(Combatant target, int steps)
        {
            if (target == null || steps <= 0) return;
            int dir = target.position > position ? 1 : (target.position < position ? -1 : 0);
            if (dir == 0) return;
            int newPos = Mathf.Clamp(position + dir * steps, 1, 6);

            // 双方不能同格、不能越过对手：双方各自停在距对手 1 格处
            if (dir > 0) newPos = Mathf.Min(newPos, target.position - 1);
            else        newPos = Mathf.Max(newPos, target.position + 1);

            if (newPos == position) return;
            position = newPos;
            OnStateChanged?.Invoke();
        }
    }
}
