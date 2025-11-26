using System;
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

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            // AttributeModifier.Quick.ModifyHealth(character, 0.8f, healToFull: true);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            // 血量低于50%触发狂暴
            if (character.Health.CurrentHealth < character.Health.MaxHealth * 0.7f && !_berserkTriggered)
            {
                _berserkTriggered = true;
                character.PopText(BerserkPopTextFmt);

                // 战斗中立即修改，immediate = true
                StatModifier.Multiply(character, StatModifier.Attributes.GunDamageMultiplier, 1.3f);
                StatModifier.Multiply(character, StatModifier.Attributes.MeleeDamageMultiplier, 1.3f);
                StatModifier.Multiply(character, StatModifier.Attributes.WalkSpeed, 1.4f);

                AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootCanMove, 1f);
                AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootDelay, 0.5f, multiply: true);
                AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.CanDash, 1f);
            }
        }
    }
}