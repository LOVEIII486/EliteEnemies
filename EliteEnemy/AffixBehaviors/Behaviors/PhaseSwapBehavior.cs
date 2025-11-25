using System;
using System.Collections;
using EliteEnemies.Localization;
using UnityEngine;
// 需要引入这个命名空间来使用协程

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 词缀：相位（Phase）
    /// 效果：命中玩家时，平滑地与玩家交换位置。
    /// </summary>
    public class PhaseSwapBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Phase";

        private static readonly float CooldownSeconds = 8f;
        private static readonly float SwapDuration = 0.3f; // 交换过程持续时间（秒），越小越快
        
        private static float _lastSwapTime = -999f;
        private static bool _isSwapping = false; // 防止在交换过程中重复触发

        private readonly Lazy<string> _phasePopTextFmt = new(() =>
            LocalizationManager.GetText(
                "Affix_Phase_PopText_1"
            )
        );

        private string PhasePopText => _phasePopTextFmt.Value;

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            if (Time.time - _lastSwapTime < CooldownSeconds || _isSwapping)
                return;

            var player = CharacterMainControl.Main;
            if (player == null || attacker == null) return;
            
            attacker.StartCoroutine(SmoothSwapRoutine(attacker, player));

            _lastSwapTime = Time.time;
            attacker.PopText(PhasePopText);
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
        public override void OnEliteInitialized(CharacterMainControl character) { }
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}