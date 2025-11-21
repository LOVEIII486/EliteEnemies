using System;
using ECM2;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【史莱姆】词缀 - 初始巨大但虚弱，血量降低时体型缩小且伤害增强，并会周期性跳跃
    /// </summary>
    public class SlimeBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Slime";

        // ===== 体型与属性配置 =====
        private static readonly float InitialScale       = 3.5f;   // 初始体型倍率
        private static readonly float MinScale           = 0.5f; // 最小体型倍率
        private static readonly float InitialHealthMult  = 3.5f;   // 初始血量倍率
        private static readonly float InitialDamageMult  = 0.4f; // 初始伤害倍率
        private static readonly float MaxDamageMult      = 1.6f; // 最大伤害倍率
        private static readonly float HealthThreshold    = 0.05f; // 血量变化达到 5% 才更新

        // ===== 跳跃相关配置 =====
        private static readonly float JumpIntervalMin    = 0.5f;  // 最小跳跃间隔
        private static readonly float JumpIntervalMax    = 1.5f;  // 最大跳跃间隔
        private static readonly float JumpForce          = 5f;    // 跳跃力度
        private static readonly float GroundPause        = 0.3f;  // 地面约束暂停时间
        private static readonly float MaxJumpHeightCheck = 5f;    // 跳跃前检测上方空间高度

        private CharacterMainControl _character;
        private Health _health;
        private CharacterMovement _movement;

        private Vector3 _originalScale;
        private Vector3 _lastValidPosition;
        private Vector3 _lastGroundedPosition;
        private bool _hasGroundedPosition;

        private float _lastHealthPercent = 1f;
        private float _nextJumpTime;
        private float _currentDamageMultiplier = 1f; // 记录当前已应用的伤害倍率（用于差值更新）

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _character = character;
            _health    = character.Health;

            if (_health == null)
            {
                Debug.LogError("[SlimeBehavior] 找不到 Health 组件");
                return;
            }
            
            if (character.movementControl != null)
            {
                _movement = character.movementControl.GetComponent<CharacterMovement>();
            }

            // 记录原始体型 & 位置
            _originalScale      = character.transform.localScale;
            _lastValidPosition  = character.transform.position;
            _lastGroundedPosition = character.transform.position;
            _hasGroundedPosition  = true;

            // 初始放大体型
            character.transform.localScale = _originalScale * InitialScale;


            ApplyInitialStats(character);

            // 初始化状态
            _lastHealthPercent       = 1f;
            _nextJumpTime            = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
            // _currentDamageMultiplier 已在 ApplyInitialStats 中设置为 InitialDamageMult
        }

        /// <summary>
        /// 应用初始血量和伤害倍率
        /// </summary>
        private void ApplyInitialStats(CharacterMainControl character)
        {
            // 血量：直接乘以倍率并回满
            AttributeModifier.Quick.ModifyHealth(character, InitialHealthMult, healToFull: true);

            // 伤害：通过“增量倍率”方式更新，
            // 防止多次调用时重复叠乘，使用 new / old 做差值
            _currentDamageMultiplier = 1f;
            ApplyDamageMultiplier(character, InitialDamageMult);
        }

        /// <summary>
        /// 以“相对当前值”的方式更新伤害倍率：实际乘以 (new / old)
        /// 这样不会因为多次调用导致无限叠乘。
        /// </summary>
        private void ApplyDamageMultiplier(CharacterMainControl character, float newMultiplier)
        {
            if (character == null) return;
            if (Mathf.Approximately(_currentDamageMultiplier, newMultiplier)) return;

            // 真实乘上的倍数
            float ratio = newMultiplier / Mathf.Max(_currentDamageMultiplier, 0.0001f);

            AttributeModifier.Quick.ModifyDamage(character, ratio);
            _currentDamageMultiplier = newMultiplier;
        }

        /// <summary>
        /// 每帧更新 
        /// </summary>
        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_health == null || _health.IsDead || character == null) return;

            // 边界检查
            Vector3 currentPos = character.transform.position;
            if (currentPos.y < -50f || currentPos.y > 100f)
            {
                Debug.LogWarning($"[SlimeBehavior] {character.characterPreset.nameKey} 超出边界，传送回安全位置");
                character.movementControl.ForceSetPosition(_lastValidPosition);
                return;
            }

            // 更新最后有效位置
            if (IsGrounded())
            {
                _lastValidPosition = currentPos;
            }
            
            float currentHealthPercent = _health.CurrentHealth / _health.MaxHealth;
            if (Mathf.Abs(currentHealthPercent - _lastHealthPercent) >= HealthThreshold)
            {
                UpdateScaleAndDamage(character, currentHealthPercent);
                _lastHealthPercent = currentHealthPercent;
            }
            
            if (Time.time >= _nextJumpTime && CanSafelyJump())
            {
                _lastGroundedPosition = character.transform.position;
                _hasGroundedPosition  = true;

                PerformJump();
                _nextJumpTime = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
            }
        }

        /// <summary>
        /// 根据当前血量百分比更新体型和伤害倍率
        /// </summary>
        private void UpdateScaleAndDamage(CharacterMainControl character, float healthPercent)
        {
            // 血量从 100% → 0% ：
            // 体型从 InitialScale → MinScale
            // 伤害从 InitialDamageMult → MaxDamageMult
            float t = 1f - healthPercent;  // 血量越低 t 越大（0→1）

            float newScale          = Mathf.Lerp(InitialScale, MinScale, t);
            float newDamageMultiplier = Mathf.Lerp(InitialDamageMult, MaxDamageMult, t);

            // 应用体型
            character.transform.localScale = _originalScale * newScale;

            // 伤害用“差值倍率”更新
            ApplyDamageMultiplier(character, newDamageMultiplier);

            // Debug.Log($"[SlimeBehavior] {character.name} HP:{healthPercent:P0}, Scale:{newScale:F2}x, Dmg:{newDamageMultiplier:F2}x");
        }

        /// <summary>
        /// 是否在地面上
        /// </summary>
        private bool IsGrounded()
        {
            if (_movement == null) return false;
            return Mathf.Abs(_movement.velocity.y) < 0.1f;
        }

        /// <summary>
        /// 是否可以安全跳跃
        /// </summary>
        private bool CanSafelyJump()
        {
            if (_movement == null || _character == null) return false;

            if (!IsGrounded()) return false;

            // 检查上方是否有足够空间
            Vector3 rayStart = _character.transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(rayStart, Vector3.up, MaxJumpHeightCheck,
                    LayerMask.GetMask("Default", "Terrain")))
            {
                return false;
            }

            // 检查周围是否过于狭窄
            float   checkRadius      = 2f;
            var     nearbyColliders  = Physics.OverlapSphere(_character.transform.position, checkRadius,
                LayerMask.GetMask("Default", "Terrain"));

            if (nearbyColliders.Length > 5)
                return false;

            return true;
        }

        /// <summary>
        /// 执行一次向上跳跃
        /// </summary>
        private void PerformJump()
        {
            if (_movement == null || _character == null) return;

            try
            {
                // 暂停地面约束
                _movement.PauseGroundConstraint(GroundPause);

                // 设置向上速度
                Vector3 velocity = _movement.velocity;
                velocity.y       = JumpForce;
                _movement.velocity = velocity;

                // Debug.Log($"[SlimeBehavior] {_character.name} 跳跃!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlimeBehavior] 跳跃失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 尝试在精英死亡时把位置拉回到地面，避免尸体悬空/跌落
        /// </summary>
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character == null) return;

            try
            {
                Vector3    pos = character.transform.position;
                RaycastHit hit;

                // 用射线往下找到地面
                if (Physics.Raycast(pos + Vector3.up, Vector3.down, out hit, 50f,
                        LayerMask.GetMask("Default", "Terrain")))
                {
                    character.transform.position = hit.point;
                }
                // 否则退回到最后一次记录的落地点
                else if (_hasGroundedPosition)
                {
                    character.transform.position = _lastGroundedPosition;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlimeBehavior] OnEliteDeath 复位到地面失败: {ex.Message}");
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            // 注意：当前整体框架（包括 Giant/Mini）在清理时都不还原 Stat，
            // 因为敌人本身通常会被销毁，对象不会复用。
            // 这里仅恢复体型，并清空引用，避免潜在的复用问题。
            try
            {
                if (character != null && _originalScale != Vector3.zero)
                {
                    character.transform.localScale = _originalScale;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlimeBehavior] OnCleanup 重置体型失败: {ex.Message}");
            }

            _character               = null;
            _health                  = null;
            _movement                = null;
            _lastHealthPercent       = 1f;
            _currentDamageMultiplier = 1f;
            _hasGroundedPosition     = false;
        }
    }
}
