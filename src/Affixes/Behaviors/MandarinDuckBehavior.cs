using System;
using System.Collections;
using EliteEnemies.Core;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【鸳鸯】词缀
    /// 效果：出生时复制一个无精英词条的伴侣，伴侣会围绕本体旋转，并为本体分担伤害。
    /// </summary>
    public class MandarinDuckBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "MandarinDuck";

        private static readonly float OrbitRadius = 1.8f;
        private static readonly float RotationSpeed = 120f;
        private static readonly float CloneDelay = 0.5f;

        private static readonly float PartnerHealthRatio = 1.3f;
        private static readonly float PartnerDamageRatio = 0.7f;
        private static readonly float PartnerScaleRatio = 0.9f;

        // 伤害分担比例
        private static readonly float DamageShareRatio = 0.7f;

        private CharacterMainControl _self;
        private CharacterMainControl _partner;
        private float _currentAngle = 0f;
        private bool _hasSpawned = false;

        /// <summary>
        /// 召唤体名字里"括号里那一截"的本地化键。**同一个键也要过网**（见 <see cref="EliteSummonRelay"/>）——
        /// 传键不传译文，客机才会按它自己那门语言重拼。
        /// </summary>
        private const string MateSuffixKey = "EliteEnemies_Affix_MandarinDuck_MateSuffix";

        private string PartnerSuffix => LocalizationManager.GetText(MateSuffixKey) ?? "Partner";

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
                customKeySuffix: "EE_DuckMate",
                customDisplayName: $"{_self.characterPreset.DisplayName} ({PartnerSuffix})",
                onSpawned: OnPartnerSpawned
            );
        }

        private void OnPartnerSpawned(CharacterMainControl clone)
        {
            if (clone == null) return;

            _partner = clone;

            // 名字过网：主机那句 customDisplayName 是
            // `{_self.characterPreset.DisplayName} ({PartnerSuffix})`，所以送出去的是
            // 「后缀键 + 前缀预设的资源名」，让客机在**自己那门语言**下重拼。
            // `SpawnClone(originalEnemy: _self)` ⇒ 召唤体的基预设就是召唤者的预设，两者同一个资源名。
            var ownerPreset = _self != null ? _self.characterPreset : null;
            if (ownerPreset != null)
            {
                EliteSummonRelay.RelaySummonName(clone, MateSuffixKey,
                                                 EliteSummonRelay.PresetKey(ownerPreset),
                                                 EliteSummonRelay.PresetKey(ownerPreset));
            }
            else
            {
                Debug.LogError("[EliteEnemies.MandarinDuck] 召唤者没有预设——伴侣的名字无法过网" +
                               "（客机会只显示基名或没有名字）");
            }

            // 伴侣由本行为每帧摆放（UpdateOrbit 直接写 transform.position），它不能自己走，
            // 否则两套移动互相打架。原先这里是 GetComponent<NavMeshAgent>().enabled = false——
            // 游戏根本不用 NavMeshAgent（AI 走 A*），所以那行**一直没生效**。
            // 见 AffixBehaviorBase.SetCharacterSelfMovement 的注释。
            SetCharacterSelfMovement(clone, false);

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
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_partner == null || _partner.Health.CurrentHealth <= 0 || character != _self) return;
            if (damageInfo.damageValue <= 0) return;

            float damageToShare = damageInfo.damageValue * DamageShareRatio;
            damageInfo.damageValue -= damageToShare;

            CharacterMainControl source = damageInfo.fromCharacter;

            // 如果伤害来源是自己，设为空
            if (source == _self) source = null;

            DamageInfo sharedDmg = new DamageInfo(source)
            {
                damageValue = damageToShare,
                damageType = damageInfo.damageType
            };

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



        public override void OnCleanup(CharacterMainControl character)
        {
            // 主人没了、轨道结束：把移动交还给伴侣自己，否则它会永远僵在原地。
            if (_partner != null) SetCharacterSelfMovement(_partner, true);

            _self = null;
            _partner = null;
        }
    }
}
