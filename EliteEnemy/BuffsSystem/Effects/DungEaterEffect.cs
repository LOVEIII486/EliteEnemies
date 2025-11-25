using Duckov.Buffs;
using ItemStatsSystem;

namespace EliteEnemies.EliteEnemy.BuffsSystem.Effects
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
                player.PopText("<color=#556B2F>吐了！</color>");
            }
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player) { }
    }
}