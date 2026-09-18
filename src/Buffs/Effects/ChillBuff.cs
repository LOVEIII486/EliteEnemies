using System;
using System.Collections.Generic;
using Duckov.Buffs;
using Duckov.Utilities;
using EliteEnemies.Modifiers;
using UnityEngine;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>
    /// 寒冷：玩家身上的**冻结进度条**。由「严寒」词条（<c>FrozenBehavior</c>）每次命中叠一层，
    /// 叠满 <see cref="MaxStack"/> 层立刻转化为**冻结**（原生 Buff <see cref="FrozenBuffId"/>），
    /// 层数随之清零。
    ///
    /// <para><b>层数不需要我们做 UI</b>：游戏自己的 buff 栏在 <c>MaxLayers &gt; 1</c> 时会显示
    /// <c>CurrentLayers</c>（<c>Duckov/UI/BuffsDisplayEntry.cs:82-84</c>、
    /// <c>Duckov/UI/ExtraBuffViewEntry.cs:38</c>），玩家看到的就是 1/5…5/5。</para>
    ///
    /// <para><b>为什么"转冻结"写在这里而不是行为类里</b>：本 Buff 挂在**玩家**身上，
    /// 层数累计与"是哪个精英打的"无关——两个精英各打两下就该叠到 4 层。
    /// 记在行为类里的话，每个精英各记各的，永远也叠不满。</para>
    /// </summary>
    public sealed class ChillBuff : EliteBuffBase
    {
        /// <summary>满层层数，同时也是 <see cref="LayerLimit"/>。</summary>
        public const int MaxStack = 5;

        /// <summary>原生「冻结」Buff 的 ID。找不到它时寒冷叠满不会转化，启动后第一次触发会报错。</summary>
        private const int FrozenBuffId = 1127;

        /// <summary>每层降低的移动速度。<see cref="EliteBuffBase.AddStat"/> 会按层数缩放，故满层为 -40%。</summary>
        private const float SpeedReductionPerLayer = -0.08f;

        /// <summary>冻结结束之后的免疫时长（秒）。</summary>
        public const float FreezeImmunitySeconds = 10f;

        private const string LogTag = "[EliteEnemies.ChillBuff]";

        /// <summary>找不到原生冻结 Buff 时只报一次错——每次满层都刷屏没有意义。</summary>
        private static bool _frozenLookupFailed;

        /// <summary>
        /// 最近一次**观察到**玩家处于冻结状态的时刻，见 <see cref="IsFreezeImmune"/>。
        ///
        /// <para>纯静态的一个 float，**刻意不做场景卸载清理**：它不持有任何对象引用（没有
        /// <c>BulletDeflectionTracker</c> 那类"攒着已销毁的引用"的泄漏问题），而且判据是
        /// <c>Time.time</c>——它跨场景只增不减，所以残留值自己会在 <see cref="FreezeImmunitySeconds"/> 秒内失效。</para>
        /// </summary>
        private static float _lastSeenFrozenAt = -999f;

        public override string BuffName => "EliteBuff_Chill";
        public override int BuffId => 99909;
        public override float Duration => 6f;
        public override int LayerLimit => MaxStack;

        /// <summary>
        /// 玩家此刻是否处于「冻结免疫」中：**冻结期间**，以及冻结结束后的
        /// <see cref="FreezeImmunitySeconds"/> 秒。冻结免疫期间不再叠寒冷，因此也不会再次冻结。
        ///
        /// <para><b>为什么必须有这个</b>：没有它就会**连锁冻结**——冻结期间寒冷照叠，
        /// 2 秒后又满层，于是再走一次 <c>AddBuff</c>；而同 ID 走的是刷新分支
        /// （<c>CharacterBuffManager.cs:51-55</c>），冻结时长被重新刷满。
        /// 只要精英持续命中（每 0.5 秒一次就够），玩家就**永远出不来**。</para>
        ///
        /// <para><b>为什么冻结期间也算免疫</b>：只免"结束后的那几秒"是不够的。冻结期间寒冷会
        /// 一路叠回满层并停在那里，冰一化开，下一次命中立刻把它兑换成新的冻结——
        /// 免疫窗口等于形同虚设。</para>
        ///
        /// <para><b>副作用（有意）</b>：本方法是**观察式**的——被调用时会记下"此刻玩家冻着"。
        /// 因此"冻结结束"这个时刻是按**命中**的节奏被发现的（精度 = 一次命中间隔，通常 ≤0.5 秒），
        /// 而不是靠订阅事件。这样换来的是零订阅、零生命周期管理；而且它只在**有人正在打**的时候
        /// 才有意义——没人打的时候也没人在叠寒冷。</para>
        /// </summary>
        public static bool IsFreezeImmune(CharacterMainControl player)
        {
            if (player == null) return false;

            if (player.HasBuff(FrozenBuffId))
            {
                _lastSeenFrozenAt = Time.time;
                return true;
            }

            return Time.time < _lastSeenFrozenAt + FreezeImmunitySeconds;
        }

        protected override void ApplyStats()
        {
            AddStat(StatKeys.WalkSpeed, SpeedReductionPerLayer);
            AddStat(StatKeys.RunSpeed, SpeedReductionPerLayer);
        }

        protected override void OnRefreshed()
        {
            if (CurrentLayers < MaxLayers) return;

            CharacterMainControl character = Character;
            if (character == null) return;

            // ★ 先把自己摘干净，再挂冻结。反过来的话，冻结一结束满层的寒冷还在，
            //   玩家下一次被命中就立刻再次冻结——等于永远出不来。
            //
            //   这是防连锁冻结的**第一半**（别让寒冷以满层驻留），
            //   第二半是 IsFreezeImmune（冻结期间与结束后 FreezeImmunitySeconds 秒内根本不叠）。
            //   两半都要：只做这里，冻结期间寒冷仍会叠到 4 层、冰一化开就补满；
            //   只做那里，一旦免疫判断被人改错就退化成永久冻结。
            //
            // ⚠ 此刻的调用栈是 CharacterBuffManager.AddBuff → NotifyIncomingBuffWithSameID
            //   → CurrentLayers setter → OnLayerChangedEvent → HandleLayerChanged → 这里，
            //   属于**重入**地增删 Buff。安全：那条路径用的是 List.Find 而不是遍历器。
            //   但同样的写法**不能**搬到 OnTick——它由 CharacterBuffManager.Update 的
            //   foreach 驱动，在遍历中改列表会抛异常（08-buffs-and-effects.md §6.8）。
            CharacterMainControl from = fromWho;
            character.RemoveBuff(BuffId, removeOneLayer: false);

            Buff frozen = FindFrozenPrefab();
            if (frozen == null) return;

            character.AddBuff(frozen, from);
        }

        /// <summary>
        /// 从游戏的原生 Buff 表里按 ID 取「冻结」预制体。
        ///
        /// <para>每次都查一遍而不做缓存：调用时机是"寒冷叠满"，最密也不过每几秒一次，
        /// 而缓存一个游戏资产引用会引出"场景卸载后引用是否还有效"的问题——不值得。</para>
        /// </summary>
        private static Buff FindFrozenPrefab()
        {
            try
            {
                // allBuffs 是私有 List<Buff>
                // （TeamSoda.Duckov.Core/Duckov/Utilities/GameplayDataSettings.cs:347），
                // 由 Publicizer 在编译期公开，直接访问。
                List<Buff> list = GameplayDataSettings.Buffs?.allBuffs;
                if (list != null)
                {
                    Buff found = list.Find(b => b.ID == FrozenBuffId);
                    if (found != null) return found;
                }
            }
            catch (Exception ex)
            {
                if (_frozenLookupFailed) return null;
                _frozenLookupFailed = true;
                Debug.LogError($"{LogTag} 读取原生 Buff 表失败: {ex}");
                return null;
            }

            if (!_frozenLookupFailed)
            {
                _frozenLookupFailed = true;
                Debug.LogError($"{LogTag} 原生 Buff 表里找不到 ID={FrozenBuffId} 的冻结 Buff——" +
                               "寒冷叠满后不会转化为冻结（该词条只剩减速效果）");
            }
            return null;
        }
    }
}
