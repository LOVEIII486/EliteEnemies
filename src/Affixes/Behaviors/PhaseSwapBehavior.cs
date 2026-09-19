using System;
using System.Collections;
using EliteEnemies.DebugTools;
using EliteEnemies.Localization;
using UnityEngine;
// 需要引入这个命名空间来使用协程

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 词缀：相位（Phase）
    /// 效果：命中玩家时，平滑地与玩家交换位置。
    ///
    /// <para><b>⚠ 两个标志是 static（全局），而协程挂在**敌人**身上（<see cref="OnHitPlayer"/> 的
    /// <c>attacker.StartCoroutine</c>）。</b> 敌人被销毁**或只是被 <c>SetActive(false)</c>**
    /// （死亡时会、<c>SetActiveByPlayerDistance</c> 把远处角色停用时也会）都会让 Unity
    /// **直接中止协程**，协程内部那两处复位（循环里的空引用检查、正常结尾）都来不及跑——
    /// 于是 <c>_isSwapping</c> 会永久停在 true，此后**所有**相位怪都不再触发
    /// （<see cref="OnHitPlayer"/> 的早退）。不报错、不留日志。</para>
    ///
    /// <para>这里不靠"在 <c>OnCleanup</c> 里复位"来兜：那只相位怪死亡时会清掉**另一只**
    /// 正在进行的交换，把 <c>_isSwapping</c> 本来要防的并发交换放回来。改用**时限自愈**：
    /// 合法的交换最长就是 <see cref="SwapDuration"/>（协程里没有前置等待），超过它还没复位
    /// 的，只可能是协程被中止了——此时清掉即可，且不影响任何仍在进行的交换。</para>
    /// </summary>
    public class PhaseSwapBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Phase";

        private static readonly float CooldownSeconds = 8f;
        /// <summary>
        /// 一次换位的时长（秒）。**联机下由客户端执行同一份平滑移动，所以必须公开**——
        /// 两端用不同的时长会让"换位"看起来不一样。
        /// </summary>
        public const float SwapDurationSeconds = 0.3f;

        private const float SwapDuration = SwapDurationSeconds; // 交换过程持续时间（秒），越小越快

        /// <summary>换位时抬高一点，避免地形起伏把角色卡进地里（与原协程里的 offset 一致）。</summary>
        private static readonly Vector3 SwapOffset = Vector3.up * 0.1f;

        /// <summary>
        /// 把 <c>_isSwapping</c> 判为"陈旧"的时限。取 <see cref="SwapDuration"/> 加 1 秒余量，
        /// 远小于 8 秒冷却——所以它只会清掉真正卡死的标志，不会放宽触发频率。
        /// </summary>
        private static readonly float SwapStaleSeconds = SwapDuration + 1f;

        private static float _lastSwapTime = -999f;
        private static bool _isSwapping = false; // 防止在交换过程中重复触发
        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            // 自愈：标志为真、却早已超过一次合法交换所需的时长 ⇒ 那条协程被中止了（见类注释）。
            // 注意 `_lastSwapTime` 与 `_isSwapping` 是在同一帧里先后写下的（协程的第一次
            // MoveNext 是同步跑完的），所以这个差值对"正在进行的交换"不会误判。
            if (_isSwapping && Time.time - _lastSwapTime > SwapStaleSeconds)
            {
                _isSwapping = false;

                // 绊线：这一步真的被走到，说明**这台机器上**刚发生过一次协程被中止
                //（否则标志早已由协程自己复位）。本工程历史上这类"被中止即静默卡死"
                // 的失效正是靠日志才被发现的，所以调试版留一行；正式版不打印。
                if (DebugSwitch.Enabled)
                {
                    Debug.Log($"[EliteEnemies.PhaseSwap] 检测到陈旧的交换标志并已复位" +
                              $"（距上次交换 {Time.time - _lastSwapTime:F1}s）");
                }
            }

            if (Time.time - _lastSwapTime < CooldownSeconds || _isSwapping)
                return;

            // ⚠ 目标必须是 **victim（被打中的那个玩家）**，不是 `CharacterMainControl.Main`。
            //   联机下判定在主机上跑，而挨打的往往是**客机玩家的复制体**；
            //   写死 Main 会把效果挂到主机自己的玩家身上，客机什么都看不到。
            var player = victim;
            if (player == null || attacker == null) return;
            
            Vector3 enemyPos = attacker.transform.position;
            Vector3 playerPos = player.transform.position;

            // 联机下只有玩家那一半要转交：精英是主机权威的、主机自己挪；
            // 而玩家的位置由他自己那台机器说了算（主机上那个只是复制体）。
            if (PlayerEffectRelay.TryRelay(player, PlayerEffectRelay.Kind.PhaseSwap, enemyPos))
            {
                // ⚠ 不走 SmoothSwapRoutine——那个会连玩家一起挪，而挪复制体
                //   既到不了真人、又会被客机的同步覆盖回去。只挪精英。
                attacker.StartCoroutine(MoveEnemyOnly(attacker, playerPos + SwapOffset));

                _lastSwapTime = Time.time;
                PlayerEffectRelay.PopTextOnElite(attacker, "EliteEnemies_Affix_Phase_PopText_1", null);
                return;
            }

            attacker.StartCoroutine(SmoothSwapRoutine(attacker, player));

            _lastSwapTime = Time.time;
            PlayerEffectRelay.PopTextOnElite(attacker, "EliteEnemies_Affix_Phase_PopText_1", null);
        }

        /// <summary>联机：只把精英挪到玩家（复制体）的位置；玩家那一半由他自己那台机器做。</summary>
        private IEnumerator MoveEnemyOnly(CharacterMainControl enemy, Vector3 target)
        {
            _isSwapping = true;
            yield return PlayerEffectActions.SmoothMoveTo(enemy, target, SwapDurationSeconds);
            _isSwapping = false;
        }

        /// <summary>
        /// 平滑交换位置的协程
        /// </summary>
        private IEnumerator SmoothSwapRoutine(CharacterMainControl enemy, CharacterMainControl player)
        {
            _isSwapping = true;

            Vector3 startPosEnemy = enemy.transform.position;
            Vector3 startPosPlayer = player.transform.position;

            // 稍微抬高一点高度，避免在移动过程中因为地形起伏卡在地里
            Vector3 offset = Vector3.up * 0.1f; 
            Vector3 targetPosEnemy = startPosPlayer + offset;
            Vector3 targetPosPlayer = startPosEnemy + offset;

            float elapsed = 0f;

            while (elapsed < SwapDuration)
            {
                // 检查对象是否还存在，防止报错
                if (enemy == null || player == null) 
                {
                    _isSwapping = false;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = elapsed / SwapDuration;
                
                // 使用 SmoothStep 让移动起步和结束更柔和，或者用 Lerp 保持匀速
                float smoothT = Mathf.SmoothStep(0, 1, t); 
                // float smoothT = t; // 线性移动更有“冲刺感”
                
                enemy.transform.position = Vector3.Lerp(startPosEnemy, targetPosEnemy, smoothT);
                player.transform.position = Vector3.Lerp(startPosPlayer, targetPosPlayer, smoothT);

                yield return null;
            }

            // 确保最终位置精确
            if (enemy != null) enemy.transform.position = targetPosEnemy;
            if (player != null) player.transform.position = targetPosPlayer;

            _isSwapping = false;
        }

        public void OnAttack(CharacterMainControl attacker, DamageInfo damageInfo) { }
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

    }
}