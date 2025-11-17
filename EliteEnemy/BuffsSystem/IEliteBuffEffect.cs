namespace EliteEnemies.BuffsSystem
{
    /// <summary>
    /// 精英Buff效果接口
    /// </summary>
    public interface IEliteBuffEffect
    {
        string BuffName { get; }

        void OnBuffSetup(Duckov.Buffs.Buff buff, CharacterMainControl player);

        void OnBuffDestroy(Duckov.Buffs.Buff buff, CharacterMainControl player);
    }
}