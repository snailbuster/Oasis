using System;

namespace HeadingToOasis.Battle
{
    [Serializable]
    public class Buff
    {
        public BuffType type;
        public float value;       // 强度
        public float secondary;   // 次参数
        public float duration;    // 剩余时间，<0 表示永久（本场战斗）
        public float tickAccum;   // DoT 累计
    }
}
