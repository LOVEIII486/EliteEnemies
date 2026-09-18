using EliteEnemies.Modifiers;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 灵动词缀：敌人极其灵活
    /// </summary>
    public class NimbleBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Nimble";

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            ModifyAI(character, AIFields.CanDash, true);
            ModifyAI(character, AIFields.DashCDMin, 0.2f);
            ModifyAI(character, AIFields.DashCDMax, 0.2f);
            ModifyAI(character, AIFields.BaseReactionTime, 0.5f);
            ModifyAI(character, AIFields.ShootCanMove, true);
            ModifyAI(character, AIFields.CombatTurnSpeed, 2.5f);
        }



        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);
        }
    }
}