using System;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【过载】词缀：受到攻击时触发狂暴火力，大幅缩短射击间隔并增加连射时长
    /// </summary>
    public class OverloadBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Overload";
        private bool _isEnraged = false;
        
        private readonly Lazy<string> _popText = new(() => LocalizationManager.GetText("Affix_Overload_PopText", "OVERLOAD!"));

        public override void OnEliteInitialized(CharacterMainControl character) 
        {
            _isEnraged = false;
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 受到伤害且尚未过载时触发
            if (_isEnraged || character == null) return;
            
            _isEnraged = true;
            character.PopText(_popText.Value);

            Modify(character, AIFieldModifier.Fields.ReactionTime, 0.3f, true);
            
            Modify(character, AIFieldModifier.Fields.ShootTimeMax, 4.0f, true);
            Modify(character, AIFieldModifier.Fields.ShootSpaceMin, 0.2f, true);
            Modify(character, AIFieldModifier.Fields.ShootSpaceMax, 0.2f, true);
            Modify(character, AIFieldModifier.Fields.ShootDelay, 0.4f, true);
            
            Modify(character, AIFieldModifier.Fields.ShootCanMove, 1.0f, false);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);
        }
    }
}