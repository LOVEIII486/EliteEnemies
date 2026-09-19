using System;
using EliteEnemies.Modifiers;
using EliteEnemies.Localization;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【过载】词缀：受到攻击时触发狂暴火力，大幅缩短射击间隔并增加连射时长
    /// </summary>
    public class OverloadBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Overload";
        private bool _isEnraged = false;
        
        private string OverloadPopText => LocalizationManager.GetText("EliteEnemies_Affix_Overload_PopText", "OVERLOAD!");

        public override void OnEliteInitialized(CharacterMainControl character) 
        {
            _isEnraged = false;
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 受到伤害且尚未过载时触发
            if (_isEnraged || character == null) return;
            
            _isEnraged = true;
            PlayerEffectRelay.PopTextOnElite(character, OverloadPopText);

            ModifyAI(character, AIFields.BaseReactionTime, 0.3f);

            ModifyAI(character, AIFields.ShootTimeMax, 4.0f);
            ModifyAI(character, AIFields.ShootSpaceMin, 0.2f);
            ModifyAI(character, AIFields.ShootSpaceMax, 0.2f);
            ModifyAI(character, AIFields.ShootDelay, 0.4f);

            ModifyAI(character, AIFields.ShootCanMove, true);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        


        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);
        }
    }
}