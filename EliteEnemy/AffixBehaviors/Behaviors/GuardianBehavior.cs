using System;
using System.Collections;
using UnityEngine;
using EliteEnemies.EliteEnemy.Core;
using EliteEnemies.EliteEnemy.VisualEffects;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【守护】词缀
    /// 效果：出生时召唤一个分身。分身存活期间，本体处于【完全无敌】状态。
    /// </summary>
    public class GuardianBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Guardian";
        
        private static readonly float OrbitRadius = 2.0f;
        private static readonly float RotationSpeed = 140f;
        private static readonly float CloneDelay = 0.5f;
        
        private static readonly float PartnerHealthRatio = 0.5f;
        private static readonly float PartnerDamageRatio = 0.5f;
        private static readonly float PartnerScaleRatio = 0.75f;

        private static readonly int MaxInvincibleHits = 30; 

        private static readonly float MaxSeparationDeviation = 8.0f; 
        private static readonly float MaxSeparationTime = 3.0f;      
        private float _separationTimer = 0f;
        
        private EliteGlowController _glowController;
        private readonly Color _shieldColor = new Color(1.0f, 0.6f, 0.0f);
        private const float FlashDuration = 0.25f;
        
        private float _lastPopTime = -999f;
        private const float PopCooldown = 0.5f;

        private CharacterMainControl _self;
        private CharacterMainControl _partner;
        private float _currentAngle = 0f;
        private bool _hasSpawned = false;
        private bool _isInvincible = false;

        private int _currentHitCount = 0;
        private bool _isForceBroken = false;

        private readonly Lazy<string> _partnerSuffix = new(() => 
            LocalizationManager.GetText("Affix_Guardian_MateSuffix") ?? "Guardian");
        
        private readonly Lazy<string> _immuneText = new(() => 
            LocalizationManager.GetText("Affix_Guardian_ImmunePop") ?? "IMMUNE");

        private readonly Lazy<string> _brokenText = new(() => 
            LocalizationManager.GetText("Affix_Guardian_ShieldBroken") ?? "SHIELD BROKEN");
        
        private string PartnerSuffix => _partnerSuffix.Value;
        private string ImmuneText => _immuneText.Value;
        private string BrokenText => _brokenText.Value;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _self = character;
            _hasSpawned = false;
            _isInvincible = false;
            _lastPopTime = -999f;
            
            _currentHitCount = 0;
            _isForceBroken = false;
            _separationTimer = 0f;

            _glowController = new EliteGlowController(character);

            ModBehaviour.Instance?.StartCoroutine(SpawnPartnerDelayed());
        }

        private IEnumerator SpawnPartnerDelayed()
        {
            yield return new WaitForSeconds(CloneDelay);

            if (_self == null || _self.Health.CurrentHealth <= 0 || _hasSpawned) 
                yield break;

            SpawnPartner();
        }

        private void SpawnPartner()
        {
            var helper = EggSpawnHelper.Instance;
            if (helper == null || !helper.IsReady) return;

            _hasSpawned = true;
            
            Vector3 spawnPos = _self.transform.position + _self.transform.forward * OrbitRadius;
            
            // 1. customKeySuffix 使用固定后缀，EggSpawnHelper 会将其同时应用到 name 和 nameKey
            // 2. customDisplayName 传入后缀名，EggSpawnHelper 内部会自动拼接原始名称
            helper.SpawnClone(
                originalEnemy: _self,
                position: spawnPos,
                healthMultiplier: PartnerHealthRatio,
                damageMultiplier: PartnerDamageRatio,
                speedMultiplier: 1.0f,
                scaleMultiplier: PartnerScaleRatio,
                affixes: null,
                preventElite: true, 
                customKeySuffix: "EE_GuardianPartner",
                customDisplayName: $"{_self.characterPreset.DisplayName} ({PartnerSuffix})",
                onSpawned: OnPartnerSpawned
            );
        }

        private void OnPartnerSpawned(CharacterMainControl clone)
        {
            if (clone == null) return;

            _partner = clone;

            var agent = clone.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent) agent.enabled = false;

            Vector3 dir = clone.transform.position - _self.transform.position;
            _currentAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            
            SetInvincibleState(true);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            _glowController?.Update(deltaTime);

            if (_isForceBroken || !IsPartnerValid())
            {
                if (_isInvincible) SetInvincibleState(false);
                return;
            }

            CheckSeparation(character, deltaTime);
            if (_isForceBroken) return;

            UpdateOrbit(deltaTime);
        }

        private void CheckSeparation(CharacterMainControl character, float deltaTime)
        {
            if (_self == null || _partner == null) return;

            float dist = Vector3.Distance(_self.transform.position, _partner.transform.position);
            
            // 如果分身因为地形原因卡死在远处，强制破盾
            if (dist > OrbitRadius + MaxSeparationDeviation)
            {
                _separationTimer += deltaTime;
                if (_separationTimer > MaxSeparationTime)
                {
                    ForceBreakShield(character);
                }
            }
            else
            {
                _separationTimer = 0f;
            }
        }

        private bool IsPartnerValid()
        {
            if (_partner == null) return false;
            if (_partner.Health == null || _partner.Health.CurrentHealth <= 0) return false;
            return _partner.gameObject.activeInHierarchy;
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character != _self || !_isInvincible || _isForceBroken) return;
            
            _glowController?.TriggerFlash(_shieldColor, FlashDuration, 2.0f);
            
            _currentHitCount++;

            // 达到最大抗性次数强制破盾
            if (_currentHitCount >= MaxInvincibleHits)
            {
                ForceBreakShield(character);
                return;
            }

            if (Time.time - _lastPopTime >= PopCooldown)
            {
                character.PopText(ImmuneText);
                _lastPopTime = Time.time;
            }
        }

        private void ForceBreakShield(CharacterMainControl character)
        {
            _isForceBroken = true;
            SetInvincibleState(false);
            character.PopText(BrokenText);

            if (_partner != null)
            {
                if (_partner.Health != null && _partner.Health.CurrentHealth > 0)
                {
                    _partner.DestroyCharacter();
                }
                UnityEngine.Object.Destroy(_partner.gameObject, 0.1f);
            }
            _partner = null;
        }

        private void UpdateOrbit(float deltaTime)
        {
            if (_partner == null) return;

            _currentAngle += RotationSpeed * deltaTime;
            if (_currentAngle >= 360f) _currentAngle -= 360f;

            Vector3 offset = Quaternion.Euler(0, _currentAngle, 0) * (Vector3.forward * OrbitRadius);
            Vector3 targetPos = _self.transform.position + offset;

            _partner.transform.position = targetPos;
            _partner.transform.rotation = _self.transform.rotation; 
        }

        private void SetInvincibleState(bool isInvincible)
        {
            if (_self == null || _self.Health == null) return;

            _isInvincible = isInvincible;
            _self.Health.SetInvincible(isInvincible);

            if (!isInvincible)
            {
                _glowController?.Reset(); 
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            SetInvincibleState(false);
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _glowController?.Reset();
            SetInvincibleState(false);
            _self = null;
            _partner = null;
        }
    }
}