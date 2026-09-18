namespace EliteEnemies.Modifiers
{
    /// <summary>
    /// AI 字段访问器。
    ///
    /// <para><b>全部走编译期访问，不用反射。</b> 依据（2026-09-17 逐字段核实）：
    /// 这些字段在 <c>AICharacterController</c> 上**本来就是 public**
    /// （唯一的 private —— <c>updateValueTimer</c> —— 模组从未使用，已移除），
    /// 本项目又启用了 Publicizer，所以反射在这里纯属多余，
    /// 而且它带来的「改名即静默失效」正是本项目要消除的东西。</para>
    ///
    /// <para>⚠ <b>按消费方式分两类，只暴露这两类，绝不暴露派生字段</b>：</para>
    /// <list type="bullet">
    ///   <item><b>上游</b>：别的值由它派生 —— 必须改它本身；改下游会失效</item>
    ///   <item><b>实时读取</b>：游戏在用的当下直接读它</item>
    /// </list>
    ///
    /// <para>典型反例 <c>reactionTime</c>：它是派生字段，游戏每秒用
    /// <c>baseReactionTime</c> 把它重算一遍（<c>AICharacterController.cs:396-408</c>），
    /// 写它最多活 1 秒。所以这里**只提供 <see cref="BaseReactionTime"/>**，
    /// 从 API 层面杜绝误用——而不是靠注释提醒。</para>
    ///
    /// <para>行号基准：<c>..\DuckovSource-ILSpy\TeamSoda.Duckov.Core\AICharacterController.cs</c>。</para>
    /// </summary>
    public static class AIFields
    {
        // ═══════════════ 上游字段（改这个，下游跟着变） ═══════════════

        /// <summary>
        /// 反应时间的上游值（<c>AICharacterController.cs:81</c>）。
        /// <c>reactionTime</c> 每秒由它重算，白天等于它、夜晚乘以 <c>nightReactionTimeFactor</c>。
        /// </summary>
        public static readonly FieldRef<AICharacterController, float> BaseReactionTime =
            (AICharacterController ai) => ref ai.baseReactionTime;

        // ═══════════════ 实时读取字段（游戏使用时直接读） ═══════════════

        /// <summary>战斗中的转身速度（<c>:39</c>）</summary>
        public static readonly FieldRef<AICharacterController, float> CombatTurnSpeed =
            (AICharacterController ai) => ref ai.combatTurnSpeed;

        /// <summary>开火前摇（<c>:87</c>）</summary>
        public static readonly FieldRef<AICharacterController, float> ShootDelay =
            (AICharacterController ai) => ref ai.shootDelay;

        /// <summary>能否边移动边射击（<c>:31</c>）</summary>
        public static readonly FieldRef<AICharacterController, bool> ShootCanMove =
            (AICharacterController ai) => ref ai.shootCanMove;

        /// <summary>能否冲刺（<c>:99</c>）</summary>
        public static readonly FieldRef<AICharacterController, bool> CanDash =
            (AICharacterController ai) => ref ai.canDash;

        /// <summary>连射时长的上限（<c>shootTimeRange.y</c>，<c>:93</c>）</summary>
        public static readonly FieldRef<AICharacterController, float> ShootTimeMax =
            (AICharacterController ai) => ref ai.shootTimeRange.y;

        /// <summary>射击间隔的下限（<c>shootTimeSpaceRange.x</c>，<c>:95</c>）</summary>
        public static readonly FieldRef<AICharacterController, float> ShootSpaceMin =
            (AICharacterController ai) => ref ai.shootTimeSpaceRange.x;

        /// <summary>射击间隔的上限（<c>shootTimeSpaceRange.y</c>，<c>:95</c>）</summary>
        public static readonly FieldRef<AICharacterController, float> ShootSpaceMax =
            (AICharacterController ai) => ref ai.shootTimeSpaceRange.y;

        /// <summary>冲刺冷却的下限（<c>dashCoolTimeRange.x</c>，<c>:101</c>）</summary>
        public static readonly FieldRef<AICharacterController, float> DashCDMin =
            (AICharacterController ai) => ref ai.dashCoolTimeRange.x;

        /// <summary>冲刺冷却的上限（<c>dashCoolTimeRange.y</c>，<c>:101</c>）</summary>
        public static readonly FieldRef<AICharacterController, float> DashCDMax =
            (AICharacterController ai) => ref ai.dashCoolTimeRange.y;
    }
}
