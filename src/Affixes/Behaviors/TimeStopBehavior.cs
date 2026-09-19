using System;
using System.Collections;
using EliteEnemies.Core;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【时停】词缀 - 敌人受到玩家伤害且在感知范围内时触发 3 秒时停效果。
    ///
    /// <para><b>时间缩放走官方管理器，不再自己写 `Time.timeScale`。</b>
    /// 游戏的 <c>TimeScaleManager.Update</c> **每帧无条件**写
    /// <c>Time.timeScale</c> 与 <c>Time.fixedDeltaTime</c>（<c>TimeScaleManager.cs:33-34</c>），
    /// 所以原先"自己写 + 一个每帧维护的协程"是在**跟它打架**——效果最多维持一帧，
    /// 那个维护协程纯粹是白烧的每帧开销。现在改成设置官方的
    /// <c>TimeScaleManager.devCambulletTimeScale</c>（<c>:5</c>；管理器每帧取
    /// <c>Mathf.Min(1, 它, …)</c>），让**它**去套用：既保住了本词条的调参
    /// （<c>TimeStopScale = 0.3</c>），又顺带让游戏自己的暂停（<c>GameManager.Paused</c>）
    /// 与相机模式（<c>CameraMode.Active</c>）按它们该有的优先级压过我们——那两条现在由它接管，
    /// 我们不再覆盖它们。</para>
    ///
    /// <para>⚠ 官方另有一个 <c>EnterBulletTime(时长)</c>（<c>:37</c>），但它把倍率写死成私有的
    /// <c>0.05f</c>（<c>:7</c>）——用它等于把本词条从"0.3 倍速"改成"几乎静止"，
    /// 那是改玩法，所以没用它。</para>
    ///
    /// <para>⚠ 等待时长必须用 <see cref="WaitForSecondsRealtime"/>：<c>WaitForSeconds</c> 走的是
    /// **被缩放后**的时间，而此刻时间缩放已经是 0.3——3 秒会变成 10 秒。</para>
    /// </summary>
    public class TimeStopBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        private const string LogTag = "[EliteEnemies.TimeStop]";

        public override string AffixName => "TimeStop";

        private static readonly float TriggerMaxDistance = 50f;  // 最大触发距离
        private static readonly float TimeStopScale = 0.3f;      // 时停时间缩放
        private static readonly float TimeStopDuration = 3f;     // 时停持续时间

        /// <summary>全局：本模组的时停当前是否已被某只怪占用（避免多只怪抢同一个全局时间缩放）。</summary>
        private static bool _isAnyTimeStopActive;

        /// <summary>域重载时复位（静态状态不随场景/域重置，见 SplitBehavior 的同名做法）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _isAnyTimeStopActive = false;

        private bool _hasTriggered;
        private bool _isMyTimeStopActive;
        private Coroutine _timeStopCoroutine;

        /// <summary>接管前的 <c>devCambulletTimeScale</c>，结束时还原（别的来源若也用了它，不该被我们清掉）。</summary>
        private float _previousDevTimeScale = 1f;

        private string EnemyPopLine =>
            LocalizationManager.GetText("EliteEnemies_Affix_TimeStop_PopText_1", "<color=#FFD700>砸瓦鲁多！</color>");

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _hasTriggered = false;
            _isMyTimeStopActive = false;
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_hasTriggered) return;
            if (_isAnyTimeStopActive) return;
            if (character == null || character.Health == null) return;

            // 1. 必须由**玩家**造成（本机玩家与远端玩家都算）——避免 NPC 互殴触发。
            //
            //    ⚠ 原先只认 `IsMainCharacter`：联机下客机造成的伤害，其攻击者在**主机**上是
            //    客机玩家的**复制体**，不满足该条件 ⇒ **时停在联机下永不触发**。
            //    判据与 `DamageReceiverPatches` 用的是同一个（见 EliteEnemyCore.RemotePlayerPredicate）。
            var player = damageInfo.fromCharacter;
            if (player == null) return;
            if (!player.IsMainCharacter && !EliteEnemyCore.IsRemotePlayerCharacter(player)) return;

            // 2. 距离检查：太远玩家感知不到，不触发。
            //    ⚠ 量的是**造成这次伤害的那个玩家**到精英的距离，不是本机玩家——
            //    联机下这俩常常不是同一个人（原先写死 Main，等于只在主机玩家挨着时才触发）。

            float distanceToPlayer = Vector3.Distance(character.transform.position, player.transform.position);
            if (distanceToPlayer > TriggerMaxDistance) return;

            _hasTriggered = true;
            TriggerTimeStop(character);
        }

        /// <summary>攻击不参与本词条逻辑（接口要求实现）。</summary>
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        private void TriggerTimeStop(CharacterMainControl character)
        {
            try
            {
                if (_isMyTimeStopActive) return;

                _isMyTimeStopActive = true;
                _isAnyTimeStopActive = true;

                _previousDevTimeScale = TimeScaleManager.devCambulletTimeScale;
                TimeScaleManager.devCambulletTimeScale = TimeStopScale;

                character.PopText(EnemyPopLine);

                if (_timeStopCoroutine != null) StopManagedCoroutine(_timeStopCoroutine);
                _timeStopCoroutine = StartManagedCoroutine(EndTimeStopAfterDelay());
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 触发时停失败: {ex}");
                EndTimeStop();
            }
        }

        private IEnumerator EndTimeStopAfterDelay()
        {
            // 必须用 realtime：此刻 timeScale 已被压到 0.3，用 WaitForSeconds 会等 10 秒
            yield return new WaitForSecondsRealtime(TimeStopDuration);
            EndTimeStop();
        }

        /// <summary>
        /// 结束本词条的时停。**只在"确实是自己在时停"时才还还原**——
        /// 原先是在死亡/清理里**无条件**把 <c>Time.timeScale</c> 写回 1，于是另一只怪的时停
        /// 会被这只怪的死亡踩回正常速一帧（维护协程下一帧才纠正）。
        /// </summary>
        private void EndTimeStop()
        {
            if (!_isMyTimeStopActive) return;

            _isMyTimeStopActive = false;
            _isAnyTimeStopActive = false;
            _timeStopCoroutine = null;

            TimeScaleManager.devCambulletTimeScale = _previousDevTimeScale > 0f ? _previousDevTimeScale : 1f;
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) => EndTimeStop();

        public override void OnCleanup(CharacterMainControl character) => EndTimeStop();
    }
}
