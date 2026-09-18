using ItemStatsSystem.Stats;

namespace EliteEnemies.Modifiers
{
    /// <summary>
    /// 统一属性修改器（门面）。
    ///
    /// <para>两条路各归各的，<b>不再按字符串猜</b>：
    /// Stat 走 <see cref="StatModifiers"/>（有游戏自带的 source 回滚机制），
    /// AI 字段走 <see cref="AIFieldOverrides"/>（原值由我们自己留底）。
    /// 历史上这里是「按字段名查表 + 名字里有没有点」来决定走哪条路，
    /// 那种写法把「改哪个字段」变成运行时字符串，写错不报错。</para>
    /// </summary>
    public static class CharacterModifiers
    {
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
                StatModifiers.AddModifier(enemy, StatKeys.MaxHealth, val, ModifierType.PercentageMultiply, source);
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
                StatModifiers.AddModifier(enemy, StatKeys.GunDamageMultiplier, val, ModifierType.PercentageMultiply, source);
                StatModifiers.AddModifier(enemy, StatKeys.MeleeDamageMultiplier, val, ModifierType.PercentageMultiply, source);
            }

            /// <summary>
            /// 修改移动速度。
            ///
            /// <para>⚠ 移速**没有**单一的 stat key：游戏里分 <c>WalkSpeed</c> 与 <c>RunSpeed</c>，
            /// 必须两个都写。历史上有一个 <c>"MoveSpeed"</c> 常量，注释声称会自动分发到这两个，
            /// 但那套逻辑并不存在，于是修改静默失效——要改移速只能走这个方法。</para>
            /// </summary>
            public static void ModifySpeed(CharacterMainControl enemy, float multiplier, object source)
            {
                if (enemy == null) return;
                float val = multiplier - 1f;
                StatModifiers.AddModifier(enemy, StatKeys.WalkSpeed, val, ModifierType.PercentageMultiply, source);
                StatModifiers.AddModifier(enemy, StatKeys.RunSpeed, val, ModifierType.PercentageMultiply, source);
            }
        }

        /// <summary>
        /// 修改一个 **Stat** 属性。AI 字段请改用 <see cref="AIFieldOverrides.Override(CharacterMainControl, FieldRef{AICharacterController, float}, float, bool, object)"/>。
        /// </summary>
        /// <param name="character">目标角色</param>
        /// <param name="attributeName">属性名，取自 <see cref="StatKeys"/></param>
        /// <param name="value">目标数值</param>
        /// <param name="isMultiplier">是否为倍率模式</param>
        /// <param name="source">来源标识（清理时凭它批量撤销，通常传词条名）</param>
        /// <returns>创建的 Modifier；<b>为 null 表示这次修改没有生效</b>（key 不存在，或参数不合法）</returns>
        public static Modifier Modify(CharacterMainControl character, string attributeName, float value, bool isMultiplier, object source)
        {
            if (character == null) return null;

            ModifierType type = isMultiplier ? ModifierType.PercentageMultiply : ModifierType.Add;

            float finalValue = isMultiplier ? (value - 1f) : value;

            return StatModifiers.AddModifier(character, attributeName, finalValue, type, source);
        }

        /// <summary>
        /// 统一清理：把某个来源造成的 **Stat 与 AI 字段改动一起**撤销。
        ///
        /// <para>历史上这里只清 Stat，AI 字段的改动会永久残留在角色身上
        /// （被回收复用后仍带着加成的根因）。</para>
        /// </summary>
        public static void ClearAll(CharacterMainControl enemy, object source)
        {
            if (enemy == null || source == null) return;
            StatModifiers.RemoveAllModifiersFromSource(enemy, source);
            AIFieldOverrides.Revert(enemy, source);
        }
    }
}
