using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;

namespace EliteEnemies.EliteEnemy.BuffsSystem.Effects
{
    public class SlowEffect : IEliteBuffEffect
    {
        public string BuffName => "EliteBuff_Slow";
        private static readonly float SpeedReduction = -0.5f; // 降低 50% 速度

        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null) return;

            var manager = EliteBuffModifierManager.Instance;

            manager.ApplyAndTrack(player, buff, StatModifier.Attributes.WalkSpeed, SpeedReduction);
            manager.ApplyAndTrack(player, buff, StatModifier.Attributes.RunSpeed, SpeedReduction);
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player)
        {
            EliteBuffModifierManager.Instance.CleanupModifiers(buff.GetInstanceID());
        }
    }
}