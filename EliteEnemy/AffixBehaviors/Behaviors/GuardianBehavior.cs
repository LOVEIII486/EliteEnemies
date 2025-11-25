using System;
using System.Collections;
using EliteEnemies.EliteEnemy.Core;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【守护】词缀
    /// 效果：出生时召唤一个分身。分身存活期间，本体处于【完全无敌】状态。
    /// 只有在受到攻击时，本体才会闪烁金光并提示免疫，提供受击反馈。
    /// </summary>
    public class GuardianBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Guardian";
        
        private static readonly float OrbitRadius = 2.0f;
        private static readonly float RotationSpeed = 180f;
        private static readonly float CloneDelay = 0.5f;
        
        private static readonly float PartnerHealthRatio = 0.6f;
        private static readonly float PartnerDamageRatio = 0.5f;
        private static readonly float PartnerScaleRatio = 0.8f;
        
        private Renderer[] _cachedRenderers; 
        private MaterialPropertyBlock _propBlock;
        private int _emissionColorId;
        
        private readonly Color _shieldColor = new Color(1.0f, 0.6f, 0.0f); 
        
        private float _flashIntensity = 0f;
        private bool _isGlowing = false; // 标记当前是否正在发光，优化性能用
        
        private float _lastPopTime = -999f;
        private const float PopCooldown = 0.5f;

        private CharacterMainControl _self;
        private CharacterMainControl _partner;
        private float _currentAngle = 0f;
        private bool _hasSpawned = false;
        private bool _isInvincible = false;

        private readonly Lazy<string> _partnerName = new(() => 
            LocalizationManager.GetText("Affix_Guardian_MateSuffix") ?? "Guardian");
        
        private readonly Lazy<string> _immuneText = new(() => 
            LocalizationManager.GetText("Affix_Guardian_ImmunePop") ?? "IMMUNE");
        
        private string PartnerName => _partnerName.Value;
        private string ImmuneText => _immuneText.Value;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _self = character;
            _hasSpawned = false;
            _isInvincible = false;
            _lastPopTime = -999f;
            _flashIntensity = 0f;
            _isGlowing = false;

            _emissionColorId = Shader.PropertyToID("_EmissionColor");
            _propBlock = new MaterialPropertyBlock();
            _cachedRenderers = character.GetComponentsInChildren<Renderer>(true);

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
            
            helper.SpawnClone(
                originalEnemy: _self,
                position: spawnPos,
                healthMultiplier: PartnerHealthRatio,
                damageMultiplier: PartnerDamageRatio,
                speedMultiplier: 1.0f,
                scaleMultiplier: PartnerScaleRatio,
                affixes: null,
                preventElite: true, 
                customDisplayName: $"{_self.characterPreset.DisplayName} {PartnerName}",
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
            UpdateFlashDecay(deltaTime);

            if (_partner == null || _partner.Health.CurrentHealth <= 0)
            {
                if (_isInvincible)
                {
                    SetInvincibleState(false);
                }
                return;
            }

            UpdateOrbit(deltaTime);
        }
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character != _self || !_isInvincible) return;
            
            _flashIntensity = 2.0f; 
            
            if (Time.time - _lastPopTime >= PopCooldown)
            {
                if (damageInfo.fromCharacter != null)
                {
                    character.PopText(ImmuneText);
                    _lastPopTime = Time.time;
                }
            }
        }

        /// <summary>
        /// 每一帧更新闪烁衰减
        /// </summary>
        private void UpdateFlashDecay(float deltaTime)
        {
            if (_cachedRenderers == null) return;
            
            if (_flashIntensity > 0f)
            {
                _isGlowing = true;
                _flashIntensity -= deltaTime * 4f; 
                if (_flashIntensity < 0f) _flashIntensity = 0f;

                Color finalColor = _shieldColor * _flashIntensity;

                foreach (var renderer in _cachedRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.GetPropertyBlock(_propBlock);
                        _propBlock.SetColor(_emissionColorId, finalColor);
                        renderer.SetPropertyBlock(_propBlock);
                    }
                }
            }
            else if (_isGlowing)
            {
                ResetVisualEffects();
                _isGlowing = false;
            }
        }

        /// <summary>
        /// 强制关闭特效
        /// </summary>
        private void ResetVisualEffects()
        {
            if (_cachedRenderers == null) return;

            foreach (var renderer in _cachedRenderers)
            {
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(_propBlock);
                    _propBlock.SetColor(_emissionColorId, Color.black);
                    renderer.SetPropertyBlock(_propBlock);
                }
            }
        }

        private void UpdateOrbit(float deltaTime)
        {
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
                _flashIntensity = 0f;
                ResetVisualEffects();
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            SetInvincibleState(false);
            if (_partner != null && _partner.Health.CurrentHealth > 0)
            {
                var agent = _partner.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent) agent.enabled = true;
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            ResetVisualEffects();
            SetInvincibleState(false);
            
            _self = null;
            _partner = null;
            _cachedRenderers = null;
        }
    }
}