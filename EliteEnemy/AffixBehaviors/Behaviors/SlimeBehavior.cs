using ECM2;
using UnityEngine;
using EliteEnemies.EliteEnemy.AttributeModifier;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【史莱姆】词缀 - 体型随血量缩小，伤害随之提升
    /// </summary>
    public class SlimeBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Slime";

        private static readonly float InitialScale = 3.5f;
        private static readonly float MinScale = 0.5f;
        private static readonly float InitialHealthMult = 3.5f;
        private static readonly float InitialDamageMult = 0.65f;
        private static readonly float MaxDamageMult = 1.6f;
        private static readonly float HealthThreshold = 0.05f;

        private static readonly float JumpForce = 5f;
        private static readonly float JumpIntervalMin = 0.5f;
        private static readonly float JumpIntervalMax = 1.5f;
        private static readonly float GroundPause = 0.3f;
        private static readonly float MaxJumpHeightCheck = 5f;
        private static readonly float NoJumpHealthThreshold = 0.2f;

        private CharacterMainControl _character;
        private Health _health;
        private CharacterMovement _movement;
        private Rigidbody _rb;

        private Vector3 _originalScale;
        private float _lastSafeGroundY;
        private bool _hasSafeData = false;
        private float _lastHealthPercent = 1f;
        private float _nextJumpTime;
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

            // 初始生命加成并补满血量
            AttributeModifier.AttributeModifier.Quick.ModifyHealth(character, InitialHealthMult, this.AffixName, true);
            
            // 初始体型变化与伤害降低
            UpdateScaleAndDamage(character, 1.0f);

            _lastHealthPercent = 1f;
            _nextJumpTime = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_health == null || _health.IsDead) return;

            // 1. 安全高度记录
            if (IsGrounded())
            {
                _lastSafeGroundY = character.transform.position.y;
                _hasSafeData = true;
            }

            // 2. 动态体型和伤害更新
            float currentHealthPercent = _health.CurrentHealth / _health.MaxHealth;
            if (Mathf.Abs(currentHealthPercent - _lastHealthPercent) >= HealthThreshold)
            {
                UpdateScaleAndDamage(character, currentHealthPercent);
                _lastHealthPercent = currentHealthPercent;
            }

            // 3. 跳跃逻辑
            if (currentHealthPercent >= NoJumpHealthThreshold && Time.time >= _nextJumpTime && CanSafelyJump())
            {
                PerformJump();
                _nextJumpTime = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
            }
        }

        private void UpdateScaleAndDamage(CharacterMainControl character, float healthPercent)
        {
            float t = 1f - healthPercent;
    
            float newScaleFactor = Mathf.Lerp(InitialScale, MinScale, t);
            character.transform.localScale = _originalScale * newScaleFactor;

            float newDamageMultiplier = Mathf.Lerp(InitialDamageMult, MaxDamageMult, t);
    
            AttributeModifier.AttributeModifier.Modify(character, StatModifier.Attributes.GunDamageMultiplier, newDamageMultiplier, true, this.AffixName);
            AttributeModifier.AttributeModifier.Modify(character, StatModifier.Attributes.MeleeDamageMultiplier, newDamageMultiplier, true, this.AffixName);
        }

        private bool IsGrounded() => _movement != null && _movement.isGrounded && Mathf.Abs(_movement.velocity.y) < 0.1f;

        private bool CanSafelyJump() => IsGrounded() && !Physics.Raycast(_character.transform.position + Vector3.up, Vector3.up, MaxJumpHeightCheck, _obstacleMask);

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
            if (_hasSafeData && character.transform.position.y > _lastSafeGroundY + 1.0f)
            {
                character.transform.position = new Vector3(character.transform.position.x, _lastSafeGroundY + 0.1f, character.transform.position.z);
            }
            
            if (_rb != null) _rb.isKinematic = true;
            if (_movement != null) _movement.enabled = false;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);

            if (character != null && _originalScale != Vector3.zero)
            {
                character.transform.localScale = _originalScale;
            }

            if (_movement != null) _movement.enabled = true;
            if (_rb != null) _rb.isKinematic = false;

            _character = null;
            _health = null;
            _movement = null;
            _rb = null;
            _hasSafeData = false;
        }
    }
}