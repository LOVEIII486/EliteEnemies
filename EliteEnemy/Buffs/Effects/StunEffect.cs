using Duckov.Buffs;
using ItemStatsSystem.Stats;

namespace EliteEnemies.Buffs.Effects
{
    public class StunEffect : IEliteBuffEffect
    {
        public string BuffName => "EliteBuff_Stun";
        private static readonly float RecoilControlReduction = -0.7f; // 后坐力控制
        // private static readonly float BulletSpeedReduction = -0.8f; // 子弹速度

        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            int buffId = buff.GetInstanceID();
    
            // 修改后坐力控制
            var recoilControlStat = player.CharacterItem.GetStat("RecoilControl");
            var recoilControlMod = new Modifier(ModifierType.PercentageMultiply, RecoilControlReduction, player);
            recoilControlStat.AddModifier(recoilControlMod);
            EliteBuffModifierManager.Instance.TrackModifier(buffId, recoilControlStat, recoilControlMod);
    
            // 修改子弹速度
            // var bulletSpeedStat = player.CharacterItem.GetStat("BulletSpeedMultiplier");
            // var bulletSpeedMod = new Modifier(ModifierType.PercentageMultiply, BulletSpeedReduction, player);
            // bulletSpeedStat.AddModifier(bulletSpeedMod);
            // EliteBuffModifierManager.Instance.TrackModifier(buffId, bulletSpeedStat, bulletSpeedMod);
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player)
        {
            EliteBuffModifierManager.Instance.CleanupModifiers(buff.GetInstanceID());
        }
    }
}