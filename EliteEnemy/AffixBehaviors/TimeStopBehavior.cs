using System;
using System.Collections;
using UnityEngine;
using EliteEnemies.AffixBehaviors;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【时停】词缀 - 敌人受到玩家伤害且在感知范围内时触发3秒时停效果
    /// </summary>
    public class TimeStopBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "TimeStop";
        
        private static readonly float TriggerMaxDistance = 50f;  // 最大触发距离
        private static readonly float TimeStopScale = 0.3f;      // 时停时间缩放
        private static readonly float TimeStopDuration = 3f;     // 时停持续时间

        // 静态标志：全局是否已有时停在运行
        private static bool _isAnyTimeStopActive = false;

        private bool _hasTriggered = false;
        private Coroutine _timeStopCoroutine;
        private CharacterMainControl _owner;
        private MonoBehaviour _coroutineRunner;
        
        private bool _isMyTimeStopActive = false;
        private float _targetTimeScale = 1f;
        
        private string EnemyPopLine => LocalizationManager.GetText("Affix_TimeStop_PopText_1","<color=#FFD700>砸瓦鲁多！</color>");

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;
            _owner = character;
            _hasTriggered = false;
            _isMyTimeStopActive = false;
            
            _coroutineRunner = character.GetComponent<EliteBehaviorComponent>();
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_hasTriggered) return;
            if (_isAnyTimeStopActive) return;
            if (character == null || character.Health == null) return;
            
            // 1. 检查伤害来源是否存在
            if (damageInfo.fromCharacter == null)
            {
                return;
            }

            // 2. 检查是否为玩家造成的伤害
            if (!damageInfo.fromCharacter.IsMainCharacter)
            {
                return; // 避免NPC互殴触发
            }

            // 3. 检查距离
            var player = CharacterMainControl.Main;
            if (player == null) return;

            float distanceToPlayer = Vector3.Distance(character.transform.position, player.transform.position);
            if (distanceToPlayer > TriggerMaxDistance)
            {
                return; // 距离过远，玩家可能感知不到，不触发
            }
            _hasTriggered = true;
            TriggerTimeStop(character);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        private void TriggerTimeStop(CharacterMainControl character)
        {
            try
            {
                if (_coroutineRunner == null) return;

                if (_timeStopCoroutine != null)
                {
                    _coroutineRunner.StopCoroutine(_timeStopCoroutine);
                }

                _timeStopCoroutine = _coroutineRunner.StartCoroutine(TimeStopCoroutine());
                character.PopText(EnemyPopLine);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TimeStopBehavior] 触发时停失败: {ex.Message}");
            }
        }

        private IEnumerator TimeStopCoroutine()
        {
            float originalTimeScale = Time.timeScale;
            float originalFixedDeltaTime = Time.fixedDeltaTime;

            try
            {
                // 标记全局时停已激活
                _isAnyTimeStopActive = true;
                _isMyTimeStopActive = true;
                _targetTimeScale = TimeStopScale;

                Coroutine maintainCoroutine = _coroutineRunner.StartCoroutine(MaintainTimeScaleCoroutine());

                Time.timeScale = TimeStopScale;
                Time.fixedDeltaTime = 0.02f * TimeStopScale;

                float elapsed = 0f;
                while (elapsed < TimeStopDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                _isMyTimeStopActive = false;
                if (maintainCoroutine != null)
                {
                    _coroutineRunner.StopCoroutine(maintainCoroutine);
                }
            }
            finally
            {
                // 恢复全局标志
                _isAnyTimeStopActive = false;
                _isMyTimeStopActive = false;
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
                _timeStopCoroutine = null;
            }
        }

        private IEnumerator MaintainTimeScaleCoroutine()
        {
            while (_isMyTimeStopActive)
            {
                if (Mathf.Abs(Time.timeScale - _targetTimeScale) > 0.01f)
                {
                    Time.timeScale = _targetTimeScale;
                    Time.fixedDeltaTime = 0.02f * _targetTimeScale;
                }
                
                yield return null;
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 如果是自己的时停，清理全局标志
            if (_isMyTimeStopActive)
            {
                _isAnyTimeStopActive = false;
                _isMyTimeStopActive = false;
            }
            
            if (_timeStopCoroutine != null && _coroutineRunner != null)
            {
                _coroutineRunner.StopCoroutine(_timeStopCoroutine);
                _timeStopCoroutine = null;
            }
            
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (_isMyTimeStopActive)
            {
                _isAnyTimeStopActive = false;
                _isMyTimeStopActive = false;
            }
            
            if (_timeStopCoroutine != null && _coroutineRunner != null)
            {
                _coroutineRunner.StopCoroutine(_timeStopCoroutine);
                _timeStopCoroutine = null;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            _hasTriggered = false;
            _owner = null;
            _coroutineRunner = null;
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
        }
    }
}