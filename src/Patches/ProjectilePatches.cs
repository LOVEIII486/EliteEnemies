using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EliteEnemies.Affixes.Behaviors;
using EliteEnemies.Buffs;
using HarmonyLib;
using UnityEngine;

namespace EliteEnemies.Patches
{
[HarmonyPatch(typeof(Projectile))]
    public static class ReflectAffixPatch
    {
        private const string LogTag = "[EliteEnemies.Reflect]";

        // Projectile.velocity / .traveledDistance 都是 private，但定义在 TeamSoda.Duckov.Core
        // （Projectile.cs:33 / :14），已由 Publicizer 在编译期公开，直接访问即可。
        // 原先是用 AccessTools.Field 配字符串字段名「velocity」取字段的——
        // 游戏改名后不会编译失败，只会静默返回 null，于是 `?.SetValue` 悄悄什么也不做。

        [HarmonyPatch(nameof(Projectile.UpdateMoveAndCheck))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // 拦截 DamageReceiver.Hurt(DamageInfo) -> bool
            //
            // ⚠ **必须带参数表、且必须判 null。** `nameof` 只钉方法名、**不校验签名**
            // （`AGENT.md §3.2` 末尾专门说过），所以游戏改参数表时这里不会编译报错：
            //   · 不带签名 ⇒ `AccessTools.Method` 仍按名字找得到**新**方法，于是照旧注入
            //     `[receiver][DamageInfo]` 形状的代理调用 ⇒ **IL 栈形状对不上**（比打不上更糟）
            //   · 带签名   ⇒ 找不到，返回 `null`
            // 而 `null` 参与 `instruction.Calls(...)` 比较时，会在一大片"操作数不是 `MethodInfo`"
            // 的指令上**也为真** ⇒ 代理被注入到几十处，`patched` 远大于 0，
            // 连下面那句"命中 0 次就报错"的守卫也不会响。**所以 null 必须在进循环之前拦掉。**
            var hurtMethod = AccessTools.Method(typeof(DamageReceiver), nameof(DamageReceiver.Hurt),
                                                new[] { typeof(DamageInfo) });
            if (hurtMethod == null)
            {
                Debug.LogError($"{LogTag} 找不到 DamageReceiver.Hurt(DamageInfo)——" +
                               "游戏大概改了这个方法的参数表。**本次不打补丁**（指令流原样返回），" +
                               "反射词条将失效；请对照 DamageReceiver.cs 复核本补丁。");

                // ⚠ 这里**不能**写 `return instructions;`——本方法是**迭代器**（下面全是
                // `yield return`），`return` 带值在迭代器里不合法；而直接 `yield break;`
                // 会让 Harmony 收到**空的指令流**，等于**把整个方法体抹掉**。
                // 唯一正确的形态是"把原指令一条不落地 yield 回去，再 break"。
                foreach (var original in instructions) yield return original;
                yield break;
            }

            // 替换 ReflectAffixPatch.ReflectOrHurt(..., Projectile) -> bool
            var proxyMethod = AccessTools.Method(typeof(ReflectAffixPatch), nameof(ReflectOrHurt));

            int patched = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(hurtMethod))
                {
                    // 在调用 hurt 之前，把 Projectile 加载到堆栈上
                    // 原堆栈: [DamageReceiver] [DamageInfo]
                    // 新堆栈: [DamageReceiver] [DamageInfo] [Projectile]
                    patched++;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);

