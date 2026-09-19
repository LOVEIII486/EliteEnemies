using System.Collections;
using EliteEnemies.Modifiers;
using UnityEngine;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 词条行为基类 —— 纯工具类，**不含隐性自动化逻辑**（一切都是显式调用才发生）。
    ///
    /// <para>所有回调默认留空，子类按需覆盖。下面的受保护助手方法把
    /// <see cref="AffixName"/> 自动带上，省去每条词条重复传参。</para>
    ///
    /// <para><b>框架给的两样东西，优先用它们</b>：<see cref="Ctx"/>（常用引用，每个敌人只解析一次）
    /// 与 <see cref="StartManagedCoroutine"/>（协程交给框架托管）。见各自的注释。</para>
    ///
    /// <para>⚠ 用了 <see cref="ClearBaseModifiers"/> 之外的那几个助手
    /// （<c>Modify</c> / <c>ModifyAI</c> / <c>ApplyPowerup</c>）之后，**应当**在
    /// <c>OnCleanup</c> 里手动调 <see cref="ClearBaseModifiers"/>——想更早撤销（例如死亡时就撤）
    /// 则放在 <c>OnEliteDeath</c>。</para>
    ///
    /// <para><b>忘了调不再是永久残留</b>：框架在 <c>EliteBehaviorComponent.OnDestroy</c> 里
    /// 会对每个行为再补一次 <c>ClearAll(_character, AffixName)</c>（依据是所有写入都以
    /// <c>AffixName</c> 作来源标识，见 <c>docs\词条模块审查与设计.md</c> §3.1）。
    /// 这条只是**保险丝**——它只在组件销毁时生效，替代不了"该撤的时候自己撤"。</para>
    ///
    /// <para>⚠ <b>清理必须幂等</b>：<c>OnCleanup</c> 可能被走到两次——若
    /// <c>OnEliteDeath</c> 里顺手调了它（本工程有 3 个行为这么做），组件销毁时还会再调一次。
    /// 幂等写法：撤销操作本身可重复调用、退订前判空、不重复创建/销毁。</para>
    /// </summary>
    public abstract class AffixBehaviorBase : IAffixBehavior
    {
        public abstract string AffixName { get; }

        // --- 框架提供的东西：用它们，别自己找 ---

        /// <summary>
        /// 这个精英身上常用引用的解析结果（<see cref="AffixContext"/>），
        /// **由框架在每个敌人身上只解析一次**。在 <c>OnEliteInitialized</c> 及之后可用。
        ///
        /// <para>需要角色、模型、AI、移动、Buff 管理器这些引用时，先看这里有没有——
        /// 不要在自己类里再 <c>GetComponent</c> 一遍（那正是这次要收掉的写法），
        /// 更不要反向去抓 <c>EliteBehaviorComponent</c>。</para>
        /// </summary>
        protected AffixContext Ctx { get; private set; }

        /// <summary>由框架调用：绑定上下文。行为不需要自己调用。</summary>
        internal void BindContext(AffixContext context)
        {
            Ctx = context;
        }

        /// <summary>
        /// 起一个**由框架托管**的协程：宿主是精英的行为组件，组件销毁时统一停。
        ///
        /// <para>用它，而不是自己挑宿主（<c>ModBehaviour.Instance</c> / <c>attacker</c> /
        /// 敌人自身）——「协程被中止、而它改过的状态没被还原」是本工程栽过的一类问题
        /// （PhaseSwap 的静态标志卡死，见 <c>docs\词条模块审查与设计.md</c> §3.2（4））。</para>
        ///
        /// <para>⚠ 托管**不等于**自动还原状态：协程里改过的东西（全局时间缩放、静态标志、
        /// 角色属性…）仍要在 <c>OnCleanup</c> 里自己撤销。它保证的只是"生命周期有个统一出口"。</para>
        /// </summary>
        protected Coroutine StartManagedCoroutine(IEnumerator routine)
        {
            return Ctx != null && Ctx.Host != null ? Ctx.Host.StartManagedCoroutine(routine) : null;
        }

        /// <summary>停掉一个托管协程（<see cref="StartManagedCoroutine"/> 的返回值）。</summary>
        protected void StopManagedCoroutine(Coroutine handle)
        {
            if (Ctx != null && Ctx.Host != null) Ctx.Host.StopManagedCoroutine(handle);
        }

        /// <summary>
        /// 让**某个角色**停止/恢复"自己移动"——用于本行为要**每帧直接摆放它**的场合
        /// （例如绕主人旋转的伴生体）。不这么做的话，它的移动系统会和我们的摆放互相打架。
        ///
        /// <para><b>为什么不再用 <c>GetComponent&lt;NavMeshAgent&gt;().enabled = false</c></b>：
        /// 游戏**根本不用** Unity 的 NavMeshAgent——AI 走 A*（<c>AstarPath</c> / <c>Seeker</c> /
        /// <c>ABPath</c>，见 <c>AICharacterController.cs:697-710</c>），所以那处查找要么拿到 null、
        /// 要么作用在一个游戏自己都不用的组件上。**这就是"这几处一直不起效"的原因。**</para>
        ///
        /// <para>官方两条口子：① <c>AICharacterController.StopMove()</c>（<c>:744</c>，AI 层标准停走，
        /// NodeCanvas 的 <c>StopMoving</c> 任务用的就是它）；② 关掉角色自己的移动系统
        /// <c>movementControl.enabled</c>。</para>
        ///
        /// <para>⚠ <b>不能用 <c>movement.enabled = false</c></b>：游戏驱动移动的是
        /// <c>CharacterMainControl.Update()</c>（<c>:1708</c>）里**无条件**调用的
        /// <c>movementControl.UpdateMovement()</c>，而那个方法内部只被 <c>if (MovementEnabled)</c> 挡住
        /// （<c>Movement.cs:200-202</c>）——<c>Movement</c> 自己的 <c>Update()</c>/<c>FixedUpdate()</c>
        /// 都是**空方法**（<c>:239-241</c>、<c>:372-374</c>）。所以关组件是个彻底的空操作。
        /// （本工程在这件事上栽过两次：先是 <c>GetComponent&lt;NavMeshAgent&gt;()</c>，
        /// 再是"关 Movement 组件"——**同一个问题的两个写法，都静默不生效**。）</para>
        ///
        /// <para>官方做法就是 <c>MovementEnabled</c>：游戏自己在"别人操控这个角色"时这么关
        /// （<c>CA_ControlOtherCharacter.cs:153</c>）。代价是它的 setter 会**连带关掉 ECM2 的碰撞体**
        /// （<c>Movement.cs:55-58</c>），所以紧接着把它开回来——我们要的是"别自己走"，不是"别被撞"。</para>
        /// </summary>
        protected static void SetCharacterSelfMovement(CharacterMainControl character, bool enabled)
        {
            if (character == null) return;

            if (!enabled)
            {
                // 只有"停"需要取消寻路；"恢复"交给 AI 自己重新决策即可。
                var ai = character.aiCharacterController;
                if (ai != null) ai.StopMove();
            }

            var movement = character.movementControl;
            if (movement == null) return;

            movement.MovementEnabled = enabled;

            // MovementEnabled 的 setter 会连带关掉 ECM2 的碰撞体（Movement.cs:55-58）。
            // 我们要的是"别自己走"，不是"别被撞"——立刻开回来。
            var ecm2 = movement.characterMovement;
            if (!enabled && ecm2 != null && ecm2.collider != null)
            {
                ecm2.collider.enabled = true;
            }
        }

        // --- 默认实现全部留空，允许子类随意覆盖 ---
        public virtual void OnEliteInitialized(CharacterMainControl character) { }
        public virtual void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public virtual void OnCleanup(CharacterMainControl character) { }
        public virtual void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo) { }

        // --- 受保护的助手方法：仅供开发者手动调用 ---

        /// <summary>
        /// 手动清理该词缀对目标造成的**全部**修改：Stat 与 AI 字段都会被撤销。
        /// </summary>
        protected void ClearBaseModifiers(CharacterMainControl character)
        {
            if (character != null)
            {
                CharacterModifiers.ClearAll(character, this.AffixName);
            }
        }

        /// <summary>
        /// 便捷方法：带入 AffixName 的 **Stat** 修改。
        /// 属性名取自 <see cref="StatKeys"/>；要改 AI 字段请用 <see cref="ModifyAI"/>。
        /// 注意：使用此方法后，请务必在 OnCleanup 或 OnEliteDeath 中手动调用 ClearBaseModifiers。
        /// </summary>
        protected void Modify(CharacterMainControl character, string attributeName, float value, bool isMultiplier = true)
        {
            CharacterModifiers.Modify(character, attributeName, value, isMultiplier, this.AffixName);
        }

        /// <summary>
        /// 便捷方法：带入 AffixName 的 **AI float 字段**修改。
        /// 字段取自 <see cref="AIFields"/>，编译期绑定——写错字段名会编译报错。
        /// 注意：使用此方法后，请务必在 OnCleanup 或 OnEliteDeath 中手动调用 ClearBaseModifiers。
        /// </summary>
        /// <param name="multiply">true = 在原值上乘；false = 直接覆盖</param>
        protected void ModifyAI(CharacterMainControl character,
            FieldRef<AICharacterController, float> field, float value, bool multiply = true)
        {
            AIFieldOverrides.Override(character, field, value, multiply, this.AffixName);
        }

        /// <summary>
        /// 便捷方法：带入 AffixName 的 **AI bool 字段**修改（如能否移动射击、能否冲刺）。
        /// 布尔量恒为覆盖，没有「乘」的语义。
        /// 注意：使用此方法后，请务必在 OnCleanup 或 OnEliteDeath 中手动调用 ClearBaseModifiers。
        /// </summary>
        protected void ModifyAI(CharacterMainControl character,
            FieldRef<AICharacterController, bool> field, bool value)
        {
            AIFieldOverrides.Override(character, field, value, this.AffixName);
        }

        /// <summary>
        /// 便捷方法：应用精英怪三维增强（生命 / 伤害 / 移速倍率）。
        /// 注意：使用此方法后，请务必在 OnCleanup 或 OnEliteDeath 中手动调用 ClearBaseModifiers。
        /// </summary>
        protected void ApplyPowerup(CharacterMainControl character, float hpMul, float dmgMul, float spdMul)
        {
            CharacterModifiers.Quick.ApplyElitePowerup(character, hpMul, dmgMul, spdMul, this.AffixName);
        }
    }
}
