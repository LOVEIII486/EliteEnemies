using ItemStatsSystem.Stats;

namespace EliteEnemies.EliteEnemy.AttributeModifier
{
    /// <summary>
    /// 统一属性修改器 (Facade)
    /// 自动分发 Stat 和 AI 字段的修改请求
    /// </summary>
    public static class AttributeModifier
    {
        // ========== 快捷修改常用属性 ==========
        public static class Quick
        {
            /// <summary>
            /// 修改血量上限 (并可选治疗)
            /// </summary>
            public static void ModifyHealth(CharacterMainControl character, float multiplier, bool healToFull = false)
            {
                // 计算增量：1.5倍 -> 增加0.5
                float val = multiplier - 1f;
                StatModifier.AddModifier(character, StatModifier.Attributes.MaxHealth, val, ModifierType.PercentageMultiply);
                
                if (healToFull && character?.Health != null)
                {
                    character.Health.SetHealth(character.Health.MaxHealth);
                }
            }

            /// <summary>
            /// 修改伤害
            /// </summary>
            public static void ModifyDamage(CharacterMainControl character, float multiplier)
            {
                float val = multiplier - 1f;
                StatModifier.AddModifier(character, StatModifier.Attributes.GunDamageMultiplier, val, ModifierType.PercentageMultiply);
                StatModifier.AddModifier(character, StatModifier.Attributes.MeleeDamageMultiplier, val, ModifierType.PercentageMultiply);
            }

            /// <summary>
            /// 修改移动能力 (全面提升)
            /// </summary>
            public static void ModifySpeed(CharacterMainControl character, float multiplier)
            {
                float val = multiplier - 1f;
                // 同时修改行走、奔跑速度和加速度
                StatModifier.AddModifier(character, StatModifier.Attributes.WalkSpeed, val, ModifierType.PercentageMultiply);
                StatModifier.AddModifier(character, StatModifier.Attributes.RunSpeed, val, ModifierType.PercentageMultiply);
                StatModifier.AddModifier(character, StatModifier.Attributes.WalkAcc, val, ModifierType.PercentageMultiply);
                StatModifier.AddModifier(character, StatModifier.Attributes.RunAcc, val, ModifierType.PercentageMultiply);
            }

            /// <summary>
            /// 修改护甲 (分别修改头甲和身甲)
            /// </summary>
            public static void ModifyDefense(CharacterMainControl character, float multiplier)
            {
                float val = multiplier - 1f;
                StatModifier.AddModifier(character, StatModifier.Attributes.HeadArmor, val, ModifierType.PercentageMultiply);
                StatModifier.AddModifier(character, StatModifier.Attributes.BodyArmor, val, ModifierType.PercentageMultiply);
            }
        }

        // ========== 标准属性名称定义 ==========
        
        public static class StandardAttributes
        {
            // Stat 基础
            public const string MaxHealth = StatModifier.Attributes.MaxHealth;
            public const string MoveSpeed = StatModifier.Attributes.WalkSpeed;
            public const string Damage = StatModifier.Attributes.GunDamageMultiplier;
            
            // Stat 防御
            public const string HeadArmor = StatModifier.Attributes.HeadArmor;
            public const string BodyArmor = StatModifier.Attributes.BodyArmor;

            // Stat 感知 (原 AI 字段，现已修正为 Stat)
            public const string SightDistance = StatModifier.Attributes.ViewDistance;
            public const string SightAngle = StatModifier.Attributes.ViewAngle;
            public const string HearingAbility = StatModifier.Attributes.HearingAbility;
            
            // AI 行为 (仍然走 AIFieldModifier)
            public const string PatrolRange = AIFieldModifier.Fields.PatrolRange;
            public const string CombatMoveRange = AIFieldModifier.Fields.CombatMoveRange;
            public const string ForgetTime = AIFieldModifier.Fields.ForgetTime;
            public const string CanDash = AIFieldModifier.Fields.CanDash;
        }

        // ========== 核心分发逻辑 =========

        /// <summary>
        /// 修改属性通用入口
        /// </summary>
        /// <returns>如果是 Stat 修改，返回 Modifier 对象；如果是 AI 修改，返回 null</returns>
        public static object Modify(CharacterMainControl character, string attributeName, float value, bool isMultiplier)
        {
            // 1. 优先尝试 Stat 修改
            if (StatModifier.CanModify(attributeName))
            {
                var type = isMultiplier ? ModifierType.PercentageMultiply : ModifierType.Add;
                // 如果是倍率模式，假设传入的是最终倍率(如1.5)，转换为增量(0.5)
                float finalValue = isMultiplier ? (value - 1f) : value;
                
                return StatModifier.AddModifier(character, attributeName, finalValue, type);
            }

            // 2. 其次尝试 AI 字段修改
            if (AIFieldModifier.CanModify(attributeName))
            {
                AIFieldModifier.ModifyImmediate(character, attributeName, value, isMultiplier);
                return null;
            }

            return null;
        }
    }
}