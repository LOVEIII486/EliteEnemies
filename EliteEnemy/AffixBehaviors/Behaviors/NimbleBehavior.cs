using UnityEngine;
using EliteEnemies.EliteEnemy.AttributeModifier;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 灵动词缀：敌人极其灵活
    /// </summary>
    public class NimbleBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Nimble";

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.CanDash, 1.0f);
            AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.DashCDMin, 0.2f, true);
            AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.DashCDMax, 0.2f, true);

            AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.BaseReactionTime, 0.5f, true);

            AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.ShootCanMove, 1.0f);

            AIFieldModifier.ModifyDelayed(character, AIFieldModifier.Fields.CombatTurnSpeed, 2.5f, true);
        }
        
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
} 