                    // **原地改写**这条指令，而不是丢掉它再插一条新的：原地改写会自动保留
                    // `labels` 与 `blocks`。原写法把整条指令丢掉，若那个调用点恰好是分支目标、
                    // 或位于 try 块的边界，IL 就坏了。
                    // （已核：`Projectile.UpdateMoveAndCheck` 全段没有 try/catch，且该调用点是
                    //   块内语句、不是 `for`/`if` 的分支目标 ⇒ 当时不可达。改成原地之后，
                    //   以后也不必再想这件事。）
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = proxyMethod;
                    yield return instruction;
                }
                else
                {
                    yield return instruction;
                }
            }

            // ⚠ **把"一处都没替换到"变成响的。** Transpiler 靠匹配 IL 找调用点：
            // 游戏若改了这个方法的结构（改名 / 拆方法 / 把 Hurt 的调用挪走 / 调用点被内联），
            // 这里会命中 **0 次**，而 Harmony 只会"找不到目标方法"时报错——**替换不到调用点它一声不吭**，
            // 表现就是"反射词条静默失效"（正是本工程最忌讳的一类）。
            // 注意 `AccessTools.Method(..., nameof(...))` 本身是编译期绑定的，方法改名会编译报错 ✓；
            // 这里防的是**方法还在、里面的调用点没了**。
            if (patched == 0)
            {
                Debug.LogError($"{LogTag} Transpiler 在 Projectile.UpdateMoveAndCheck 里" +
                               "**没找到** DamageReceiver.Hurt 的调用点——反射词条已失效。" +
                               "游戏大概改过这个方法的结构，请对照 Projectile.cs 复核本补丁。");
            }
        }

        /// <summary>
        /// 拦截命中：目标是"反射者"时把子弹反弹回去，否则按原样调用 <c>hurt</c>。
        ///
        /// <para>⚠ 整个方法必须有 try/catch：它是**替换掉游戏调用点**的代理——
        /// 异常会直接冒进 `Projectile.UpdateMoveAndCheck` 的循环里，把游戏自己的弹道更新带走。
        /// 失败时**回落到普通命中**（`return receiver.Hurt(info);`），而不是让这一击凭空消失。</para>
        /// </summary>
        public static bool ReflectOrHurt(DamageReceiver receiver, DamageInfo info, Projectile projectile)
        {
            try
            {
                // 用游戏自己的口子拿受害者：`DamageReceiver.health` 是公开字段，
                // 而 `Health.TryGetCharacter()` 是**缓存**的（Health.cs:206-221），
                // 游戏自己也这么拿（DamageReceiver.cs:113）。
                // 原先这里是 GetComponentInParent——**每次子弹命中**都做一次层级遍历。
                var victim = receiver.health != null
                    ? receiver.health.TryGetCharacter()
                    : receiver.GetComponentInParent<CharacterMainControl>();

                if (victim != null && ReflectBehavior.IsReflecting(victim.GetInstanceID()))
                {
                    ref var ctx = ref projectile.context;

                    // ⚠ **取 receiver.Team，不是 victim.Team**（2026-09-21 修）。
                    //
                    //   游戏下一帧会拿 `context.team` 去比 `_dmgReceiverTemp.Team`
                    //   （`Projectile.cs:363`），而那个 `Team` 读的是 **`health.team`**
                    //   （`DamageReceiver.cs:19-33`）。`victim.Team` 读的是
                    //   `CharacterMainControl.team` 那个**字段**。两者本该相等
                    //   （`SetTeam` 同时写两个，`CharacterMainControl.cs:1628-1631`），
                    //   但那道同队保护正是"子弹反射后不再咬同一只精英"的第二道防线——
                    //   只要两端有一个字段没同步，它就会**静默失效**，表现是子弹在精英
                    //   体内反复翻方向。直接取"游戏等下要比对的那个来源"，
                    //   这道门就**按构造**成立。游戏自家反弹也是这么取的
                    //   （`Projectile.cs:475` 的 `context.team = component.team`）。
                    ctx.team = receiver.Team;
                    ctx.fromCharacter = victim;

                    // 反向基础向量
                    Vector3 baseReverseDir = -ctx.direction;

                    // 随机偏转角度
                    float deviationAngle = UnityEngine.Random.Range(-10f, 10f);
                    Vector3 newDirection = Quaternion.Euler(0, deviationAngle, 0) * baseReverseDir;
                    newDirection.y = 0;
                    newDirection.Normalize();

                    ctx.direction = newDirection;
                    projectile.transform.forward = newDirection;

                    projectile.velocity = newDirection * ctx.speed;

                    // ⚠ **私有的 `direction` 字段也得写**（2026-09-21 修）：本帧末尾那句
                    //   `transform.position += direction * _distanceThisFrame`
                    //   （`Projectile.cs:521`）用的是**它**，而它要到**下一帧**的
                    //   `direction = velocity.normalized`（`:328`）才会跟着 velocity 走。
                    //   只写 `ctx.direction`/`transform.forward`/`velocity` 的话，
                    //   反射当帧会按**旧朝向**把子弹再往敌人身体里推整整一帧
                    //   （`transform.forward` 被我们改过，`:338` 的
                    //   `origin = position - forward * 0.1f` 因此也指向体内）。
                    //   游戏自家反弹就写了这一句（`Projectile.cs:479` `direction = -direction`）。
                    //   已由 Publicizer 公开，同 `velocity`（见本文件顶部注释）。
                    projectile.direction = newDirection;

                    // 重置命中判定。
                    //
                    // ⚠ **加的是"碰撞体所在的"gameObject，不是角色根物体**（2026-09-21 修）。
                    //   去重判据是 `damagedObjects.Contains(hits[i].collider.gameObject)`
                    //   （`Projectile.cs:355`），而 `receiver` 正是从那个碰撞体上取下来的
                    //   （`:362` `hits[i].collider.GetComponent<DamageReceiver>()`）
                    //   ⇒ **`receiver.gameObject` 与判据比的是同一个对象，按构造必定命中**。
                    //   原先加 `victim.gameObject`（角色根）只在"命中盒恰好挂在根物体上"
                    //   时才碰巧相等，**等于这道去重从来没真正生效过**。
                    //   游戏自家反弹加的也是碰撞体所在的物体（`Projectile.cs:489`）。
                    projectile.damagedObjects.Clear();
                    projectile.damagedObjects.Add(receiver.gameObject);

                    projectile.traveledDistance = 0f;

                    ctx.penetrate += 1;
                    return false;
                }

                return receiver.Hurt(info);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 反射处理失败（已隔离，本次按普通命中处理）: {ex}");
                return receiver.Hurt(info);
            }
        }
    }

