using System;
using UnityEngine;
using ItemStatsSystem;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【报复】词缀 - 敌人受伤时发射子弹弹反击
    /// </summary>
    public class RevengeBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Revenge";

        private const string LogTag = "[EliteEnemies.Revenge]";
        private const int ProjectileSourceItemId = 327; // 子弹来源武器ID
        private const float ShootCooldown = 2f; // 发射冷却时间

        private CharacterMainControl _owner;
        private float _lastShootTime = -999f;

        // 缓存弹道预制体
        private static Projectile _cachedProjectilePrefab;
        private static GameObject _cachedMuzzleFxPrefab;
        private static bool _prefabInitialized = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _owner = character;

            // 初始化弹道预制体
            if (!_prefabInitialized)
            {
                InitializeProjectilePrefab();
            }
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (Time.time - _lastShootTime < ShootCooldown)
            {
                return;
            }
            if (damageInfo.fromCharacter == null)
            {
                return;
            }
            var attacker = damageInfo.fromCharacter;
            if (attacker.Team == character.Team)
            {
                return;
            }
            
            // 发射反击子弹
            ShootBullet(character, attacker);
            _lastShootTime = Time.time;
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _owner = null;
        }

        /// <summary>
        /// 发射
        /// </summary>
        private void ShootBullet(CharacterMainControl shooter, CharacterMainControl target)
        {
            try
            {
                if (shooter == null || target == null)
                {
                    return;
                }

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
                if (LevelManager.Instance?.BulletPool == null)
                {
                    Debug.LogWarning($"{LogTag} 弹道池不可用");
                    return;
                }

                Projectile bullet = LevelManager.Instance.BulletPool.GetABullet(_cachedProjectilePrefab);
                if (bullet == null)
                {
                    Debug.LogWarning($"{LogTag} 无法从弹道池获取子弹");
                    return;
                }

                // 设置子弹位置和方向
                bullet.transform.position = muzzlePos;
                bullet.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

                // 配置弹道上下文
                ProjectileContext context = new ProjectileContext
                {
                    // 基础配置
                    firstFrameCheck = true,
                    firstFrameCheckStartPoint = muzzlePos - direction * 0.5f,
                    direction = direction,
                    speed = 13f,

                    // 队伍和来源
                    team = shooter.Team,
                    fromCharacter = shooter,
                    fromWeaponItemID = ProjectileSourceItemId,

                    // 伤害配置
                    damage = 6f,
                    critRate = 0.05f,
                    critDamageFactor = 1.2f,

                    // 距离配置
                    distance = 30f,
                    halfDamageDistance = 15f,

                    // 穿透和护甲
                    penetrate = 0,
                    armorPiercing = 0f,
                    armorBreak = 0f,

                    // 元素伤害（物理）
                    element_Physics = 0f,
                    element_Fire = 1f,
                    element_Poison = 0f,
                    element_Electricity = 0f,
                    element_Space = 0f,

                    // 爆炸和 Buff
                    explosionRange = 2f,
                    explosionDamage = 8f,
                    buffChance = 0f,
                    buff = null,

                    // 其他
                    bleedChance = 0.2f,
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
                        // Debug.Log($"{LogTag} 从物品资源初始化弹道预制体成功");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 初始化弹道预制体失败: {ex.Message}");
                _prefabInitialized = true;
            }
        }

        /// <summary>
        /// 创建枪口特效
        /// </summary>
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