using UnityEngine;

namespace EliteEnemies.Modifiers
{
    /// <summary>
    /// AI 字段改写器：**按来源施加、可按来源撤销**。
    ///
    /// <para>与 Stat 的关键差别：Stat 修改有游戏自带的 source 机制可以回滚
    /// （<c>Item.RemoveAllModifiersFrom</c>），AI 字段是**直接覆盖**，没有这层机制，
    /// 原值必须由我们自己留底——见 <see cref="AIFieldOverrideState"/>。
    /// 所以这里每个入口都**强制要求 source**：没有来源就没法还原。</para>
    ///
    /// <para><b>不用反射</b>：这些字段本来就是 public，用 <see cref="AIFields"/> 的
    /// 编译期访问器，字段改名会编译报错，而不是静默跳过。</para>
    /// </summary>
    public static class AIFieldOverrides
    {
        private const string LogTag = "[EliteEnemies.AIFieldOverrides]";

        /// <summary>
        /// 改写一个 float 字段（如反应时间、射击间隔、冲刺冷却）。
        /// </summary>
        /// <param name="character">目标角色</param>
        /// <param name="field">改哪个字段，取自 <see cref="AIFields"/></param>
        /// <param name="value">数值</param>
        /// <param name="multiply">true = 在原值上乘；false = 直接覆盖</param>
        /// <param name="source">来源标识（撤销时凭它批量还原，通常传词条名）</param>
        public static void Override(CharacterMainControl character,
            FieldRef<AICharacterController, float> field, float value, bool multiply, object source)
        {
            if (character == null) return;
            if (source == null)
            {
                Debug.LogError($"{LogTag} 未提供 source，无法登记还原，已跳过这次 AI 字段修改。");
                return;
            }

            var state = AIFieldOverrideState.For(character);
            state.Enqueue(source, ai => state.WriteFloat(source, field, ai, value, multiply));
        }

        /// <summary>
        /// 改写一个 bool 字段（如能否移动射击、能否冲刺）。
        /// 布尔量没有「乘」的语义，恒为覆盖。
        /// </summary>
        /// <param name="character">目标角色</param>
        /// <param name="field">改哪个字段，取自 <see cref="AIFields"/></param>
        /// <param name="value">目标值</param>
        /// <param name="source">来源标识（撤销时凭它批量还原，通常传词条名）</param>
        public static void Override(CharacterMainControl character,
            FieldRef<AICharacterController, bool> field, bool value, object source)
        {
            if (character == null) return;
            if (source == null)
            {
                Debug.LogError($"{LogTag} 未提供 source，无法登记还原，已跳过这次 AI 字段修改。");
                return;
            }

            var state = AIFieldOverrideState.For(character);
            state.Enqueue(source, ai => state.WriteBool(source, field, ai, value));
        }

        /// <summary>
        /// 还原某个来源改过的全部 AI 字段，并丢弃它尚未执行的排队写入。
        /// 与 <see cref="StatModifiers.RemoveAllModifiersFromSource"/> 配对使用
        /// （见 <see cref="CharacterModifiers.ClearAll"/>）。
        /// </summary>
        public static void Revert(CharacterMainControl character, object source)
        {
            if (character == null || source == null) return;

            var state = character.GetComponent<AIFieldOverrideState>();
            if (state != null) state.Revert(source);
        }

        /// <summary>
        /// 取角色身上的 AI 控制器。
        /// 先看 <c>aiCharacterController</c> 字段，退化时才往子层级找。
        /// </summary>
        internal static AICharacterController GetAI(CharacterMainControl character)
        {
            if (character == null) return null;
            if (character.aiCharacterController != null) return character.aiCharacterController;
            return character.GetComponentInChildren<AICharacterController>(true);
        }
    }
}
