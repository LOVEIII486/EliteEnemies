using ItemStatsSystem.Stats;
using System.Collections.Generic;

namespace EliteEnemies.EliteEnemy.AttributeModifier
{
    /// <summary>
    /// 统一属性修改器
    /// </summary>
    public static class AttributeModifier
    {
        private static readonly HashSet<string> AI_FIELDS = new HashSet<string>
        {
            AIFieldModifier.Fields.ReactionTime,
            AIFieldModifier.Fields.BaseReactionTime,
            AIFieldModifier.Fields.UpdateValueTimer,
            AIFieldModifier.Fields.PatrolTurnSpeed,
            AIFieldModifier.Fields.CombatTurnSpeed,
            AIFieldModifier.Fields.ShootDelay,
            AIFieldModifier.Fields.ShootCanMove,
            AIFieldModifier.Fields.SightDistance,
            AIFieldModifier.Fields.SightAngle,
            AIFieldModifier.Fields.HearingAbility,
            AIFieldModifier.Fields.CanDash,
            AIFieldModifier.Fields.PatrolRange,
            AIFieldModifier.Fields.CombatMoveRange,
            AIFieldModifier.Fields.ForgetTime,
            AIFieldModifier.Fields.HasSkill,
            AIFieldModifier.Fields.SkillChance,
            AIFieldModifier.Fields.DefaultWeaponOut
        };

        public static class Quick
        {
            /// <summary>
            /// 快捷增强精英怪基础属性
            /// </summary>
            /// <param name="healthMul">血量倍率 (如 2.0)</param>
            /// <param name="damageMul">伤害倍率 (如 1.5)</param>
            /// <param name="speedMul">移速倍率 (如 1.2)</param>
            /// <param name="source">词缀名称 (用于清理)</param>
            public static void ApplyElitePowerup(CharacterMainControl enemy, float healthMul, float damageMul, float speedMul, object source)
            {
                if (enemy == null) return;

                if (healthMul > 1.001f) ModifyHealth(enemy, healthMul, source);
                if (damageMul > 1.001f) ModifyDamage(enemy, damageMul, source);
                if (speedMul > 1.001f) ModifySpeed(enemy, speedMul, source);
            }

            /// <summary>
            /// 修改血量上限并补满
            /// </summary>
            public static void ModifyHealth(CharacterMainControl enemy, float multiplier, object source, bool healToFull = true)
            {
                float val = multiplier - 1f;
                StatModifier.AddModifier(enemy, StatModifier.Attributes.MaxHealth, val, ModifierType.PercentageMultiply, source);
                if (healToFull && enemy?.Health != null)
                {
                    enemy.Health.SetHealth(enemy.Health.MaxHealth);
                }
            }

            /// <summary>
            /// 修改远程与近战伤害
            /// </summary>
            public static void ModifyDamage(CharacterMainControl enemy, float multiplier, object source)
            {
                float val = multiplier - 1f;
                StatModifier.AddModifier(enemy, StatModifier.Attributes.GunDamageMultiplier, val, ModifierType.PercentageMultiply, source);
                StatModifier.AddModifier(enemy, StatModifier.Attributes.MeleeDamageMultiplier, val, ModifierType.PercentageMultiply, source);
            }

            /// <summary>
            /// 修改移动速度
            /// </summary>
            public static void ModifySpeed(CharacterMainControl enemy, float multiplier, object source)
            {
                if (enemy == null) return;
                float val = multiplier - 1f;
                StatModifier.AddModifier(enemy, StatModifier.Attributes.WalkSpeed, val, ModifierType.PercentageMultiply, source);
                StatModifier.AddModifier(enemy, StatModifier.Attributes.RunSpeed, val, ModifierType.PercentageMultiply, source);
            }

            /// <summary>
            /// 强化 AI 反应与行为
            /// </summary>
            public static void EnhanceAI(CharacterMainControl enemy, bool canMoveShoot, float reactionMultiplier)
            {
                if (canMoveShoot)
                    AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.ShootCanMove, 1.0f);
                
                if (reactionMultiplier < 0.99f)
                    AIFieldModifier.ModifyDelayed(enemy, AIFieldModifier.Fields.ReactionTime, reactionMultiplier, true);
            }
        }
        
        /// <summary>
        /// 通用属性修改入口：自动判断是走 Stat 系统还是反射系统
        /// </summary>
        /// <param name="character">目标角色</param>
        /// <param name="attributeName">属性名</param>
        /// <param name="value">目标数值</param>
        /// <param name="isMultiplier">是否为倍率模式</param>
        /// <param name="source">来源标识</param>
        /// <returns>如果是 Stat 修改，返回 Modifier 对象；如果是 AI 逻辑修改，返回 null</returns>
        public static object Modify(CharacterMainControl character, string attributeName, float value, bool isMultiplier, object source = null)
        {
            if (character == null) return null;

            // 检查是否属于 AI 字段
            if (AI_FIELDS.Contains(attributeName) || attributeName.Contains("."))
            {
                AIFieldModifier.ModifyDelayed(character, attributeName, value, isMultiplier);
                return null;
            }

            // 作为 Stat 属性处理
            ModifierType type = isMultiplier ? ModifierType.PercentageMultiply : ModifierType.Add;
            
            float finalValue = isMultiplier ? (value - 1f) : value;

            return StatModifier.AddModifier(character, attributeName, finalValue, type, source);
        }

        /// <summary>
        /// 统一清理方法
        /// </summary>
        public static void ClearAll(CharacterMainControl enemy, object source)
        {
            if (enemy == null || source == null) return;
            StatModifier.RemoveAllModifiersFromSource(enemy, source);
        }
    }
}