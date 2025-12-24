using System;
using System.Collections;
using ItemStatsSystem;
using UnityEngine;
using EliteEnemies.EliteEnemy.VisualEffects;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【报复】词缀 - 敌人受伤时发射子弹弹反击
    /// </summary>
    public class RevengeBehavior : AffixBehaviorBase, ICombatAffixBehavior, IUpdateableAffixBehavior
    {
        public override string AffixName => "Revenge";

        private static readonly string LogTag = "[EliteEnemies.Revenge]";
        private static readonly int ProjectileSourceItemId = 327; // 子弹来源武器ID
        private static readonly float ShootCooldown = 2f; // 发射冷却时间
        
        private static readonly Color GlowColor = new Color(0f, 1f, 1f); // 青色
        private static readonly float FlashDuration = 0.5f; // 闪烁半秒

        private CharacterMainControl _owner;
        private float _lastShootTime = -999f;
        private EliteGlowController _glowController;
        
        // 缓存弹道预制体
        private static Projectile _cachedProjectilePrefab;
        private static GameObject _cachedMuzzleFxPrefab;
        private static bool _prefabInitialized = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _owner = character;
            if (!_prefabInitialized)
            {
                InitializeProjectilePrefab();
            }
            _glowController = new EliteGlowController(character);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (Time.time - _lastShootTime < ShootCooldown) return;
            if (character == null || !character.gameObject.activeInHierarchy) return;
            if (damageInfo.fromCharacter == null || damageInfo.fromCharacter.Team == character.Team) return;
            
            _glowController.TriggerFlash(GlowColor, FlashDuration);

            ShootBullet(character, damageInfo.fromCharacter);
            _lastShootTime = Time.time;
        }
        
        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            _glowController?.Update(deltaTime);
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _glowController?.Reset();
            _owner = null;
            _glowController = null;
        }

        /// <summary>
        /// 发射反击子弹
        /// </summary>
        private void ShootBullet(CharacterMainControl shooter, CharacterMainControl target)
        {
            try
            {
                if (shooter == null || target == null) return;

                // 获取发射位置（敌人头部附近）
                Vector3 muzzlePos = shooter.transform.position + Vector3.up * 1.5f;

                // 计算射击方向
                Vector3 targetPos = target.transform.position + Vector3.up * 1f;
                Vector3 direction = (targetPos - muzzlePos).normalized;

                // 检查弹道预制体
                if (_cachedProjectilePrefab == null)
                {
                    Debug.LogWarning($"{LogTag} 弹道预制体未初始化");
                    return;
                }

                // 从弹道池获取子弹
                if (LevelManager.Instance?.BulletPool == null) return;

                Projectile bullet = LevelManager.Instance.BulletPool.GetABullet(_cachedProjectilePrefab);
                if (bullet == null) return;

                // 设置子弹位置和方向
                bullet.transform.position = muzzlePos;
                bullet.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

                // 配置弹道上下文 (直接复用原逻辑)
                ProjectileContext context = new ProjectileContext
                {
                    firstFrameCheck = true,
                    firstFrameCheckStartPoint = muzzlePos - direction * 0.5f,
                    direction = direction,
                    speed = 20f,
                    team = shooter.Team,
                    fromCharacter = shooter,
                    fromWeaponItemID = ProjectileSourceItemId,
                    damage = 8f,
                    critRate = 0.05f,
                    critDamageFactor = 1.2f,
                    distance = 25f,
                    halfDamageDistance = 15f,
                    penetrate = 0,
                    armorPiercing = 6f,
                    armorBreak = 0f,
                    element_Physics = 0f,
                    element_Fire = 1f,
                    element_Poison = 0f,
                    element_Electricity = 0f,
                    element_Space = 0f,
                    explosionRange = 2f,
                    explosionDamage = 12f,
                    buffChance = 0f,
                    buff = null,
                    bleedChance = 0.5f,
                    ignoreHalfObsticle = true,
                    gravity = 0f
                };

                // 初始化子弹
                bullet.Init(context);
                // 创建枪口特效
                CreateMuzzleFx(muzzlePos, direction);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 发射失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 初始化弹道预制体
        /// </summary>
        private static void InitializeProjectilePrefab()
        {
            try
            {
                Item prefab = ItemAssetsCollection.GetPrefab(ProjectileSourceItemId);
                if (prefab != null)
                {
                    ItemSetting_Gun gunSetting = prefab.GetComponent<ItemSetting_Gun>();
                    if (gunSetting != null && gunSetting.bulletPfb != null)
                    {
                        _cachedProjectilePrefab = gunSetting.bulletPfb;
                        _cachedMuzzleFxPrefab = gunSetting.muzzleFxPfb;
                        _prefabInitialized = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 初始化弹道预制体失败: {ex.Message}");
                _prefabInitialized = true;
            }
        }

        private void CreateMuzzleFx(Vector3 position, Vector3 direction)
        {
            try
            {
                if (_cachedMuzzleFxPrefab != null)
                {
                    UnityEngine.Object.Instantiate(
                        _cachedMuzzleFxPrefab,
                        position,
                        Quaternion.LookRotation(direction, Vector3.up)
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 创建枪口特效失败: {ex.Message}");
            }
        }
    }
}