using System;
using UnityEngine;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    public class OverloadBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Overload";
        private bool _isEnraged = false;
        
        private readonly Lazy<string> _popText = new(() => LocalizationManager.GetText("Affix_Overload_PopText", "!"));

        public override void OnEliteInitialized(CharacterMainControl character) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_isEnraged || character == null) return;
            
            _isEnraged = true;
            character.PopText(_popText.Value);


            AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ReactionTime, -0.1f, false);
            AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootTimeMin, 7.0f, true);
            AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootTimeMax, 7.0f, true);
            AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootDelay, 0.1f, true);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo) { }
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}