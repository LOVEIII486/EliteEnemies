// VampirismBehavior.cs
// 词缀：吸血（Vampirism）
// 敌人近战攻击命中玩家时，回复自身生命值

using System;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 词缀：吸血
    /// 敌人攻击玩家时回复自身生命值（近战 50%，远程 20%）
    /// </summary>
    public class VampirismBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Vampirism";
        
        private static readonly float MeleeLifeStealPercent = 1.0f;  // 近战 100%
        private static readonly float RangedLifeStealPercent = 0.5f; // 远程 50%
        private static readonly float CooldownSeconds = 0.5f; // cd

        private readonly Lazy<string> _vampirePopTextLazy = new(() =>
            LocalizationManager.GetText(
                "Affix_Vampirism_PopText_1",
                "<color=#DC143C>吸血回复！+{0}</color>"
            )
        );
        private string VampirePopText => _vampirePopTextLazy.Value;

        public void OnAttack(CharacterMainControl attacker, DamageInfo dmg)
        {
            // 获取组件数据存储
            var component = attacker.GetComponent<EliteBehaviorComponent>();
            if (component == null)
            {
                //Debug.LogWarning($"[Vampirism] {attacker.name} 没有 EliteBehaviorComponent！");
                return;
            }
    
            // 获取该敌人的上次治疗时间
            float lastHeal = component.GetCustomData<float>("VampirismLastHeal", -999f);
    
            // 冷却检查
            if (Time.time - lastHeal < CooldownSeconds)
                return;
    
            // 确保攻击者和生命组件有效
            if (attacker == null || attacker.Health == null || attacker.Health.IsDead) 
                return;

            // 确保玩家存在
            var player = CharacterMainControl.Main;
            if (!player) return;

            // 确保击中了玩家
            if (!AffixBehaviorUtils.IsPlayerHitByAttacker(attacker))
            {
                // Debug.LogWarning($"{attacker.characterPreset.DisplayName}攻击未命中");
                return;
            }

            // 判断攻击类型
            bool isMelee = IsMeleeAttack(attacker, dmg);
            float lifeStealPercent = isMelee ? MeleeLifeStealPercent : RangedLifeStealPercent;

            // 计算回复量（基于最大生命值的百分比）
            float healAmount = Mathf.Clamp(attacker.Health.MaxHealth * 0.20f * lifeStealPercent, 15f, 60f);
            
            // 回复生命
            float beforeHealth = attacker.Health.CurrentHealth;
            attacker.Health.AddHealth(healAmount);
            float actualHeal = attacker.Health.CurrentHealth - beforeHealth;

            // 如果实际回复了生命值，显示提示
            if (actualHeal > 0.1f)
            {
                string popText = string.Format(VampirePopText, Mathf.CeilToInt(actualHeal));
                attacker.PopText(popText);
                component.SetCustomData("VampirismLastHeal", Time.time);
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo dmg)
        {
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
    }
}
