using Duckov.Buffs;
using ItemStatsSystem.Stats;

namespace EliteEnemies.Buffs.Effects
{
    public class SlowEffect : IEliteBuffEffect
    {
        public string BuffName => "EliteBuff_Slow";
        private static readonly float WalkSpeedReduction = -0.5f;
        private static readonly float RunSpeedReduction = -0.5f;

        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            int buffId = buff.GetInstanceID();
            
            var walkStat = player.CharacterItem.GetStat("WalkSpeed");
            var walkMod = new Modifier(ModifierType.PercentageMultiply, WalkSpeedReduction, player);
            walkStat.AddModifier(walkMod);
            
            var runStat = player.CharacterItem.GetStat("RunSpeed");
            var runMod = new Modifier(ModifierType.PercentageMultiply, RunSpeedReduction, player);
            runStat.AddModifier(runMod);

            EliteBuffModifierManager.Instance.TrackModifier(buffId, walkStat, walkMod);
            EliteBuffModifierManager.Instance.TrackModifier(buffId, runStat, runMod);
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player)
        {
            EliteBuffModifierManager.Instance.CleanupModifiers(buff.GetInstanceID());
        }
    }
}