using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeadingToOasis.Battle
{
    [Serializable]
    public class CardEffect
    {
        public EffectType type;
        public float value;       // 主参数
        public float secondary;   // 次参数
        public float duration;    // 持续时间(秒)
    }

    [CreateAssetMenu(menuName = "Oasis/Card Data", fileName = "NewCard")]
    public class CardData : ScriptableObject
    {
        public string cardName;
        [TextArea] public string description;
        public CardType cardType = CardType.Attack;
        public int staminaCost = 1;
        public float speedValue = 2f;
        public int baseDamage = 0;
        /// <summary>卡牌立绘（中部大区域）。可空，空时显示占位。</summary>
        public Sprite cardArt;
        public List<CardEffect> effects = new List<CardEffect>();
    }
}
