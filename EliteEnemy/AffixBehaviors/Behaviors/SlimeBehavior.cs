using System;
using ECM2;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【史莱姆】词缀
    /// </summary>
    public class SlimeBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Slime";

        // ===== 基础配置 =====
        private static readonly float InitialScale = 3.5f;
        private static readonly float MinScale = 0.5f;
        private static readonly float InitialHealthMult = 3.5f;
        private static readonly float InitialDamageMult = 0.65f;
        private static readonly float MaxDamageMult = 1.6f;
        private static readonly float HealthThreshold = 0.05f;

        // ===== 跳跃配置 =====
        private static readonly float JumpIntervalMin = 0.5f;
        private static readonly float JumpIntervalMax = 1.5f;
        private static readonly float JumpForce = 5f;
        private static readonly float GroundPause = 0.3f;
        private static readonly float MaxJumpHeightCheck = 5f;
        
        // 残血跳跃禁用阈值
        private static readonly float NoJumpHealthThreshold = 0.2f;

        // ===== 核心组件 =====
        private CharacterMainControl _character;
        private Health _health;
        private CharacterMovement _movement;
        private Rigidbody _rb;

        // ===== 状态记录 =====
        private Vector3 _originalScale;
        private float _lastSafeGroundY;
        private bool _hasSafeData = false;

        private float _lastHealthPercent = 1f;
        private float _nextJumpTime;
        private float _currentDamageMultiplier = 1f;

        private int _obstacleMask;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _character = character;
            _health = character.Health;
            _rb = character.GetComponent<Rigidbody>();
            
            if (character.movementControl != null)
                _movement = character.movementControl.GetComponent<CharacterMovement>();

            _obstacleMask = LayerMask.GetMask("Default", "Ground", "Wall", "HalfObsticle", "Door");

            // 初始化记录
            _originalScale = character.transform.localScale;
            _lastSafeGroundY = character.transform.position.y;
            _hasSafeData = true;

            // 初始变大
            character.transform.localScale = _originalScale * InitialScale;
            ApplyInitialStats(character);

            _lastHealthPercent = 1f;
            _nextJumpTime = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
        }

        private void ApplyInitialStats(CharacterMainControl character)
        {
            AttributeModifier.AttributeModifier.Quick.ModifyHealth(character, InitialHealthMult, healToFull: true);
            _currentDamageMultiplier = 1f;
            ApplyDamageMultiplier(character, InitialDamageMult);
        }

        private void ApplyDamageMultiplier(CharacterMainControl character, float newMultiplier)
        {
            if (character == null) return;
            if (Mathf.Abs(_currentDamageMultiplier - newMultiplier) < 0.01f) return;
            float ratio = newMultiplier / Mathf.Max(_currentDamageMultiplier, 0.0001f);
            AttributeModifier.AttributeModifier.Quick.ModifyDamage(character, ratio);
            _currentDamageMultiplier = newMultiplier;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_health == null || _health.IsDead || character == null) return;

            // 1. 实时更新安全高度
            if (IsGrounded())
            {
                _lastSafeGroundY = character.transform.position.y;
                _hasSafeData = true;
            }

            // 2. 边界保护
            if (character.transform.position.y < -50f)
            {
                if (_hasSafeData)
                {
                    character.movementControl.ForceSetPosition(
                        new Vector3(character.transform.position.x, _lastSafeGroundY + 0.5f, character.transform.position.z)
                    );
                }
                return;
            }

            // 3. 动态体型和伤害
            float currentHealthPercent = _health.CurrentHealth / _health.MaxHealth;
            if (Mathf.Abs(currentHealthPercent - _lastHealthPercent) >= HealthThreshold)
            {
                UpdateScaleAndDamage(character, currentHealthPercent);
                _lastHealthPercent = currentHealthPercent;
            }

            // 4. 残血时禁止跳跃
            if (currentHealthPercent < NoJumpHealthThreshold)
            {
                return;
            }

            // 5. 正常跳跃逻辑
            if (Time.time >= _nextJumpTime && CanSafelyJump())
            {
                PerformJump();
                _nextJumpTime = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
            }
        }

        private void UpdateScaleAndDamage(CharacterMainControl character, float healthPercent)
        {
            float t = 1f - healthPercent;
            float newScale = Mathf.Lerp(InitialScale, MinScale, t);
            float newDamageMultiplier = Mathf.Lerp(InitialDamageMult, MaxDamageMult, t);
            character.transform.localScale = _originalScale * newScale;
            ApplyDamageMultiplier(character, newDamageMultiplier);
        }

        private bool IsGrounded()
        {
            if (_movement == null) return false;
            return _movement.isGrounded && Mathf.Abs(_movement.velocity.y) < 0.1f;
        }

        private bool CanSafelyJump()
        {
            if (!IsGrounded()) return false;
            
            // 头顶检测
            if (Physics.Raycast(_character.transform.position + Vector3.up, Vector3.up, MaxJumpHeightCheck, _obstacleMask))
                return false;
                
            return true;
        }

        private void PerformJump()
        {
            if (_movement == null) return;
            _movement.PauseGroundConstraint(GroundPause);
            Vector3 v = _movement.velocity;
            v.y = JumpForce;
            _movement.velocity = v;
        }
        
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character == null) return;

            // 1. 恢复体型
            if (_originalScale != Vector3.zero)
            {
                character.transform.localScale = _originalScale;
            }

            // 2. 冻结物理
            if (_movement != null)
            {
                _movement.velocity = Vector3.zero;
                _movement.enabled = false;
            }
            if (_rb != null)
            {
                _rb.velocity = Vector3.zero;
                _rb.isKinematic = true;
            }

            // 3. 位置修正
            if (_hasSafeData)
            {
                float currentY = character.transform.position.y;
                
                // 如果当前高度比记录的安全高度高出 1 米以上，说明可能在跳跃中死亡
                if (currentY > _lastSafeGroundY + 1.0f)
                {
                    character.transform.position = new Vector3(
                        character.transform.position.x,
                        _lastSafeGroundY + 0.1f,
                        character.transform.position.z
                    );
                    // Debug.Log($"[Slime] 检测到悬空死亡，回退到安全高度 {_lastSafeGroundY:F2}");
                }
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (character != null)
            {
                if (_originalScale != Vector3.zero) 
                    character.transform.localScale = _originalScale;
                if (_movement != null) 
                    _movement.enabled = true;
                if (_rb != null)
                {
                    _rb.isKinematic = false;
                    _rb.detectCollisions = true;
                }
            }
            
            _character = null;
            _health = null;
            _movement = null;
            _rb = null;
            _hasSafeData = false;
        }
    }
}