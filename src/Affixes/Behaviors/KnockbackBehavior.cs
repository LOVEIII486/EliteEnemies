using System;
using System.Collections.Generic;
using ECM2;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    public class KnockbackBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Knockback";

        private static readonly float KnockbackCooldown = 5f; // cd

        /// <summary>
        /// 每个玩家各自的"上次被击退时刻"，**按受害者**记。
        ///
        /// <para>⚠ 原先是一个 <c>static float</c>：**所有精英、所有玩家共用同一个 5 秒冷却**。
        /// 单机看不出问题（只有一个玩家），联机下两个玩家会**互相占掉对方的冷却**——
        /// 客机 A 被击退之后，客机 B 在 5 秒内不会被击退，反之亦然。
        /// 同一类"跨玩家串味"本工程刚在 <c>FrozenBehavior</c> 上修过（提交 <c>9549fb1</c>）。</para>
        ///
        /// <para><b>改成按受害者记，单机行为逐帧不变</b>：单机只有一个玩家，
        /// 所有精英共用同一个键 ⇒ 与原来的全局冷却**完全等价**。</para>
        ///
        /// <para>键用 <c>GetInstanceID()</c> 而不是角色引用——本工程不做"以 Unity 对象为键的静态字典"
        /// （那种表不会随对象销毁收缩，见 <c>04-架构模式</c> §10.4）。这里是 int 键，条目由
        /// <see cref="PruneStale"/> 定期清，上界是"当前在场玩家数"。</para>
        /// </summary>
        private static readonly Dictionary<int, float> _lastKnockbackByVictim =
            new Dictionary<int, float>();

        /// <summary>超过这个规模就清一次失效条目（正常情况下远达不到——玩家数 ≤ 16）。</summary>
        private const int PruneThreshold = 32;

        /// <summary>这个玩家还在冷却里吗（没记录过 ⇒ 不在）。</summary>
        private static bool IsOnCooldown(CharacterMainControl victim)
            => _lastKnockbackByVictim.TryGetValue(victim.GetInstanceID(), out float last)
               && Time.time - last < KnockbackCooldown;

        /// <summary>
        /// 记下"这个玩家刚被击退过"，并顺手清掉已失效的条目。
        ///
        /// <para>**为什么要清**：超过冷却的时间戳在判断里**等同不存在**，留着只是让这张表
        /// 随"玩家进进出出"无限增长（每次重连都是一个新的实例 ID）。</para>
        /// </summary>
        private static void MarkKnockback(CharacterMainControl victim)
        {
            _lastKnockbackByVictim[victim.GetInstanceID()] = Time.time;

            if (_lastKnockbackByVictim.Count <= PruneThreshold) return;

            // ⚠ **先取键快照再删**——边遍历边改会抛 InvalidOperationException
            // （与 `CoopPlayers.PruneDestroyed` 是同一个坑，那里是倒着遍历）。
            var stale = new List<int>();
            foreach (var pair in _lastKnockbackByVictim)
            {
                if (Time.time - pair.Value >= KnockbackCooldown) stale.Add(pair.Key);
            }

            for (int i = 0; i < stale.Count; i++) _lastKnockbackByVictim.Remove(stale[i]);
        }
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            // ⚠ 目标必须是 **victim（被打中的那个玩家）**，不是 `CharacterMainControl.Main`。
            //   联机下判定在主机上跑，而挨打的往往是**客机玩家的复制体**；
            //   写死 Main 会把效果挂到主机自己的玩家身上，客机什么都看不到。
            var player = victim;
            if (player == null)
            {
                return;
            }

            // ⚠ 冷却必须**先拿到 player 再判**——它是按受害者记的
            //   （原先放在最前面判一个全局时间戳，那正是"跨玩家串味"的写法）。
            if (IsOnCooldown(player))
            {
                return;
            }

            // ⚠ 方向与距离倍率只有主机算得出来（要用精英与玩家的位置），
            //   所以先在这里算好，再决定是本地做还是交给对方那台机器做。
            Vector3 horizontal = player.transform.position - attacker.transform.position;
            horizontal.y = 0f;

            float distance = horizontal.magnitude;
            float distanceMultiplier = Mathf.Clamp(1.5f / Mathf.Max(distance, 1f), 0.9f, 2f);
            Vector3 scaledDirection = (distance > 0.0001f ? horizontal / distance : Vector3.zero)
                                      * distanceMultiplier;

            if (PlayerEffectRelay.TryRelay(player, PlayerEffectRelay.Kind.Knockback, scaledDirection))
            {
                PlayerEffectRelay.PopTextOnElite(attacker, "EliteEnemies_Affix_Knockback_PopText_1", "<color=#FF4500>装逼我让你飞起来！</color>");
                MarkKnockback(player);
                return;
            }

            PlayerEffectActions.Knockback(player, scaledDirection);

            PlayerEffectRelay.PopTextOnElite(attacker, "EliteEnemies_Affix_Knockback_PopText_1", "<color=#FF4500>装逼我让你飞起来！</color>");
            MarkKnockback(player);
        }

    }
}