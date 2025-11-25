namespace EliteEnemies.EliteEnemy.AttributeModifier
{
    /// <summary>
    /// 统一属性修改器
    /// 自动处理 Stat 和 AI 字段的修改
    /// </summary>
    public static class AttributeModifier
    {
        private const string LogTag = "[EliteEnemies.AttributeModifier]";
        
        /// <summary>
        /// 快捷修改常用属性
        /// </summary>
        public static class Quick
        {
            /// <summary>
            /// 修改血量
            /// </summary>
            public static void ModifyHealth(CharacterMainControl character, float multiplier, bool healToFull = false)
            {
                StatModifier.Multiply(character, StatModifier.Attributes.MaxHealth, multiplier);
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
                StatModifier.Multiply(character, StatModifier.Attributes.GunDamageMultiplier, multiplier);
                StatModifier.Multiply(character, StatModifier.Attributes.MeleeDamageMultiplier, multiplier);
            }

            /// <summary>
            /// 修改移动速度
            /// </summary>
            public static void ModifySpeed(CharacterMainControl character, float multiplier)
            {
                StatModifier.Multiply(character, StatModifier.Attributes.WalkSpeed, multiplier);
                StatModifier.Multiply(character, StatModifier.Attributes.RunSpeed, multiplier);
            }

            /// <summary>
            /// 修改射击精度（散布倍率越小越准确）
            /// </summary>
            public static void ModifyAccuracy(CharacterMainControl character, float multiplier)
            {
                StatModifier.Multiply(character, StatModifier.Attributes.GunScatterMultiplier, multiplier);
            }

            /// <summary>
            /// 修改反应速度（AI 字段，生成时延迟修改，战斗中立即修改）
            /// </summary>
            /// <param name="immediate">是否立即修改（战斗中使用 true，生成时使用 false）</param>
            public static void ModifyReactionSpeed(CharacterMainControl character, float multiplier, bool immediate = false)
            {
                if (immediate)
                {
                    AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ReactionTime, multiplier, multiply: true);
                    AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootDelay, multiplier, multiply: true);
                }
                else
                {
                    AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.ReactionTime, multiplier, multiply: true);
                    AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.ShootDelay, multiplier, multiply: true);
                }
            }

            /// <summary>
            /// 启用/禁用移动射击
            /// </summary>
            public static void SetMobileShooting(CharacterMainControl character, bool enable, bool immediate = false)
            {
                float value = enable ? 1f : 0f;
                if (immediate)
                    AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootCanMove, value);
                else
                    AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.ShootCanMove, value);
            }

            /// <summary>
            /// 启用/禁用冲刺
            /// </summary>
            public static void SetDash(CharacterMainControl character, bool enable, bool immediate = false)
            {
                float value = enable ? 1f : 0f;
                if (immediate)
                    AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.CanDash, value);
                else
                    AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.CanDash, value);
            }

            /// <summary>
            /// 修改视野
            /// </summary>
            public static void ModifyVision(CharacterMainControl character, float distanceMultiplier, float angleMultiplier)
            {
                StatModifier.Multiply(character, StatModifier.Attributes.ViewDistance, distanceMultiplier);
                StatModifier.Multiply(character, StatModifier.Attributes.ViewAngle, angleMultiplier);
            }
        }

        // ========== 批量修改接口 ==========

        /// <summary>
        /// 批量修改基础属性（精英初始化专用）
        /// </summary>
        public static void ApplyEliteMultipliers(
            CharacterMainControl character,
            float healthMult = 1f,
            float damageMult = 1f,
            float speedMult = 1f,
            bool healToFull = true)
        {
            StatModifier.ApplyMultipliers(
                character,
                healthMult: healthMult,
                damageMult: damageMult,
                speedMult: speedMult,
                healToFull: healToFull
            );
        }

        // ========== 属性常量 ==========

        /// <summary>
        /// 所有可修改的属性名称
        /// </summary>
        public static class Attributes
        {
            // === Stat 属性（立即生效）===
            
            // 生命
            public const string MaxHealth = StatModifier.Attributes.MaxHealth;
            public const string CurrentHealth = StatModifier.Attributes.CurrentHealth;
            
            // 伤害
            public const string GunDamageMultiplier = StatModifier.Attributes.GunDamageMultiplier;
            public const string MeleeDamageMultiplier = StatModifier.Attributes.MeleeDamageMultiplier;
            public const string GunCritRateGain = StatModifier.Attributes.GunCritRateGain;
            
            // 移动
            public const string WalkSpeed = StatModifier.Attributes.WalkSpeed;
            public const string RunSpeed = StatModifier.Attributes.RunSpeed;
            public const string SprintSpeed = StatModifier.Attributes.SprintSpeed;
            
            // 射击
            public const string GunScatterMultiplier = StatModifier.Attributes.GunScatterMultiplier;
            public const string BulletSpeedMultiplier = StatModifier.Attributes.BulletSpeedMultiplier;
            public const string GunDistanceMultiplier = StatModifier.Attributes.GunDistanceMultiplier;
            
            // 视野
            public const string ViewDistance = StatModifier.Attributes.ViewDistance;
            public const string ViewAngle = StatModifier.Attributes.ViewAngle;
            public const string NightVisionAbility = StatModifier.Attributes.NightVisionAbility;
            
            // 防御
            public const string ArmorValue = StatModifier.Attributes.ArmorValue;
            public const string DodgeChance = StatModifier.Attributes.DodgeChance;
            
            // === AI 字段（需要考虑时机）===
            
            // AI 行为
            public const string ReactionTime = AIFieldModifier.Fields.ReactionTime;
            public const string ShootDelay = AIFieldModifier.Fields.ShootDelay;
            public const string ShootCanMove = AIFieldModifier.Fields.ShootCanMove;
            public const string CanDash = AIFieldModifier.Fields.CanDash;
            
            // AI 感知
            public const string SightDistance = AIFieldModifier.Fields.SightDistance;
            public const string SightAngle = AIFieldModifier.Fields.SightAngle;
            public const string HearingAbility = AIFieldModifier.Fields.HearingAbility;
            
            // AI 战斗
            public const string PatrolRange = AIFieldModifier.Fields.PatrolRange;
            public const string CombatMoveRange = AIFieldModifier.Fields.CombatMoveRange;
            public const string ForgetTime = AIFieldModifier.Fields.ForgetTime;
        }

        // ========== 工具方法 ==========

        /// <summary>
        /// 检查属性是否存在
        /// </summary>
        public static bool IsValidAttribute(string attributeName)
        {
            return StatModifier.CanModify(attributeName) || AIFieldModifier.CanModify(attributeName);
        }

        /// <summary>
        /// 检查属性是否为 Stat 类型
        /// </summary>
        public static bool IsStatAttribute(string attributeName)
        {
            return StatModifier.CanModify(attributeName);
        }

        /// <summary>
        /// 检查属性是否为 AI 字段类型
        /// </summary>
        public static bool IsAIFieldAttribute(string attributeName)
        {
            return AIFieldModifier.CanModify(attributeName);
        }
    }
}
