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
    /// 
    /// 健壮性增强：
    /// 1. 增加受击次数上限（默认75次），超过次数强制解除无敌。
    /// 2. 增强对分身状态的每一帧检测，防止分身失效但无敌未解除。
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

        // 保底机制：最大无敌受击次数
        private const int MaxInvincibleHits = 75; 
        
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

        // 运行时状态
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
            
            // 重置保底计数器
            _currentHitCount = 0;
            _isForceBroken = false;

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

            // 如果已经强制破盾，或者分身无效，则确保无敌关闭并停止逻辑
            if (_isForceBroken || !IsPartnerValid())
            {
                if (_isInvincible) SetInvincibleState(false);
                return;
            }

            UpdateOrbit(deltaTime);
        }

        /// <summary>
        /// 增强的有效性检测
        /// </summary>
        private bool IsPartnerValid()
        {
            if (_partner == null) return false;
            if (_partner.Health == null || _partner.Health.CurrentHealth <= 0) return false;
            
            // 增加检测：如果分身被禁用（例如被回收或脚本禁用），视为失效
            if (!_partner.gameObject.activeInHierarchy) return false;

            return true;
        }
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character != _self || !_isInvincible) return;
            
            // 如果已经强制破盾，不再处理
            if (_isForceBroken) return;
            
            _flashIntensity = 2.0f; 
            
            // 累计受击次数
            _currentHitCount++;

            // 检测是否达到保底阈值
            if (_currentHitCount >= MaxInvincibleHits)
            {
                ForceBreakShield(character);
                return;
            }

            // 常规免疫提示
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
        /// 强制击碎护盾（保底触发）
        /// </summary>
        private void ForceBreakShield(CharacterMainControl character)
        {
            _isForceBroken = true;
            SetInvincibleState(false);
            character.PopText(BrokenText);

            // 尝试清理可能卡住的分身
            if (_partner != null && _partner.Health.CurrentHealth > 0)
            {
                _partner.DestroyCharacter();
            }
            _partner = null;
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
            // 虽然已经在OnUpdate开头检查过有效性，这里作为双重保险
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
            
            // 只有当不是强制破盾导致的分身被清理时，才恢复分身AI（虽然此时通常也应该随之死亡）
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