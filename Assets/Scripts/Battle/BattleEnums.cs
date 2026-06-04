namespace HeadingToOasis.Battle
{
    public enum CardType { Attack, Thought, Passive }

    public enum EffectType
    {
        // Attack effects
        CloseRangeDamageBonus,    // 近身时伤害+X
        CloseRangeSpeedDebuff,    // 近身时速度-X
        ApplyBleedOnClose,        // 近身时附加流血
        MoveCloserOnFar,          // 远距时靠近X
        ChargeArmor,              // 蓄力期护甲X
        ComboOnHit,               // 命中后获得连击+X

        // Thought effects
        MoveCloser,               // 靠近X
        BuffNextAttack,           // 下张进攻卡伤害+X
        SelfDamageBuffWithDebuff, // 自伤debuff组合
        EvadeWithStaminaReward    // 50%闪避，成功+体力
    }

    public enum BuffType
    {
        Bleed,
        EvadeNext,
        Armor,
        OutgoingDamageBuff,
        IncomingDamageBuff,
        NextAttackDamageBuff,
        ComboCharge
    }

    public enum BattleState { Idle, Running, Ended }
}
