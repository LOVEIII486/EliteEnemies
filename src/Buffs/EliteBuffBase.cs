using System;
using Duckov.Buffs;
using Duckov.Utilities;
using EliteEnemies.Modifiers;
using ItemStatsSystem.Stats;
using UnityEngine;

namespace EliteEnemies.Buffs
{
    /// <summary>
    /// 精英 Buff 基类。**一个 Buff = 一个子类 = 一个文件**——名字、ID、时长、层数、效果逻辑
    /// 全在子类里，运行期按**类型**派发，不查表。
    ///
    /// <para><b>为什么是"子类"而不是"注册表查名字"</b>：历史实现靠
    /// <c>GameObject.name</c> 反查一个字符串注册表（<c>EliteBuffRegistry.GetEffect(buffName)</c>），
    /// 于是名字与数字 ID 在两个文件里各写一遍、没有任何东西保证一致，任一处漂移就是
    /// <c>if (effect == null) return;</c> ——**静默不生效**。改成子类之后，
    /// "找不到"这个分支在编译期就不存在了。
    ///
    /// <para><b>实测依据</b>（见 <c>docs\Buff模块审查与设计.md</c> §6）：
    /// 游戏的 <c>CharacterBuffManager.AddBuff</c> 走 <c>Instantiate(buffPrefab)</c>，
    /// 而 Unity 克隆 GameObject 保留组件类型、随后显式调 <c>Setup</c> → 虚方法落到子类。
    /// 实机日志已确认：<c>实例类型 = EliteEnemies.DebugTools.ProbeBuff</c>。</para>
    ///
    /// <para><b>子类只需要两样东西</b>：<see cref="BuffName"/> 与 <see cref="BuffId"/>，
    /// 加上（可选的）<see cref="ApplyStats"/> / <see cref="OnApplied"/> 等钩子。</para>
    /// </summary>
    public abstract class EliteBuffBase : Buff
    {
        private const string LogTag = "[EliteEnemies.EliteBuff]";

        // ═══════════════ 身份：名字与 ID 的**唯一来源** ═══════════════

        /// <summary>Buff 名。同时决定 GameObject 名与本地化 key（<c>Buff_&lt;名&gt;_Name</c>）。</summary>
        public abstract string BuffName { get; }

        /// <summary>
        /// 交给游戏的数字 ID（游戏用它判身份与叠层）。
        /// 必须落在 <see cref="EliteBuffRegistry.IdRangeMin"/>–<see cref="EliteBuffRegistry.IdRangeMax"/> 内，
        /// 且与其它 Buff 不重复——两条都会在注册时被自检拦下。
        /// </summary>
        public abstract int BuffId { get; }

        // ═══════════════ 配置：子类按需覆写 ═══════════════

        /// <summary>持续时间（秒）。</summary>
        public virtual float Duration => 5f;

        /// <summary>最大层数。同 ID 再施加会加层而不是新建实例。</summary>
        public virtual int LayerLimit => 1;

        /// <summary>是否携带描述。覆写为 false 则不设 <c>description</c>、也不要求有对应的本地化键。</summary>
        public virtual bool HasDescription => true;

        // ═══════════════ 本地化键 ═══════════════
        //
        // 做成 virtual 而不是写死，是因为本模块将来要整体搬进前置库——
        // 托管方应当能换掉"Buff_<名>_Name"这套约定，而不必改基类。
        //
        // ⚠ 键被谁解析很关键：Buff 的 DisplayName / Description 由**游戏自己的**本地化器
        //   解析（displayName.ToPlainText()，Buff.cs:130/134），而我们的 CSV 是
        //   本模组自己的 CSVFileLocalizor 读的——两者互不相通。所以光设键不够，
        //   本地化模块必须把文本**全量推给游戏**，见 LocalizationManager.PushToGame。

        /// <summary>名称的本地化键。</summary>
        protected internal virtual string NameKey => $"EliteEnemies_Buff_{BuffName}_Name";

