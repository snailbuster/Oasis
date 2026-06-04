namespace HeadingToOasis.Battle
{
    /// <summary>
    /// 卡牌效果统一执行器。
    /// </summary>
    public static class EffectExecutor
    {
        public static void Execute(CardData card, Combatant self, Combatant opponent)
        {
            if (card == null || self == null) return;

            // 休息卡特例
            if (card.cardName == "休息")
            {
                self.RecoverStamina(10);
                BattleLog.Log($"{self.displayName} 休息（体力 +10）");
                return;
            }

            int damage = card.baseDamage;
            int comboToAdd = 0;
            bool noteBleed = false, noteMove = false, noteArmor = false;

            // 先解析所有效果，再统一结算
            if (card.effects != null)
            {
                foreach (var eff in card.effects)
                {
                    switch (eff.type)
                    {
                        case EffectType.CloseRangeDamageBonus:
                            if (self.IsClose) damage += (int)eff.value;
                            break;
                        case EffectType.CloseRangeSpeedDebuff:
                            // 此处简化：仅在 Log 中提示，速度调整可加在卡组层面（后续扩展）
                            break;
                        case EffectType.ApplyBleedOnClose:
                            if (self.IsClose && opponent != null)
                            {
                                opponent.AddBuff(new Buff { type = BuffType.Bleed, value = eff.value, duration = eff.duration });
                                noteBleed = true;
                            }
                            break;
                        case EffectType.MoveCloserOnFar:
                            if (!self.IsClose)
                            {
                                self.MoveToward(opponent, (int)eff.value);
                                noteMove = true;
                            }
                            break;
                        case EffectType.ChargeArmor:
                            self.AddBuff(new Buff { type = BuffType.Armor, value = eff.value, duration = 3f });
                            noteArmor = true;
                            break;
                        case EffectType.ComboOnHit:
                            comboToAdd = (int)eff.value;
                            break;
                        case EffectType.MoveCloser:
                            self.MoveToward(opponent, (int)eff.value);
                            break;
                        case EffectType.BuffNextAttack:
                            self.AddBuff(new Buff { type = BuffType.NextAttackDamageBuff, value = eff.value, duration = -1 });
                            break;
                        case EffectType.SelfDamageBuffWithDebuff:
                            self.AddBuff(new Buff { type = BuffType.OutgoingDamageBuff, value = eff.value, duration = eff.duration });
                            self.AddBuff(new Buff { type = BuffType.IncomingDamageBuff, value = eff.secondary, duration = eff.duration });
                            break;
                        case EffectType.EvadeWithStaminaReward:
                            self.AddBuff(new Buff { type = BuffType.EvadeNext, value = eff.value, secondary = eff.secondary, duration = -1 });
                            break;
                    }
                }
            }

            if (damage > 0 && opponent != null)
            {
                int bonus = self.GetOutgoingDamageBonus();
                int finalDamage = damage + bonus;
                BattleLog.Log($"{self.displayName} 使用 [{card.cardName}] → {finalDamage} 伤害" +
                              (noteBleed ? "（流血）" : "") +
                              (noteMove ? "（靠近）" : "") +
                              (noteArmor ? "（护甲）" : ""));
                opponent.TakeDamage(finalDamage);
                self.ConsumeNextAttackBonus();
                if (comboToAdd > 0 && opponent.IsAlive)
                {
                    self.AddBuff(new Buff { type = BuffType.NextAttackDamageBuff, value = comboToAdd, duration = -1 });
                    BattleLog.Log($"{self.displayName} 获得连击 +{comboToAdd}");
                }
            }
            else
            {
                BattleLog.Log($"{self.displayName} 使用 [{card.cardName}]" +
                              (noteMove ? "（靠近）" : "") +
                              (noteArmor ? "（护甲）" : ""));
            }
        }
    }
}