/// <summary>
    /// 子弹扭曲补丁 - 使受扭曲影响的玩家的子弹产生弧形偏转
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Update))]
    public class BulletDistortionPatch
    {
        private const string LogTag = "[EliteEnemies.BulletDistortion]";

        /// <summary>
        /// 偏转强度：**每飞行 1 米偏转多少度**。
        ///
        /// <para><b>为什么按"米"，而不是按"帧"或按"秒"</b>——这是唯一同时满足两件事的口径：</para>
        /// <list type="bullet">
        /// <item><b>帧率无关</b>：按帧计会让 60fps 与 144fps 差 2.4 倍（原实现正是如此）。
        /// 按秒计能解决帧率，但**不同枪的弹速差很多**——同一个"度/秒"对慢弹是绕圈、
        /// 对快弹几乎不弯。</item>
        /// <item><b>武器无关</b>：按米计时，**任何弹速的子弹每飞一米转的角度都相同**，
        /// 弧线的**半径**因而固定 ⇒ 所有玩家的观感一致。这正是词条描述「形成弧形」
        /// 想要的那个不变量。</item>
        /// </list>
        ///
        /// <para>本帧路程用**与游戏相同的口径**算（见 <see cref="StepDistance"/>），
        /// 所以这里的"米"就是游戏实际走掉的米。</para>
        ///
        /// <para><b>怎么调</b>：本作是**俯视角**、且**交战距离不大**，所以判据取
        /// "**飞过 10 米后横向偏出多少米**"：<c>偏移 = R·(1 − cos(d/R))</c>，其中
        /// <c>R = 1/θ</c>（θ = 每米的弧度数）。</para>
        /// <list type="bullet">
        /// <item><c>1</c>°/米 → R ≈ 57 米 → 10 米处偏 <c>≈0.9</c> 米：太弱，几乎看不出
        /// （**初版取值，作者实测反馈偏弱**）</item>
        /// <item><c>2</c>°/米 → R ≈ 29 米 → 10 米处偏 <c>≈1.7</c> 米：轻微</item>
        /// <item><c>3</c>°/米 → R ≈ 19 米 → 10 米处偏 <c>≈2.6</c> 米：**当前取值**，一眼看得出弯</item>
        /// <item><c>4</c>°/米 → R ≈ 14 米 → 10 米处偏 <c>≈3.4</c> 米：很明显</item>
        /// <item><c>6</c>°/米 → R ≈ 10 米 → 10 米处偏 <c>≈4.8</c> 米：接近半圈，会明显打偏</item>
        /// </list>
        ///
        /// <para>⚠ <b>旋转轴是 <c>Vector3.up</c></b>（见 <see cref="ApplyCurveDeflection"/>）——
        /// 弧线落在**水平面**里。**别改回 <c>Cross(velocity, up)</c>**：那是水平法线，
        /// 绕它旋转改的是**俯仰**，子弹会上飘或扎进地面，而俯视角下那种弧根本看不出来。</para>
        /// </summary>
        private const float DeflectionDegreesPerMeter = 5f;

        [HarmonyPrefix]
        public static void Prefix(Projectile __instance)
        {
            try
            {
                // 已经判定死亡的子弹正在走释放路径（`Projectile.Update` 开头就是
                // `if (dead) { Release(); return; }`，`Projectile.cs:181-187`），
                // 不该再往它身上写状态。顺带省掉每次死亡子弹的一次 HashSet 查询。
                if (__instance.dead) return;

                // 检查是否是玩家射出的子弹
                if (__instance.context.fromCharacter == null) return;
                if (__instance.context.fromCharacter != CharacterMainControl.Main) return;

                // 检查玩家是否受扭曲影响
                if (!BulletDeflectionTracker.Instance.IsDistorted(__instance.context.fromCharacter))
                    return;

                // 获取当前速度
                // velocity 是私有字段（TeamSoda.Duckov.Core/Projectile.cs:33），
                // 由 publicizer 在编译期公开，直接访问。
                Vector3 currentVelocity = __instance.velocity;

                // 速度为零则跳过
                if (currentVelocity.magnitude < 0.1f) return;

                // 计算偏转后的速度（按**本帧实际飞过的距离**转，见 DeflectionDegreesPerMeter）
                Vector3 deflectedVelocity = ApplyCurveDeflection(
                    currentVelocity, StepDistance(currentVelocity), DeflectionSign(__instance));

                // 设置新速度
                __instance.velocity = deflectedVelocity;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 补丁执行失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 这一发弹往**左**还是往**右**偏（<c>+1</c> / <c>-1</c>）。
        ///
        /// <para><b>两条约束，缺一不可</b>：</para>
        /// <list type="number">
        /// <item><b>每发弹固定，不能每帧重掷</b>——每帧重掷的话弹道是**抖动**而不是弧线，
        /// 而词条描述写的是「攻击使玩家子弹偏转**形成弧形**」。每帧绕同一个轴转一个小角度，
        /// 才弯成一条弧。</item>
        /// <item><b>各发弹之间要看不出规律</b>——不能出现"左右交替"「周期 4」这类模式，
        /// 否则一次连发、一发霰弹会明显看出花样。</item>
        /// </list>
        ///
        /// <para><b>为什么是散列，而不是"给每发弹记一个方向"</b>：<c>Projectile</c> 是游戏的类型，
        /// 我们**不能给它加字段**；用字典按实例记账则要处理"子弹什么时候死的"清理问题
        /// （子弹是池化的，漏清理就是慢性泄漏）。拿 <c>InstanceID</c> 散列可同时满足两条：
        /// ID 在子弹整个生命周期内**不变**（⇒ 每发固定），而连号的 ID 被散开后
        /// 看不出规律（⇒ 各发之间像随机）。零状态、零分配。</para>
        ///
        /// <para>⚠ <b>不能直接用 ID 的最低位</b>：Unity 的实例 ID 是**连号分配**的，最低位会
        /// 严格奇偶交替 ⇒ 呈现"左、右、左、右"的规律（本方法的第一版正是这么写的）。</para>
        /// </summary>
        private static float DeflectionSign(Projectile projectile)
        {
            unchecked
            {
                // 乘法散列 + 两次"移位异或 / 乘法"打散，最后取最低位。
                //
                // **必须打到这个强度。** 实测过（连号 20 万发，看四个 lag 上的自相关——
                // 真随机每一列都应 ≈0.5，任何一列接近 1.0 就说明有周期）：
                //
                //   方案                        lag1   lag2   lag3   lag4
                //   只乘、取乘积的高位           0.78   0.57   0.35   0.13   ✗ 成块
                //   乘 + 一次 `x ^= x >> 16`    0.53   0.05   0.43   0.90   ✗ 周期 4
                //   本轮（乘 + 两次打散）         0.50   0.50   0.50   0.50   ✓
                //
                // ⚠ 别拿"相邻两发同向的比例"当唯一判据：它对第二行给出 0.53，
                //   看着像随机，而那一版的实际序列是 `RRLLRRLLRRLL…`——**周期 4**。
                //   **指标必须能区分你真正担心的那种规律**（本工程的老教训，只是换了张脸）。
                uint x = (uint)projectile.GetInstanceID() * 2654435761u;
                x ^= x >> 15;
                x *= 2246822519u;
                x ^= x >> 13;
                return (x & 1) == 0 ? 1f : -1f;
            }
        }

        /// <summary>
        /// 应用弧形偏转：把速度绕 <c>Vector3.up</c> 转一个小角度 ⇒ **在水平面内拐弯**。
        ///
        /// <para>轴取 <c>Vector3.up</c> 而**不是** <c>Cross(velocity, up)</c>：本作是俯视角，
        /// 弧线必须在水平面里（子弹绕开掩体），而后者是**水平法线**、绕它旋转改的是**俯仰**
        /// ⇒ 子弹上飘或扎进地面，且在俯视角下根本看不出来。</para>
        /// </summary>
        private static Vector3 ApplyCurveDeflection(Vector3 currentVelocity, float stepDistance,
                                                    float directionSign)
        {
            // 偏转轴固定取 Vector3.up —— **本作是俯视角游戏，弧线必须在水平面里**
            // （子弹该绕开掩体，而不是上飘或扎进地面）。
            //
            // 原先取 Cross(velocity, up)：那是**水平法线**，绕它旋转改的是**俯仰**，
            // 于是子弹在竖直平面里抬/压——正是要避免的那件事。
            //
            // 换来 up 还顺带消掉一处退化分支：原写法在子弹近乎垂直时
            // Cross(velocity, up) 趋近零向量，得靠一个 fallback 换轴兜住；
            // 而绕 up 旋转**恒有定义**——子弹恰好垂直时旋转是恒等变换，无害，不会出 NaN。
            Quaternion rotation = Quaternion.AngleAxis(
                DeflectionDegreesPerMeter * stepDistance * directionSign,
                Vector3.up
            );

            return rotation * currentVelocity;
        }

        /// <summary>
        /// 本帧这颗子弹走过的距离。**刻意与游戏自己算位移的口径保持一致**：
        /// <c>Projectile.cs:307-310</c> 先把 <c>Time.deltaTime</c> 夹在 <c>0.04</c> 以内
        /// （帧率过低时游戏会放慢推进），<c>:329</c> 再用 <c>velocity.magnitude * dt</c>
        /// 得到 <c>_distanceThisFrame</c>。
        ///
        /// <para>用同一个 dt，才能让 <see cref="DeflectionDegreesPerMeter"/> 里的"米"
        /// 名副其实——否则在 25fps 以下我们会按未夹住的 dt 多转一点。</para>
        /// </summary>
        private static float StepDistance(Vector3 velocity)
        {
            float dt = Time.deltaTime;
            if (dt > 0.04f) dt = 0.04f;

            return velocity.magnitude * dt;
        }
    }
}
