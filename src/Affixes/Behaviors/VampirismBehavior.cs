using System;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 词缀：吸血
    /// 敌人攻击玩家时，按造成伤害的百分比回复自身生命值
    /// 近战 100% 吸血，远程 50% 吸血
    /// </summary>
    public class VampirismBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Vampirism";

        // 吸血比例
        private static readonly float MeleeLifeStealPercent = 1.5f; // 近战：造成伤害的 150%
        private static readonly float RangedLifeStealPercent = 0.6f; // 远程：造成伤害的 60%

        // 吸血限制
        private static readonly float MinHealAmount = 10f; // 最小回复量
        private static readonly float MaxHealAmount = 100f; // 最大回复量
        private static readonly float CooldownSeconds = 0.15f; // 触发冷却

        /// <summary>
        /// 上次成功吸血的时间。**这是行为自己的字段**，不再记在
        /// <c>EliteBehaviorComponent</c> 的自定义数据袋里——行为实例本就是"每个敌人一份"
        /// （<c>Initialize</c> 为每个词条 new 一个实例），记在组件上只是多绕一层；
        /// 而且原先为了写这个值，要在**每次打中玩家**时 <c>GetComponent&lt;EliteBehaviorComponent&gt;()</c>。
        /// </summary>
        private float _lastHealTime = -999f;

        private string VampirePopText =>
            LocalizationManager.GetText(
                "EliteEnemies_Affix_Vampirism_PopText_1",
                "<color=#DC143C>吸血 +{0}</color>");

        public void OnAttack(CharacterMainControl attacker, DamageInfo dmg)
        {
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo dmg)
        {
        }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (Time.time - _lastHealTime < CooldownSeconds)
                return;
            
            if (attacker == null || attacker.Health == null || attacker.Health.IsDead)
                return;
            
            // ⚠ 目标必须是 **victim（被打中的那个玩家）**，不是 `CharacterMainControl.Main`。
            //   联机下判定在主机上跑，而挨打的往往是**客机玩家的复制体**；
            //   写死 Main 会把效果挂到主机自己的玩家身上，客机什么都看不到。
            var player = victim;
            if (!player)
                return;
            
            // finalDamage 在此时还是 0，因为实际伤害计算在 health.Hurt() 之后
            float damage = damageInfo.damageValue;
            
            if (damage <= 0)
            {
                return;
            }
            
            bool isMelee = IsMeleeAttack(attacker, damageInfo);
            float lifeStealPercent = isMelee ? MeleeLifeStealPercent : RangedLifeStealPercent;

            // 计算回复量：估算伤害 × 吸血比例
            float healAmount = damage * lifeStealPercent;
            healAmount = Mathf.Clamp(healAmount, MinHealAmount, MaxHealAmount);
            
            float beforeHealth = attacker.Health.CurrentHealth;
            float maxHealth = attacker.Health.MaxHealth;

            attacker.Health.AddHealth(healAmount);

            float afterHealth = attacker.Health.CurrentHealth;
            float actualHeal = afterHealth - beforeHealth;

            // 如果实际回复了生命值，显示提示
            if (actualHeal > 0.1f)
            {
                string popText = string.Format(VampirePopText, Mathf.CeilToInt(actualHeal));
                PlayerEffectRelay.PopTextOnElite(attacker, popText);

                // 更新冷却时间
                _lastHealTime = Time.time;
            }
        }

        /// <summary>
        /// 判断是否为近战攻击。
        ///
        /// <para>用**游戏自己的标记**分流，而不是猜类型名：枪在生成时由
        /// <c>ItemSetting_Gun.SetMarkerParam</c> 给物品打上 <c>"IsGun"</c>
        /// （<c>ItemSetting_Gun.cs:543</c>），游戏自己也这么判（<c>ItemExtensions.cs:58</c>）。
        /// 原先这里是 <c>currentAgent.GetType().Name.Contains("Gun")</c>——
        /// 类名一改就静默判错，模组加的非标准枪械（类名不含 Gun）也会被误判成近战，
        /// 从而按 150% 而不是 60% 吸血。</para>
        /// </summary>
        private bool IsMeleeAttack(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            // 如果是爆炸伤害，不算近战
            if (damageInfo.isExplosion)
                return false;

            // 获取当前持握物品。拿不到物品（空手）算近战
            var currentAgent = attacker.CurrentHoldItemAgent;
            if (currentAgent == null || currentAgent.Item == null)
                return true;

            // 官方标记：见 ItemSetting_Gun.SetMarkerParam（ItemSetting_Gun.cs:543）
            return !currentAgent.Item.GetBool("IsGun");
        }






    }
}