        /// <summary>描述的本地化键。</summary>
        protected internal virtual string DescriptionKey => $"EliteEnemies_Buff_{BuffName}_Description";

        // ═══════════════ 模板装配：每个类型只跑一次 ═══════════════

        /// <summary>
        /// 由 <see cref="EliteBuffRegistry"/> 在注册时调用一次，把子类声明的身份写进这个模板实例。
        /// 之后游戏每次施加都会 <c>Instantiate</c> 这个模板，配置随之被克隆。
        /// </summary>
        internal void ConfigureTemplate()
        {
            name = BuffName;
            ID = BuffId;
            limitedLifeTime = true;
            totalLifeTime = Duration;
            maxLayers = LayerLimit;

            // ★ 存 **key** 而不是"已经解析好的文本"：游戏在显示时才解析
            //   （Buff.DisplayName => displayName.ToPlainText()，Buff.cs:130），
            //   于是换语言会实时生效；也顺带免疫了 AGENT.md §3.5 那个
            //   "静态初始化器里调 GetText 把兜底文本冻死"的雷——本方法在启动早期跑，
            //   若在这里解析文本，本地化还没就绪，冻死的就是我们。
            //
            //   代价是**必须**把这两个键推给游戏本地化器（我们的 CSV 它读不到），
            //   由 LocalizationManager 全量完成。
            displayName = NameKey;
            if (HasDescription) description = DescriptionKey;

            // 表现层字段从游戏自带的 BaseBuff 抄一份。
            // 本模板是**新建的 GameObject**，不抄的话 icon / 特效 / 互斥标签全是 C# 默认值，
            // 而历史实现是"克隆 BaseBuff"——那些值本来就带着。不抄等于静默改了外观。
            //
            // ⚠ 这一段**单独 try/catch**：拿不到 BaseBuff 只该让外观退回默认值，
            //   **绝不能**让异常冒出去——外层（注册表）的 catch 会把这个 Buff 整个丢弃，
            //   于是一个外观问题变成"词条效果彻底失效"。
            try
            {
                var baseBuff = GameplayDataSettings.Buffs?.BaseBuff;
                if (baseBuff == null)
                {
                    Debug.LogWarning($"{LogTag} 拿不到 BaseBuff，{BuffName} 将使用默认的 icon/特效");
                    return;
                }

                icon = baseBuff.icon;
                description = baseBuff.description;
                buffFxPfb = baseBuff.buffFxPfb;
                exclusiveTag = baseBuff.exclusiveTag;
                exclusiveTagPriority = baseBuff.exclusiveTagPriority;
                hide = baseBuff.hide;
                displayInExtraView = baseBuff.displayInExtraView;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 复制 BaseBuff 的表现层字段失败（不影响功能）: {ex.Message}");
            }
        }

        // ═══════════════ 收口游戏的虚方法 ═══════════════

        // 用 sealed 收口：子类只能覆写下面那几个钩子，绕不过基类的记账
        // （订阅层数事件 / 订阅移除事件 / 自动撤销属性）。这是刻意的——
        // 「把约束交给类型系统」而不是靠注释提醒。

        protected sealed override void OnSetup()
        {
            OnLayerChangedEvent += HandleLayerChanged;

            if (master != null)
            {
                master.onRemoveBuff += HandleRemoved;
            }
            else
            {
                // 取不到管理器 = 订阅不上移除事件 = 移除时不会清理。**必须报出来**：
                // 这种失败的症状是"属性加成了但一直不撤"，而没有任何报错。
                Debug.LogError($"{LogTag} {BuffName} 的 master 为 null，无法订阅移除事件——" +
                               "本 Buff 的属性修改将无法在移除时撤销");
            }

            Safe(nameof(ApplyStats), ApplyStats);
            Safe(nameof(OnApplied), OnApplied);
        }

        protected sealed override void OnUpdate() => Safe(nameof(OnTick), OnTick);

        protected sealed override void OnNotifiedOutOfTime() => Safe(nameof(OnExpired), OnExpired);

        // ═══════════════ 子类钩子 ═══════════════

