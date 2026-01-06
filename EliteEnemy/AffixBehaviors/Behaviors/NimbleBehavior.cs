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
            Modify(character, AIFieldModifier.Fields.CanDash, 1.0f, false);
            Modify(character, AIFieldModifier.Fields.DashCDMin, 0.2f, true);
            Modify(character, AIFieldModifier.Fields.DashCDMax, 0.2f, true);
            Modify(character, AIFieldModifier.Fields.BaseReactionTime, 0.5f, true);
            Modify(character, AIFieldModifier.Fields.ShootCanMove, 1.0f, false);
            Modify(character, AIFieldModifier.Fields.CombatTurnSpeed, 2.5f, true);
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);
        }
    }
}