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
                // 【判据总览】三道门，顺序与理由见各自注释：
                //   1.  挨打的得是个角色
                //   1b. 挨打的得是**玩家**（本机或远端）
                //   1c. `__result` 这道"游戏是否真的结算了"的检查——**只对本机玩家用**，
                //       远端玩家不看（联机模组会拦下那种伤害，`__result` 因此不可信）
                //
                // ⚠ 1c 那道门**挡不住**"被无敌帧吃掉的伤害"：`DamageReceiver.Hurt` 在正常路径上
                //    **无条件返回 true**（`DamageReceiver.cs:100`），不管 `Health.Hurt` 内部是否
                //    因 `invincible` 早退（`Health.cs:314`）。要连那一层一起挡，只能改订阅
                //    `Health.OnHurt`——在 Postfix 里读 `damageInfo.finalDamage` 是拿不到的
                //    （`DamageInfo` 是结构体、被逐层按值传递，结算值写在 `Health.Hurt` 的局部副本上）。

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
                bool isRemotePlayer = EliteEnemyCore.IsRemotePlayerCharacter(receiver);
                if (!receiver.IsMainCharacter && !isRemotePlayer) return;

                // 1c. **`__result` 只对"本机玩家"可信，对远端玩家不可信。**
                //
                //     上面那句 `if (!__result) return;` 的意思是"Hurt 返回 false = 这一击
                //     游戏根本没结算"。但联机模组在**主机**上会用**更高优先级**的 Prefix
                //     拦下"打中远端玩家复制体"的伤害、把它转发给真正的客机，然后 `return false`
                //     （`EscapeFromDuckovCoopMod/Patch/../Main/HarmonyFix.cs` 的
                //     `Patch_ServerForwardRemotePlayerDamage`）。
                //
                //     Harmony 的规则是：**Prefix 返回 false 时原方法不跑，但 Postfix 照跑**，
                //     此时 `__result` 是**默认值 `false`**。于是主机上每一发打在客机玩家身上的
                //     伤害，都会被上面那句当成"没结算"挡掉——**这正是 debuff 词条在联机下
                //     不生效的最后一道拦路虎**（实测确认：主机的本机玩家能中 debuff，
                //     客机不能）。
                //
                //     所以：本机玩家仍按老规矩看 `__result`（单机行为一字不差）；
                //     远端玩家不看它——那一击是**真的打中了玩家**，该触发。
                if (!__result && !isRemotePlayer) return;

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

    /// <summary>
    /// 给 <see cref="EliteBehaviorComponent"/> 的"去重标志"划定**生命周期**。
    ///
    /// <para><b>为什么需要它</b>：那个标志（<c>_handledByReceiver</c>）的作用是
    /// "既然 <c>DamageReceiver.Hurt</c> 已经派发过一遍，紧随其后的那遍
    /// <c>Health.OnHurtEvent</c> 就跳过"。它成立的前提是**两个事件在同一次同步调用链里
    /// 先后到达**——而 <c>DamageReceiver.Hurt</c> 里那句 <c>health.Hurt</c>
    /// 可能**提前返回**（<c>invincible</c> / <c>isDead</c> / <c>IsLoading</c>，
    /// <c>Health.cs:308-320</c>），那时 <c>Health.OnHurtEvent</c> 根本不会发
    /// ⇒ <b>标志没人消费、就留在 true 上</b>。</para>
    ///
    /// <para>后果：**下一次程序化结算**被静默吞掉一次 <c>OnDamaged</c> 派发。
    /// 而"程序化结算"正是联机模组在主机上结算客机上报伤害的那条路
    /// （<c>health.Hurt</c> 直调，见 <c>EliteBehaviorComponent</c> 的注释）
    /// ⇒ 这是一个**只有联机才存在**、且**一个字日志都没有**的行为差异。
    /// 可复现的组合：精英正处于无敌（守卫护盾 / 不死 2.5 秒）+ 主机玩家又打了一下
    /// + 客机紧接着打。</para>
    ///
    /// <para><b>为什么挂在 Postfix 上是对的</b>：Postfix **保证**跑在整条链之后——
    /// 包括里面那句 <c>health.Hurt</c>，无论它是正常返回还是提前返回。
    /// 于是标志的生命周期被精确地压回"这一次 <c>DamageReceiver.Hurt</c> 调用"，
    /// 与该标志设计时假设的"同一次同步调用链"完全吻合。
    /// （换 <c>Update</c> 里清也能收窄，但窗口还留着"本帧剩余时间"，不够准。）</para>
    /// </summary>
    [HarmonyPatch(typeof(DamageReceiver), nameof(DamageReceiver.Hurt))]
    internal static class EliteHitDedupeResetPatch
    {
        private const string LogTag = "[EliteEnemies.DedupeReset]";

        // ⚠ 补丁体必须有 try/catch：这是**每次受击**都会走的路径，
        //    异常会顺着游戏自己的伤害链往上冒（与上面那个补丁同一条纪律）。
        static void Postfix(DamageReceiver __instance)
        {
            try
            {
                // 用游戏自己的口子拿角色：`health` 是公开字段，
                // `TryGetCharacter()` 是**缓存**的（同上面那个补丁的取法）。
                var character = __instance != null && __instance.health != null
                    ? __instance.health.TryGetCharacter()
                    : null;
                if (character == null) return;

                var behaviors = character.GetComponent<EliteBehaviorComponent>();
                if (behaviors != null) behaviors.ClearReceiverHandledFlag();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 补丁执行失败（已隔离，不影响游戏伤害链）: {ex}");
            }
        }
    }
}
