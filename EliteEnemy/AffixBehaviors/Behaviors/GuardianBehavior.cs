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
    /// 
    /// 1. 增加受击次数上限（默认50次），超过次数强制解除无敌。
    /// 2. 增强对分身状态的每一帧检测，防止分身失效但无敌未解除。
    /// 3. 新增：距离完整性检测。如果分身因物理/地形原因被卡住导致距离过远，自动解除无敌。
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

        // 最大无敌受击次数
        private const int MaxInvincibleHits = 50; 

        // 距离检测参数
        private const float MaxSeparationDeviation = 8.0f; // 允许的额外偏离距离
        private const float MaxSeparationTime = 3.0f;      // 持续处于异常距离的时间阈值
        private float _separationTimer = 0f;
        
        private Renderer[] _cachedRenderers; 
        private MaterialPropertyBlock _propBlock;
        private int _emissionColorId;
        
        private readonly Color _shieldColor = new Color(1.0f, 0.6f, 0.0f); 
        
        private float _flashIntensity = 0f;
        private bool _isGlowing = false; 
        
        private float _lastPopTime = -999f;
        private const float PopCooldown = 0.5f;

        private CharacterMainControl _self;
        private CharacterMainControl _partner;
        private float _currentAngle = 0f;
        private bool _hasSpawned = false;
        private bool _isInvincible = false;

        private int _currentHitCount = 0;
        private bool _isForceBroken = false;

        private readonly Lazy<string> _partnerName = new(() => 
            LocalizationManager.GetText("Affix_Guardian_MateSuffix") ?? "Guardian");
        
        private readonly Lazy<string> _immuneText = new(() => 
            LocalizationManager.GetText("Affix_Guardian_ImmunePop") ?? "IMMUNE");

        private readonly Lazy<string> _brokenText = new(() => 
            LocalizationManager.GetText("Affix_Guardian_ShieldBroken") ?? "SHIELD BROKEN");
        
        private string PartnerName => _partnerName.Value;
        private string ImmuneText => _immuneText.Value;
        private string BrokenText => _brokenText.Value;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _self = character;
            _hasSpawned = false;
            _isInvincible = false;
            _lastPopTime = -999f;
            _flashIntensity = 0f;
            _isGlowing = false;
            
            _currentHitCount = 0;
            _isForceBroken = false;
            _separationTimer = 0f;

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
                customKeySuffix: "EE_GuardianCore_NonElite",
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

            // 基础有效性检查
            if (_isForceBroken || !IsPartnerValid())
            {
                if (_isInvincible) SetInvincibleState(false);
                return;
            }

            // 1. 距离完整性检查 (CheckSeparation)
            CheckSeparation(character, deltaTime);
            if (_isForceBroken) return;

            // 2. 更新轨道位置
            UpdateOrbit(deltaTime);
        }

        /// <summary>
        /// 检查分身是否距离本体过远
        /// </summary>
        private void CheckSeparation(CharacterMainControl character, float deltaTime)
        {
            if (_self == null || _partner == null) return;

            float dist = Vector3.Distance(_self.transform.position, _partner.transform.position);
            
            // 判定阈值：标准半径 + 允许的最大偏差
            // 如果分身被卡墙角，虽然代码强制设置位置，但物理引擎可能会把它挤出去
            if (dist > OrbitRadius + MaxSeparationDeviation)
            {
                _separationTimer += deltaTime;
                if (_separationTimer > MaxSeparationTime)
                {
                    // 超过容忍时间，判定为严重卡死，强制破盾
                    Debug.LogWarning($"[GuardianBehavior] Partner stuck detected. Dist: {dist:F1}. Force breaking shield.");
                    ForceBreakShield(character);
                }
            }
            else
            {
                // 距离恢复正常，重置计时器
                _separationTimer = 0f;
            }
        }

        private bool IsPartnerValid()
        {
            if (_partner == null) return false;
            if (_partner.Health == null || _partner.Health.CurrentHealth <= 0) return false;
            if (!_partner.gameObject.activeInHierarchy) return false;

            return true;
        }
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character != _self || !_isInvincible) return;
            
            if (_isForceBroken) return;
            
            _flashIntensity = 2.0f; 
            _currentHitCount++;

            // 受击次数保底
            if (_currentHitCount >= MaxInvincibleHits)
            {
                ForceBreakShield(character);
                return;
            }

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
        /// 强制击碎护盾
        /// </summary>
        private void ForceBreakShield(CharacterMainControl character)
        {
            _isForceBroken = true;
            SetInvincibleState(false);
            character.PopText(BrokenText);

            // 销毁分身
            if (_partner != null)
            {
                if (_partner.Health != null && _partner.Health.CurrentHealth > 0)
                {
                    _partner.DestroyCharacter();
                }

                if (_partner != null && _partner.gameObject != null)
                {
                    UnityEngine.Object.Destroy(_partner.gameObject, 0.1f);
                }
            }
            _partner = null;
        }

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
                _flashIntensity = 0f;
                ResetVisualEffects();
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            SetInvincibleState(false);
            
            // 只有当不是强制破盾导致的分身被清理时，才恢复分身AI
            if (!_isForceBroken && _partner != null && _partner.Health.CurrentHealth > 0)
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