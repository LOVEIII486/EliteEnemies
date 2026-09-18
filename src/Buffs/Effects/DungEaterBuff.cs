using ItemStatsSystem;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>食粪者：让玩家当场吐出来。</summary>
    public sealed class DungEaterBuff : EliteBuffBase
    {
        private const int ShitItemId = 938;

        public override string BuffName => "EliteBuff_DungEater";
        public override int BuffId => 99904;
        public override float Duration => 4f;

        protected override void OnApplied()
        {
            var character = Character;
            if (character == null) return;

            var item = ItemAssetsCollection.InstantiateSync(ShitItemId);
            if (item == null) return;

            character.UseItem(item);

            // ⚠ 这里是硬编码中文——原实现如此，本次重构只搬位置不改行为。
            //   （与本地化体系不一致，若要改另开一次改动。）
            character.PopText("<color=#556B2F>吐了！</color>");
        }
    }
}
