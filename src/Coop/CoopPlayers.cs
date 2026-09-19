using System.Collections.Generic;
using EliteEnemies.Core;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 记住**在场的远端玩家角色**，供本模组判断"这一击是不是打在某个玩家身上"。
    ///
    /// <para><b>为什么需要</b>：词条的"打中玩家"判定原本只认 <c>IsMainCharacter</c>
    /// （<b>本机</b>玩家）。联机下判定在**主机**上跑，而主机上挨打的是
    /// <b>客机玩家的复制体</b>——它不满足那个条件，于是**所有 debuff 词条在联机下静默失效**
    /// （实测确认）。要修就得能回答"这是玩家，只是不是我"。</para>
    ///
    /// <para><b>怎么知道谁是远端玩家</b>：不猜队伍、不去翻复制体身上的组件名，
    /// 而是**订阅联机模组自己的玩家进场事件** <c>ModApiEvents.PlayerSpawned</c>
    /// （签名带 <c>isLocal</c>）——由它来告诉我们。</para>
    ///
    /// <para>单机下这个集合永远是空的 ⇒ 判定恒为假 ⇒ **单机行为与从前一字不差**。</para>
    /// </summary>
    internal static class CoopPlayers
    {
        /// <summary>超过这个规模就顺手清理一次已销毁的条目（正常情况下远达不到）。</summary>
        private const int PruneThreshold = 32;

        private static readonly HashSet<CharacterMainControl> s_remote = new HashSet<CharacterMainControl>();

        public static void Initialize()
        {
            EliteEnemyCore.RemotePlayerPredicate = IsRemote;
        }

        public static void Shutdown()
        {
            EliteEnemyCore.RemotePlayerPredicate = null;
            s_remote.Clear();
        }

        /// <summary>由 <c>CoopApi</c> 转发联机模组的 <c>PlayerSpawned</c> 事件。</summary>
        public static void OnPlayerSpawned(CharacterMainControl cmc, string playerId, bool isLocal)
        {
            if (!cmc) return;

            if (isLocal)
            {
                // 本机玩家不该在这个集合里（`IsMainCharacter` 那条路已经覆盖它）。
                s_remote.Remove(cmc);
                return;
            }

            if (s_remote.Add(cmc))
            {
                CoopLog.Info($"[玩家] 远端玩家进场 playerId={playerId} name={cmc.name}" +
                             $"（在场远端玩家 {s_remote.Count} 个）");
            }

            if (s_remote.Count > PruneThreshold) PruneDestroyed();
        }

        /// <summary>
        /// 已销毁的角色会在集合里留成"假 null"的条目。它们不影响正确性
        /// （查的是引用相等），但会让集合只增不减，所以顺手清一次。
        /// </summary>
        private static void PruneDestroyed()
        {
            int before = s_remote.Count;
            s_remote.RemoveWhere(c => !c);   // Unity 的 == null 对已销毁对象为 true
            if (before != s_remote.Count)
                CoopLog.Info($"[玩家] 清理了 {before - s_remote.Count} 个已销毁的远端玩家条目");
        }

        private static bool IsRemote(CharacterMainControl cmc) => s_remote.Contains(cmc);
    }
}
