using System;
using UnityEngine;
using ItemStatsSystem.Stats; // 必须引用，用于 ModifierType
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    public class BerserkBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Berserk";
        private bool _berserkTriggered = false;

        private readonly Lazy<string> _berserkPopTextFmt = new(() => LocalizationManager.GetText("Affix_Berserk_PopText_1"));

        private string BerserkPopTextFmt => _berserkPopTextFmt.Value;

        public override void OnEliteInitialized(CharacterMainControl character) { }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (character == null || character.Health == null) return;

            // 血量低于 70% 触发狂暴
            if (character.Health.CurrentHealth < character.Health.MaxHealth * 0.7f && !_berserkTriggered)
            {
                _berserkTriggered = true;
                character.PopText(BerserkPopTextFmt);

                // --- 修改 1: 使用 AddModifier ---
                // 1.3 倍伤害 = 基础值 + 0.3 倍增量
                StatModifier.AddModifier(character, StatModifier.Attributes.GunDamageMultiplier, 0.3f, ModifierType.PercentageMultiply);
                StatModifier.AddModifier(character, StatModifier.Attributes.MeleeDamageMultiplier, 0.3f, ModifierType.PercentageMultiply);
                
                // 移速增加 40% (0.4f)
                // 建议同时修改行走和奔跑速度
                StatModifier.AddModifier(character, StatModifier.Attributes.WalkSpeed, 0.4f, ModifierType.PercentageMultiply);
                StatModifier.AddModifier(character, StatModifier.Attributes.RunSpeed, 0.4f, ModifierType.PercentageMultiply);

                // --- 修改 2: AI 修改 ---
                // 允许射击移动 (1f 代表 true)
                AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootCanMove, 1f, false);
                
                // 注意：ShootDelay 已被移除，因为在新框架的白名单中未找到该字段。
                // 如果确认游戏内有该字段，需先在 AIFieldModifier.cs 中添加它。
                // AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootDelay, 0.5f, true); 
            }
        }
        
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}