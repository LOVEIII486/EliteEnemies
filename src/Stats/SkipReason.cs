namespace EliteEnemies.Stats
{
    /// <summary>
    /// 一只敌人**没有**变成精英的原因。
    ///
    /// <para>⚠ 分两组，含义完全不同，报告里也分两行显示：</para>
    /// <list type="bullet">
    /// <item><b>走到了判定却没成</b>（前四项）——反映的是概率与名单配置，
    /// 调精英率时看的是这一组；</item>
    /// <item><b>根本没参与判定</b>（后三项）——那批敌人本来就不该被精英化，
    /// 它们的作用只是让总数能对上账。</item>
    /// </list>
    ///
    /// <para><b>为什么值得分成七项</b>：早先的实现把前四项**合并成一个桶**
    /// （只记「已跳过」），于是"为什么一个精英都没有"在日志里永远没有答案——
    /// 「概率调成了 0」和「预设根本不在合格名单里」是完全不同的两件事，
    /// 却给出同一行输出。这份统计存在的理由就是把这个区别记下来。</para>
    /// </summary>
    internal enum SkipReason
    {
        // ── 走到了判定 ──

        /// <summary>随机判定没中。**最主要的一类**，直接随精英率设置而变。</summary>
        ChanceMissed,

        /// <summary>既非 Boss、也非商人，且不在合格预设名单里（自动注册也没接住）。</summary>
        IneligibleType,

        /// <summary>在忽略名单里。</summary>
        Ignored,

        /// <summary>抽词条返回空——词条池被禁用光了，或互斥规则导致无解。</summary>
        NoAffixSelected,

        // ── 根本没参与判定 ──

        /// <summary>玩家自己。</summary>
        MainCharacter,

        /// <summary>与玩家同队的单位。</summary>
        FriendlyTeam,

        /// <summary>身上没有 <c>characterPreset</c>，无从判断。</summary>
        NoPreset,
    }
}
