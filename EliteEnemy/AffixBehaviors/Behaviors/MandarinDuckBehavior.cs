using System;
using System.Collections;
using EliteEnemies.EliteEnemy.Core;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【鸳鸯】词缀
    /// 效果：出生时复制一个无精英词条的伴侣，伴侣会围绕本体旋转，并为本体分担 50% 的伤害。
    /// </summary>
    public class MandarinDuckBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "MandarinDuck";

        private static readonly float OrbitRadius = 1.8f;
        private static readonly float RotationSpeed = 120f;
        private static readonly float CloneDelay = 0.5f;
        
        private static readonly float PartnerHealthRatio = 1.3f;
        private static readonly float PartnerDamageRatio = 0.7f;
        private static readonly float PartnerScaleRatio = 0.8f;
        
        // 伤害分担比例
        private static readonly float DamageShareRatio = 0.7f; 
        
        private CharacterMainControl _self;
        private CharacterMainControl _partner;
        private float _currentAngle = 0f;
        private bool _hasSpawned = false;
        
        private readonly Lazy<string> _partnerName = new(() => 
            LocalizationManager.GetText("Affix_MandarinDuck_MateSuffix"));
        private string PartnerName => _partnerName.Value;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _self = character;
            _hasSpawned = false;
            
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
                customKeySuffix:"EE_MandarinDuckPartner_NonElite",
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
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_partner == null || _partner.Health.CurrentHealth <= 0) return;
            UpdateOrbit(deltaTime);
        }

        private void UpdateOrbit(float deltaTime)
        {
            _currentAngle += RotationSpeed * deltaTime;
            if (_currentAngle >= 360f) _currentAngle -= 360f;

            Vector3 offset = Quaternion.Euler(0, _currentAngle, 0) * (Vector3.forward * OrbitRadius);
            Vector3 targetPos = _self.transform.position + offset;

            _partner.transform.position = targetPos;
            //_partner.transform.rotation = _self.transform.rotation; 
        }
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }
        
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_partner == null || _partner.Health.CurrentHealth <= 0 || character != _self) return;
            if (damageInfo.damageValue <= 0) return;
    
            float damageToShare = damageInfo.damageValue * DamageShareRatio;
            damageInfo.damageValue -= damageToShare;
            
            CharacterMainControl source = damageInfo.fromCharacter;

            // 关键修复：如果攻击者是本体自己（_self），或者原始攻击者为空，
            // 绝对不要把 _self 传进去，因为 _self 可能马上就要死了（被销毁）。
            // 传 null 代表“未知/环境伤害”，这样伴侣受击后不会试图去寻找/攻击一个已销毁的对象。
            if (source == _self || source == null)
            {
                source = null;
            }
            DamageInfo sharedDmg = new DamageInfo(source);

            sharedDmg.damageValue = damageToShare;
            sharedDmg.damageType = damageInfo.damageType;

            // 对分身造成伤害
            if (_partner.mainDamageReceiver != null)
            {
                _partner.mainDamageReceiver.Hurt(sharedDmg);
            }
            else
            {
                _partner.Health.Hurt(sharedDmg);
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_partner != null && _partner.Health.CurrentHealth > 0)
            {
                var agent = _partner.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent) 
                {
                    agent.enabled = true;
                    UnityEngine.AI.NavMeshHit hit;
                    if (UnityEngine.AI.NavMesh.SamplePosition(_partner.transform.position, out hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        _partner.transform.position = hit.position;
                    }
                }
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _self = null;
            _partner = null;
        }
    }
}