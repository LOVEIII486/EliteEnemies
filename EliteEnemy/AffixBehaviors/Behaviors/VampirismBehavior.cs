// VampirismBehavior.cs
// 词缀：吸血（Vampirism）
// 敌人攻击命中玩家时，根据实际造成的伤害回复自身生命值

using System;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
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
        private static readonly float MinHealAmount = 8f; // 最小回复量
        private static readonly float MaxHealAmount = 100f; // 最大回复量
        private static readonly float CooldownSeconds = 0.15f; // 触发冷却

        private readonly Lazy<string> _vampirePopTextLazy = new(() =>
            LocalizationManager.GetText(
                "Affix_Vampirism_PopText_1",
                "<color=#DC143C>吸血 +{0}</color>"
            )
        );

        private string VampirePopText => _vampirePopTextLazy.Value;

        public void OnAttack(CharacterMainControl attacker, DamageInfo dmg)
        {
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo dmg)
        {
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            var component = attacker.GetComponent<EliteBehaviorComponent>();
            if (component == null)
                return;
            
            float lastHeal = component.GetCustomData<float>("VampirismLastHeal", -999f);
            if (Time.time - lastHeal < CooldownSeconds)
                return;
            
            if (attacker == null || attacker.Health == null || attacker.Health.IsDead)
                return;
            
            var player = CharacterMainControl.Main;
            if (!player)
                return;
            
            // finalDamage 在此时还是 0，因为实际伤害计算在 health.Hurt() 之后
            float damage = damageInfo.damageValue;
            
            if (damage <= 0)
            {
                Debug.Log($"[VampirismBehavior] 基础伤害无效: {damage}");
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
                attacker.PopText(popText);

                // 更新冷却时间
                component.SetCustomData("VampirismLastHeal", Time.time);
            }
        }

        /// <summary>
        /// 判断是否为近战攻击
        /// </summary>
        private bool IsMeleeAttack(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            // 如果是爆炸伤害，不算近战
            if (damageInfo.isExplosion)
                return false;

            // 获取当前持握物品
            var currentAgent = attacker.CurrentHoldItemAgent;
            if (currentAgent == null)
                return true; // 空手攻击算近战

            // 检查是否为枪械类型
            string agentTypeName = currentAgent.GetType().Name;
            bool isGun = agentTypeName.Contains("Gun") || agentTypeName.Contains("Rifle");

            // 非枪械类型算近战
            return !isGun;
        }

        public override void OnEliteInitialized(CharacterMainControl character)
        {
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
        }
    }
}