namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 词条行为接口 —— 所有复杂词条必须实现。
    /// <para>实现类放在 <c>Affixes/Behaviors/</c> 下，由 <see cref="AffixBehaviorRegistration"/>
    /// 扫描程序集自动登记，**不需要**任何手写清单。</para>
    /// </summary>
    public interface IAffixBehavior
    {
        string AffixName { get; }
        void OnEliteInitialized(CharacterMainControl character);
        void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo);
        void OnCleanup(CharacterMainControl character);
    }

    /// <summary>额外需要每帧更新的词条行为。</summary>
    public interface IUpdateableAffixBehavior : IAffixBehavior
    {
        void OnUpdate(CharacterMainControl character, float deltaTime);
    }

    /// <summary>额外需要参与战斗事件的词条行为。</summary>
    public interface ICombatAffixBehavior : IAffixBehavior
    {
        /// <summary>
        /// 该敌人**发动攻击**时（射击或近战各一次）。
        ///
        /// <para>⚠ <b><paramref name="damageInfo"/> 是框架**合成**的对象</b>——射击/近战事件本身
        /// 只给一个 <c>DuckovItemAgent</c>（武器），拿不到"打谁、打了多少"。合成对象里只有
        /// <c>fromCharacter</c>（就是这个敌人），**别把它当成本次攻击的真实数据**。
        /// 需要真实数据（武器、目标、伤害）请自己从武器/弹道/命中事件里取。</para>
        /// </summary>
        void OnAttack(CharacterMainControl character, DamageInfo damageInfo);

        /// <summary>该敌人**受到伤害**时。<paramref name="damageInfo"/> 是真实的本次伤害信息。</summary>
        void OnDamaged(CharacterMainControl character, DamageInfo damageInfo);

        /// <summary>
        /// 该敌人**打中玩家**时（由 <c>DamageReceiverPatches</c> 转发）。
        ///
        /// <para><paramref name="attacker"/> 是发动攻击的那个敌人（即 <c>character</c>）；
        /// <paramref name="victim"/> 是**挨打的那个玩家角色**——要给玩家上 debuff 就用它，
        /// **不要用 <c>CharacterMainControl.Main</c>**：联机下挨打的可能是**别的玩家**
        /// （详见 <c>EliteBuffs.ApplyToPlayer</c> 的注释）。</para>
        ///
        /// <para>⚠ 两个参数都是 <c>CharacterMainControl</c>，<b>顺序是"打人的在前、挨打的在后"</b>，
        /// 与 <paramref name="character"/> 同一个东西的是 <paramref name="attacker"/>。</para>
        /// </summary>
        void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo);
    }
}
