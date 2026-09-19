using System.Collections.Generic;
using EliteEnemies.Core;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 记住**在场的远端玩家角色**，供本模组回答两类问题：
    /// 「这一击是不是打在某个玩家身上」与「离这只怪最近的玩家是谁」。
    ///
    /// <para><b>为什么需要</b>：词条的判定在**主机**上跑，而主机上挨打的、或者站在旁边的
    /// 常常是<b>客机玩家的复制体</b>——它不是 <c>CharacterMainControl.Main</c>（那是**本机**玩家）。
    /// 写死 <c>Main</c> 的代码在单机下正确、在联机下会静默作用到错误的人（或干脆不触发）。
    /// 详见 <c>Docs\Coop\05-integration-gotchas.md</c> §11。</para>
    ///
    /// <para><b>怎么知道谁是远端玩家</b>：不猜队伍、不去翻复制体身上的组件名，
    /// 而是**订阅联机模组自己的玩家进场事件** <c>ModApiEvents.PlayerSpawned</c>
    /// （签名带 <c>isLocal</c>）——由它来告诉我们。</para>
    ///
    /// <para><b>为什么是 <c>List</c> 而不是 <c>HashSet</c></b>：
    /// <see cref="EliteEnemyCore.FindNearestPlayer"/> 会被 <c>OnUpdate</c> **每帧**调用，
    /// 需要能**零分配**地按下标遍历。用 <c>HashSet</c> 就得装枚举器。
    /// 而"是不是远端玩家"那个判定退化成线性查也无妨——玩家数 ≤ 16，
    /// 且它只在**每次受击**时问一次，不在热路径上。
    /// <b>刻意只维护一个集合</b>：两份必然漂移。</para>
    ///
    /// <para>单机下这个列表**始终为空** ⇒ 两个判定都退化成"只有本机玩家"
    /// ⇒ **单机行为与从前一字不差**。</para>
    /// </summary>
    internal static class CoopPlayers
    {
        /// <summary>超过这个规模就顺手清理一次已销毁的条目（正常情况下远达不到）。</summary>
        private const int PruneThreshold = 32;

        private static readonly List<CharacterMainControl> s_remote = new List<CharacterMainControl>();

        public static void Initialize()
        {
            // 把**同一个列表实例**交给 Core 只读遍历（不是拷贝）——
            // 这样后续的增删它都能立刻看到，且热路径零分配。
            EliteEnemyCore.RemotePlayers = s_remote;
            EliteEnemyCore.RemotePlayerPredicate = IsRemote;
        }

        public static void Shutdown()
        {
            EliteEnemyCore.RemotePlayers = null;
            EliteEnemyCore.RemotePlayerPredicate = null;
            s_remote.Clear();
        }

        /// <summary>由 <c>CoopApi</c> 转发联机模组的 <c>PlayerSpawned</c> 事件。</summary>
        public static void OnPlayerSpawned(CharacterMainControl cmc, string playerId, bool isLocal)
        {
            if (!cmc) return;

            if (isLocal)
            {
                // 本机玩家不该在这个列表里（`Main` 那条路已经覆盖它）。
                s_remote.Remove(cmc);
                return;
            }

            if (!s_remote.Contains(cmc))
            {
                s_remote.Add(cmc);
                CoopLog.Info($"[玩家] 远端玩家进场 playerId={playerId} name={cmc.name}" +
                             $"（在场远端玩家 {s_remote.Count} 个）");
            }

            if (s_remote.Count > PruneThreshold) PruneDestroyed();
        }

        /// <summary>
        /// 已销毁的角色会在列表里留成"假 null"的条目。它们不影响正确性，
        /// 但会让列表只增不减，所以顺手清一次。
        ///
        /// <para>⚠ 这里**倒着遍历**再 <c>RemoveAt</c>：正着删会跳过元素。</para>
        /// </summary>
        private static void PruneDestroyed()
        {
            int removed = 0;
            for (int i = s_remote.Count - 1; i >= 0; i--)
            {
                if (!s_remote[i])   // Unity 的 == null 对已销毁对象为 true
                {
                    s_remote.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
                CoopLog.Info($"[玩家] 清理了 {removed} 个已销毁的远端玩家条目（剩余 {s_remote.Count}）");
        }

        private static bool IsRemote(CharacterMainControl cmc) => s_remote.Contains(cmc);
    }
}
