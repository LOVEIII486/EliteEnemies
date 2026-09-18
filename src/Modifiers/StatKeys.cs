namespace EliteEnemies.Modifiers
{
    /// <summary>
    /// 角色属性的 **key 常量表**——stat 名的唯一来源。
    ///
    /// <para>这些字符串会被原样交给游戏的 <c>StatCollection.GetStat(key)</c>；
    /// key 不存在时游戏**返回 null 而不抛异常**，所以写错的历史代价是「静默失效」
    /// （见 <c>WalkSpeed</c> 上方的注释）。现在的防线在 <see cref="StatModifiers.AddModifier"/>：
    /// 它用 <c>Item.AddModifier</c> 的返回值做注册期校验，key 不存在会 <c>LogError</c> 并明说「这次修改不会生效」。</para>
    ///
    /// <para>⚠ 新增常量时请一并确认它在游戏侧真实存在——判据见仓库 docs 里的
    /// 「AI 字段是否被消费」一节：<b>「没搜到」不等于「不存在」</b>。</para>
    /// </summary>
    public static class StatKeys
    {
        public const string MaxHealth = "MaxHealth"; 
        public const string Stamina = "Stamina";
        public const string StaminaDrainRate = "StaminaDrainRate";
        public const string StaminaRecoverRate = "StaminaRecoverRate";
        public const string StaminaRecoverTime = "StaminaRecoverTime";
        public const string MaxEnergy = "MaxEnergy";
        public const string EnergyCost = "EnergyCost";
        public const string MaxWater = "MaxWater";
        public const string WaterCost = "WaterCost";
        public const string MaxWeight = "MaxWeight";
        public const string FoodGain = "FoodGain";
        public const string HealGain = "HealGain";

        // ⚠ 刻意**没有** MoveSpeed 这个 key —— 它**不是**本游戏的 stat key。
        //   历史上这里有一个 `MoveSpeed` 常量，注释宣称"自动分发到 WalkSpeed 和 RunSpeed"，
        //   但那套分发逻辑在任何地方都不存在。后果是 GetStat("MoveSpeed") 返回 null、
        //   修改静默不生效：实机日志里留下 3 条「未能在角色上找到属性 Key: MoveSpeed」，
        //   对应 Berserk / Giant / Mini 三个词条（见 docs\属性模块审查与设计.md §2.2）。
        //   要改移速请调 CharacterModifiers.Quick.ModifySpeed()，它同时写下面两个 key。
        //
        //   ⚠ 特别注意：`"MoveSpeed"` 这个**字符串**在游戏里是存在的 ——
        //   但它是 **Animator 参数名**（CharacterAnimationControl.cs:24 的
        //   `Animator.StringToHash("MoveSpeed")`），与 ItemStatsSystem 的 stat key
        //   是两个不相干的命名空间。不要因为"搜到了"就把它加回来。
        public const string WalkSpeed = "WalkSpeed";
        public const string WalkAcc = "WalkAcc";
        public const string RunSpeed = "RunSpeed";
        public const string RunAcc = "RunAcc";
        public const string TurnSpeed = "TurnSpeed";
        public const string AimTurnSpeed = "AimTurnSpeed";
        public const string DashSpeed = "DashSpeed";
        public const string Moveability = "Moveability";

        public const string GunDamageMultiplier = "GunDamageMultiplier";
        public const string GunShootSpeedMultiplier = "GunShootSpeedMultiplier";
        public const string ReloadSpeedGain = "ReloadSpeedGain";
        public const string GunCritRateGain = "GunCritRateGain";
        public const string GunCritDamageGain = "GunCritDamageGain";
        public const string BulletSpeedMultiplier = "BulletSpeedMultiplier";
        public const string RecoilControl = "RecoilControl";
        public const string GunScatterMultiplier = "GunScatterMultiplier";
        public const string GunDistanceMultiplier = "GunDistanceMultiplier";
        
        public const string MeleeDamageMultiplier = "MeleeDamageMultiplier";
        public const string MeleeCritRateGain = "MeleeCritRateGain";
        public const string MeleeCritDamageGain = "MeleeCritDamageGain";

        public const string HeadArmor = "HeadArmor";
        public const string BodyArmor = "BodyArmor";
        public const string ElementFactor_Physics = "ElementFactor_Physics";
        public const string ElementFactor_Fire = "ElementFactor_Fire";
        public const string ElementFactor_Poison = "ElementFactor_Poison";
        public const string ElementFactor_Electricity = "ElementFactor_Electricity";
        public const string ElementFactor_Space = "ElementFactor_Space";
        public const string ElementFactor_Ghost = "ElementFactor_Ghost";
        public const string ElementFactor_Ice = "ElementFactor_Ice";

        public const string ViewDistance = "ViewDistance";
        public const string ViewAngle = "ViewAngle";
        public const string HearingAbility = "HearingAbility";
        public const string SenseRange = "SenseRange";
        public const string WalkSoundRange = "WalkSoundRange";
        public const string RunSoundRange = "RunSoundRange";
        public const string VisableDistanceFactor = "VisableDistanceFactor";

        public const string GasMask = "GasMask";
        public const string InventoryCapacity = "InventoryCapacity";
        public const string PetCapcity = "PetCapcity";
        public const string FlashLight = "FlashLight";
        
        public const string StormProtection = "StormProtection";
        public const string ColdProtection = "ColdProtection";
        public const string HeatProtection = "HeatProtection";

        public const string WaterEnergyRecoverMultiplier = "WaterEnergyRecoverMultiplier";
        public const string NightVisionAbility = "NightVisionAbility";
        public const string DashCanControl = "DashCanControl";
    }
}
