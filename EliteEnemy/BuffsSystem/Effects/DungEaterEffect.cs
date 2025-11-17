using Duckov.Buffs;
using FMOD;
using ItemStatsSystem;
using Debug = UnityEngine.Debug;

namespace EliteEnemies.BuffsSystem.Effects
{
    public class DungEaterEffect : IEliteBuffEffect
    {
        public string BuffName => "EliteBuff_DungEater";
        private static readonly int ShitItemId = 938; // 粑粑id

        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            var item = ItemAssetsCollection.InstantiateSync(ShitItemId);
            if (item != null)
            {
                player.UseItem(item);
                player.PopText("吐了！");
            }
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player) { }
    }
}