        /// <summary>
        /// 施加属性效果。**层数变化时会再来一次**，所以要么是幂等的，要么先用内部字段记住
        /// 随机值（见 <c>TearBuff</c>）。不要在这里做「只该发生一次」的事——
        /// 那类逻辑放 <see cref="OnApplied"/>。
        /// </summary>
        protected virtual void ApplyStats() { }

        /// <summary>Buff 挂上时调用一次。</summary>
        protected virtual void OnApplied() { }

        /// <summary>每帧（由 <c>CharacterBuffManager.Update</c> 驱动，不是 Unity 的 Update）。</summary>
        protected virtual void OnTick() { }

        /// <summary>超时到点、即将被移除时。</summary>
        protected virtual void OnExpired() { }

        /// <summary>层数变化时（同 ID 再施加、或被"移除一层"）。</summary>
        protected virtual void OnRefreshed() { }

        /// <summary>
        /// 被移除时。此时 <c>Character</c> **仍可读**、<c>gameObject</c> **还活着**
        /// （实测确认，见设计文档 §6.2），所以可以安全地做清理。
        ///
        /// <para>属性撤销**不需要**写在这里——基类已经自动做了。</para>
        /// </summary>
        protected virtual void OnRemoved() { }

        // ═══════════════ 给子类的工具 ═══════════════

        /// <summary>改属性，**按当前层数缩放**（对齐游戏自己的 <c>ModifierAction</c>：值 = 基础值 × 层数）。</summary>
        protected void AddStat(string statKey, float value, ModifierType type = ModifierType.PercentageMultiply)
            => AddStatFlat(statKey, value * CurrentLayers, type);

        /// <summary>改属性，不随层数缩放。</summary>
        protected void AddStatFlat(string statKey, float value, ModifierType type = ModifierType.PercentageMultiply)
            => StatModifiers.AddModifier(Character, statKey, value, type, this);

        /// <summary>
        /// 撤销本 Buff 加上的**全部**属性修改。
        ///
        /// <para>走的是 <c>this</c> 这个 source，所以不需要自己维护「加过哪几条、原值多少」的记账表——
        /// 一句就够（见 <c>..\Docs\agent\04-架构模式.md</c> §10.1）。
        /// 基类在层数变化与移除时**已经自动调它**，子类一般用不到。</para>
        /// </summary>
        protected void RemoveStats()
            => Character?.CharacterItem?.RemoveAllModifiersFrom(this);

        // ═══════════════ 内部 ═══════════════

        private void HandleLayerChanged()
        {
            // 先撤后加：层数变了，效果要按新层数重建。
            // 撤销走 source（一句 RemoveAllModifiersFrom(this)），所以这是幂等的、成本只有几个 Modifier。
            //
            // ⚠ 这条**必须**有：CharacterBuffManager.RemoveBuff 在"减一层且还剩层"时直接 return，
            //   **不触发 onRemoveBuff**，只经 CurrentLayers 的 setter 发这个事件（见设计文档 §5.2.1）。
            //   不重建的话，减层后属性会停在旧值。
            RemoveStats();
            Safe(nameof(ApplyStats), ApplyStats);
            Safe(nameof(OnRefreshed), OnRefreshed);
        }

        private void HandleRemoved(CharacterBuffManager manager, Buff removed)
        {
            // 事件挂在**管理器**上，它比 Buff 活得久，且会为**别的** Buff 的移除触发
            if (removed != this) return;

            if (master != null) master.onRemoveBuff -= HandleRemoved;

            // ★ 基类保证对称：ApplyStats 配一个自动撤销，子类不必自己记
            RemoveStats();
            Safe(nameof(OnRemoved), OnRemoved);
        }

        /// <summary>
        /// 钩子里抛异常不该炸掉调用方（<c>AddBuff</c> 在游戏流程里），但**必须报出来**——
        /// 静默吞掉正是本项目一路在清的东西。
        /// </summary>
        private static void Safe(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} {what} 抛异常: {ex}");
            }
        }
    }
}
