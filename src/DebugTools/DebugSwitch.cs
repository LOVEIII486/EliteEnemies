namespace EliteEnemies.DebugTools
{
    /// <summary>
    /// 调试工具的**唯一开关**。
    ///
    /// <para><b>它为什么在 <c>DebugTools</c> 命名空间里，而不是在 <c>ModBehaviour</c> 里</b>：
    /// 原先它是 <c>ModBehaviour</c> 的一个私有字段，而有些工具是被**生产代码**
    /// （敌人生成路径、掉落路径）直接调用的——它们够不着那个私有字段，
    /// 于是**根本不受管辖，每次游玩都在跑**。开关放在调试工具自己名下之后，
    /// 每个调试类都能自门控。</para>
    ///
    /// <para>⚠ <b>统计类的东西不属于这里</b>：本局统计（<c>EliteEnemies.Stats.SessionStats</c>）
    /// 是**常开的**——它的用途是提高玩家出错时那份日志的可分析率，必须出现在正式版里。
    /// 判断标准是「这份输出是给开发者调试用的，还是给排查玩家问题用的」。</para>
    ///
    /// <para><b>为什么是 <c>static readonly</c> 而不是 <c>const</c></b>：<c>const false</c> 会让
    /// 调用方的 <c>if (!DebugSwitch.Enabled) return;</c> 整块变成**不可达代码**（CS0162 警告）。
    /// 用 <c>static readonly</c> 时它是运行期值，编译期不消除分支，也就没有这个警告。</para>
    ///
    /// <para><b>怎么打开</b>：编译期定义 <c>ELITE_DEBUG</c>，见 csproj 的 <c>EliteDebug</c> 属性：</para>
    /// <code>dotnet build -p:EliteDebug=true</code>
    ///
    /// <para>⚠ <b>刻意不是「Debug 配置自动打开」</b>。本工程的部署（<c>DeployToGame</c>）挂在
    /// <c>Build</c> 目标上，而 <c>dotnet build</c> 默认就是 Debug 配置——那样写的话，
    /// 平时一条 <c>dotnet build</c> 就会把**开着调试工具的版本直接部署进游戏**，
    /// 比现在的写死 false 更危险。所以默认是关，要开必须显式要求。</para>
    /// </summary>
    internal static class DebugSwitch
    {
#if ELITE_DEBUG
        internal static readonly bool Enabled = true;
#else
        internal static readonly bool Enabled = false;
#endif
    }
}
