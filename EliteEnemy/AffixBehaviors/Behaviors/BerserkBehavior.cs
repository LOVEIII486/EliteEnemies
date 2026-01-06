using System;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 狂暴
    /// </summary>
    public class BerserkBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Berserk";
        
        private bool _berserkTriggered = false;
        private readonly Lazy<string> _berserkPopTextFmt = new(() => LocalizationManager.GetText("Affix_Berserk_PopText_1"));

        public override void OnEliteInitialized(CharacterMainControl character) 
        {
            _berserkTriggered = false;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (character == null || character.Health == null) return;

            // 血量低于 70% 触发狂暴
            if (character.Health.CurrentHealth < character.Health.MaxHealth * 0.7f && !_berserkTriggered)
            {
                TriggerBerserk(character);
            }
        }

        private void TriggerBerserk(CharacterMainControl character)
        {
            _berserkTriggered = true;
            character.PopText(_berserkPopTextFmt.Value);

            // 1. 伤害增加 30%
            Modify(character, StatModifier.Attributes.GunDamageMultiplier, 1.3f, true);
            Modify(character, StatModifier.Attributes.MeleeDamageMultiplier, 1.3f, true);
            
            // 2. 移速增加 30%
            Modify(character, StatModifier.Attributes.MoveSpeed, 1.3f, true);

            // 3. AI 逻辑增强
            Modify(character, AIFieldModifier.Fields.ShootCanMove, 1.0f, false);
            Modify(character, AIFieldModifier.Fields.CanDash, 1.0f, false);
            Modify(character, AIFieldModifier.Fields.ReactionTime, 0.5f, true);
        }
        
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);
            _berserkTriggered = false;
        }
    }
}