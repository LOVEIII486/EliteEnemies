using EliteEnemies.Localization;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>
    /// 电磁干扰：关掉 HUD，闪烁一阵，再逐个恢复。
    ///
    /// <para><b>隐藏期的状态不在本类里，在 <see cref="HudHidingGuard"/>。</b>
    /// 原因见那个类的注释：HUD 是**全局资源**，而 Buff 实例每次施加各一份——
    /// 状态留在实例上，两发 EMP 重叠时就会"只关掉半个 HUD、两个还原协程互相打架"。
    /// 本类只负责"进入 / 离开隐藏期"与提示文本。</para>
    ///
    /// <para><b>历史</b>：更早的实现里效果对象是**全局单例**，两发 EMP 会共用同一份快照与
    /// 同一个协程句柄；后来改成"逻辑住在 Buff 实例里"——那一步修的是**跨玩家**共用
    /// （对"玩家身份"是对的），但把**同一玩家身上的重叠触发**漏掉了。现在按"资源是全局的"
    /// 归到 <see cref="HudHidingGuard"/>，两件事一起成立。</para>
    /// </summary>
    public sealed class EMPBuff : EliteBuffBase
    {
        public override string BuffName => "EliteBuff_EMP";
        public override int BuffId => 99906;
        public override float Duration => 5f;

        /// <summary>
        /// 这个 Buff 是不是挂在**本机玩家**身上。
        ///
        /// <para>⚠️ <b>HUD 是「这台机器」的资源，只有挨打的那个玩家就在本机时才该动它。</b>
        /// 联机下精英打中客机时判定在<b>主机</b>上跑，debuff 会被挂到客机玩家的<b>复制体</b>上——
        /// 那时 <c>OnApplied</c> 也在主机上跑。不判这一下，就会
        /// <b>把主机的 HUD 关掉</b>（而真正该关的客机那边，要等 buff 被转发过去才生效）。</para>
        /// </summary>
        private bool OwnsLocalHud => Character != null && Character.IsMainCharacter;

        /// <summary>
        /// 本次是否真的动过 HUD。<b>必须记住，不能在 <c>OnRemoved</c> 里重算。</b>
        ///
        /// <para>若在 <c>OnRemoved</c> 里重新判一次 <see cref="OwnsLocalHud"/>，
        /// 而那时 <c>Character</c> 恰好已不可读，就会出现「Hold 了却没 Release」——
        /// 守卫的持有计数卡住，HUD 再也回不来。记住则 Hold/Release 严格成对。</para>
        /// </summary>
        private bool _heldHud;

        protected override void OnApplied()
        {
            _heldHud = OwnsLocalHud;
            if (_heldHud) HudHidingGuard.Hold();

            PopText("EliteEnemies_Affix_EMP_PopText_1");
        }

        /// <summary>
        /// ⚠ **不能按"角色还在不在"提前返回**：HUD 是场景物体、与角色存活无关，
        /// 而本方法返回后 Buff 的 <c>gameObject</c> 立刻被游戏销毁
        /// （<c>CharacterBuffManager.cs:116-120</c>）——**这是最后一个能离开隐藏期的时机**。
        /// 漏掉 <see cref="HudHidingGuard.Release"/> 会让守卫的持有者计数卡住；
        /// 换关卡时 <see cref="HudHidingGuard.Hold"/> 会把计数连同快照一起重置，见那里的注释。
        /// </summary>
        protected override void OnRemoved()
        {
            if (_heldHud)
            {
                HudHidingGuard.Release();
                _heldHud = false;
            }

            PopText("EliteEnemies_Affix_EMP_PopText_2");
        }

        private void PopText(string localizationKey)
        {
            var text = LocalizationManager.GetText(localizationKey);
            if (!string.IsNullOrEmpty(text)) Character?.PopText(text);
        }
    }
}
