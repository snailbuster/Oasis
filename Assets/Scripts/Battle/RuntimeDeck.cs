using System.Collections.Generic;
using UnityEngine;

namespace HeadingToOasis.Battle
{
    /// <summary>
    /// 单个卡组（进攻 or 思考）的运行时状态：抽牌堆、墓地、倒计时。
    /// </summary>
    public class RuntimeDeck
    {
        public List<CardData> drawPile = new List<CardData>();
        public List<CardData> graveyard = new List<CardData>();
        public CardType deckType;

        public float currentTimer;   // 剩余倒计时
        public float currentSpeed;   // 当前顶卡的速度（用于UI环计算进度）

        public CardData TopCard => drawPile.Count > 0 ? drawPile[0] : null;
        public bool ReadyToFire => currentTimer <= 0f;

        private static CardData _restCardCache;
        public static CardData GetRestCard(CardType type)
        {
            if (_restCardCache == null)
            {
                _restCardCache = ScriptableObject.CreateInstance<CardData>();
                _restCardCache.cardName = "休息";
                _restCardCache.description = "体力 +10";
                _restCardCache.staminaCost = 0;
                _restCardCache.speedValue = 2f;
                _restCardCache.baseDamage = 0;
            }
            // 共用同一个实例即可
            return _restCardCache;
        }

        public RuntimeDeck(List<CardData> source, CardType type)
        {
            deckType = type;
            for (int i = 0; i < 5; i++)
            {
                if (i < source.Count && source[i] != null)
                    drawPile.Add(source[i]);
                else
                    drawPile.Add(GetRestCard(type));
            }
            ResetTimer();
        }

        public void ResetTimer()
        {
            currentSpeed = TopCard != null ? Mathf.Max(0.1f, TopCard.speedValue) : 2f;
            currentTimer = currentSpeed;
        }

        public void Tick(float dt)
        {
            currentTimer -= dt;
        }

        public CardData DrawTop()
        {
            if (drawPile.Count == 0) Reshuffle();
            if (drawPile.Count == 0) return null;
            var top = drawPile[0];
            drawPile.RemoveAt(0);
            return top;
        }

        public void Discard(CardData card)
        {
            if (card != null) graveyard.Add(card);
            if (drawPile.Count == 0) Reshuffle();
            ResetTimer();
        }

        public float Progress01()
        {
            if (currentSpeed <= 0f) return 1f;
            return Mathf.Clamp01(1f - currentTimer / currentSpeed);
        }

        void Reshuffle()
        {
            for (int i = graveyard.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (graveyard[i], graveyard[j]) = (graveyard[j], graveyard[i]);
            }
            drawPile.AddRange(graveyard);
            graveyard.Clear();
        }
    }
}
