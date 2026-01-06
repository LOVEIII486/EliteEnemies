using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;

namespace EliteEnemies.EliteEnemy.BuffsSystem.Effects
{
    public class StunEffect : IEliteBuffEffect
    {
        public string BuffName => "EliteBuff_Stun";
        private static readonly float RecoilControlReduction = -0.7f; // 降低 70% 后坐力控制

        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null) return;

            EliteBuffModifierManager.Instance.ApplyAndTrack(
                player, 
                buff, 
                StatModifier.Attributes.RecoilControl, 
                RecoilControlReduction
            );
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player)
        {
            EliteBuffModifierManager.Instance.CleanupModifiers(buff.GetInstanceID());
        }
    }
}