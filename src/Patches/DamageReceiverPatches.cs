using System;
using EliteEnemies.Affixes;
using EliteEnemies.Core;
using HarmonyLib;
using UnityEngine;

namespace EliteEnemies.Patches
{
    /// <summary>
    /// 玩家受伤检测逻辑 (用于触发词缀的战斗逻辑)
    ///
    /// <para>⚠ 补丁体内**必须**有 try/catch：这是**每次受击**都会走的路径，
    /// 抛出的异常会顺着游戏自己的伤害链往上冒（`DamageReceiver.Hurt` → 弹道/近战/死亡处理），
    /// 把一个本模组的 bug 变成"游戏行为被中断"。补丁层的原则与行为回调层一致：
    /// **只记录，不重抛**。</para>
    /// </summary>
    [HarmonyPatch(typeof(DamageReceiver), nameof(DamageReceiver.Hurt))]
    public static class PlayerHitDetectionPatch
    {
        private const string LogTag = "[EliteEnemies.PlayerHitDetection]";

        static void Postfix(DamageReceiver __instance, DamageInfo damageInfo, bool __result)
        {
            try
            {
                // 0. `Hurt` 返回 false = **这一击游戏根本没结算**——玩家已死（`DamageReceiver.cs:78-81`）、
                //    坐在受保护的载具里（`:86-93`）、或 `LevelManager` 未就绪（`:82-85`）。
                //    那就不算"打中玩家"，不该触发任何 `OnHitPlayer` 效果
                //    （否则会对尸体上致盲/眩晕/击退，或在载具里给玩家上 debuff）。
                //
                //    ⚠ 它**挡不住**"被无敌帧吃掉的伤害"：`DamageReceiver.Hurt` 在正常路径上
                //    **无条件返回 true**（`DamageReceiver.cs:100`），不管 `Health.Hurt` 内部是否
                //    因 `invincible` 早退（`Health.cs:314`）。要连那一层一起挡，只能改订阅
                //    `Health.OnHurt`——在 Postfix 里读 `damageInfo.finalDamage` 是拿不到的
                //    （`DamageInfo` 是结构体、被逐层按值传递，结算值写在 `Health.Hurt` 的局部副本上）。
                if (!__result) return;

                // 1. 先判"挨打的是不是玩家"——**最便宜的判断放最前面**。
                //    用游戏自己的口子：`DamageReceiver.health` 是公开字段，
                //    而 `Health.TryGetCharacter()` 是**缓存**的（`Health.cs:206-221`，
                //    首次解析后直接返回缓存），游戏自己也这么拿角色（`DamageReceiver.cs:113`）。
                //    原先这里是 `__instance.GetComponentInParent<CharacterMainControl>()`——
                //    每次受击都做一次**层级遍历**。
                CharacterMainControl receiver = __instance.health != null
                    ? __instance.health.TryGetCharacter()
                    : null;
                if (receiver == null) return;

                // 1b. **必须是"玩家"，但不必是"本机玩家"。**
                //
                //     ⚠ 原先这里只认 `IsMainCharacter`，于是联机下**整个补丁都不触发**：
                //     主机上挨打的是**客机玩家的复制体**，它不满足 `IsMainCharacter`；
                //     而客机侧的"攻击者"是没有行为组件的复制体（在第 4 步被挡掉）。
                //     结果是**所有 debuff 类词条在联机下静默失效**（实测确认）。
                //
                //     "远端玩家"这个判定由联机模块注入——单机下它恒为假，
                //     所以**单机的行为与从前一字不差**。
                if (!receiver.IsMainCharacter && !EliteEnemyCore.IsRemotePlayerCharacter(receiver)) return;

                // 2. 必须是由角色造成的伤害
                CharacterMainControl attacker = damageInfo.fromCharacter;
                if (attacker == null) return;

                // 3. 同队伤害不算
                if (attacker.Team == receiver.Team) return;

                // 4. 攻击者必须是有行为组件的精英（本模组的目标）
                var behaviorComponent = attacker.GetComponent<EliteBehaviorComponent>();
                if (behaviorComponent == null) return;

                // `receiver` 就是**挨打的那个玩家**——把它一起传下去，
                // debuff 才能挂对人（联机下挨打的多半不是本机玩家）。
                behaviorComponent.TriggerHitPlayer(attacker, receiver, damageInfo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 补丁执行失败（已隔离，不影响游戏伤害链）: {ex}");
            }
        }
    }
}
