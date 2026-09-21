using System;
using UnityEngine;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 精英**玩法状态**在主机与客机之间转交的中立出口。
    ///
    /// <para><b>为什么要有这一层</b>：联机下有些词条的判定**不在主机上发生**，
    /// 却又依赖主机才知道的状态。最典型的是【反弹】——
    /// 子弹由<b>开枪的那台机器</b>本地模拟（联机侧只给远端投射物造"假"实例），
    /// 所以"这颗子弹该不该被弹开"是<b>客机在自己那边判的</b>；
    /// 而"这只精英此刻在不在反射"只有主机知道。</para>
    ///
    /// <para>但<b>词条行为不该认识联机模块</b>（本工程 §5.5 的单向依赖纪律：
    /// <c>Coop</c> 可以引用 <c>Affixes</c>，反过来不行）。所以这里只留两个"口子"：
    /// 主机侧把状态<b>报出去</b>，客机侧把状态<b>问进来</b>。
    /// <b>没装 ⇒ 恒不接管 / 恒为 false ⇒ 照常本地执行</b>，单机路径一字未改。</para>
    ///
    /// <para>与 <see cref="PlayerEffectRelay"/> 的分工：那个管"作用于玩家、该由哪台机器执行"，
    /// 本类管"精英自身的状态、该由哪台机器说了算"。同一形状，不同语义——
    /// <b>刻意不并成一个类</b>，否则日后"新增一个口子该挂哪边"又要重新想一遍。</para>
    /// </summary>
    internal static class EliteStateRelay
    {
        /// <summary>
        /// 主机侧：某只精英的反射状态变了。**由行为类调用**，
        /// 联机模块据此广播给客机（并写回自己的精英条目）。
        ///
        /// <para>参数是<b>角色对象</b>而不是 <c>aiId</c>——行为类手上只有角色，
        /// 而"角色 ↔ aiId"的映射是联机模块自己的知识（见 <c>CoopEliteSync.FindHostAiId</c>）。</para>
        ///
        /// <para>返回 <c>true</c> = 联机模块已接管。<b>调用方目前不看这个返回值</b>
        /// （本机该做的事与它无关），保留它只是为了与同样形状的
        /// <c>PlayerEffectRelay.EliteVisualHandler</c> 一致——
        /// 那几个口子的冒号后面都写着"已接管 / 未接管"，少一个会让人以为漏了判断。</para>
        ///
        /// <para><b>默认 null ⇒ 什么都不做 ⇒ 单机行为与从前一字不差。</b></para>
        /// </summary>
        public static Func<CharacterMainControl, bool, bool> ReflectStateHandler { get; set; }

        /// <summary>
        /// 客机侧：查询"这个角色实例此刻在不在反射"，由联机模块按<b>主机的权威状态</b>回答。
        ///
        /// <para>参数是 <c>GetInstanceID()</c> 而不是角色对象：调用方是弹道补丁，
        /// 它在<b>每次子弹命中任何 AI</b> 时都要问一次，用实例 ID 才能让客机侧做成
        /// O(1) 的集合查询（见 <c>CoopEliteSync</c> 的客机侧反射表）。</para>
        ///
        /// <para><b>默认 null ⇒ 恒为 false ⇒ 单机照常</b>（单机下反射状态本来就在
        /// <see cref="Behaviors.ReflectBehavior"/> 自己的集合里，走不到这里）。</para>
        /// </summary>
        public static Func<int, bool> ReflectQueryHandler { get; set; }

        /// <summary>主机侧：上报一只精英的反射状态。返回 <c>true</c> = 联机模块已接管。</summary>
        public static void RelayReflectState(CharacterMainControl ai, bool reflecting)
        {
            var handler = ReflectStateHandler;
            if (handler == null) return;

            handler(ai, reflecting);
        }

        /// <summary>客机侧：问一只精英（按实例 ID）此刻在不在反射。没装 ⇒ 恒为 false。</summary>
        public static bool QueryReflectState(int characterInstanceID)
        {
            var handler = ReflectQueryHandler;
            return handler != null && handler(characterInstanceID);
        }
    }
}
