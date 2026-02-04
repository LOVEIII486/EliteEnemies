using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【多重射击】词缀 - 攻击时额外发射两枚偏转子弹
    /// </summary>
    public class MultiShotBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "MultiShot";

        // 全局频率限制
        private float _lastGlobalTriggerTime;
        private const float MinTriggerInterval = 0.05f;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 1. 基础校验
            if (character == null) return;
            if (Time.time - _lastGlobalTriggerTime < MinTriggerInterval) return;
            _lastGlobalTriggerTime = Time.time;

            // 2. 动态获取枪械
            ItemSetting_Gun currentGun = character.GetComponentInChildren<ItemSetting_Gun>();
            if (currentGun == null || currentGun.bulletPfb == null) return;

            // 3. 确定枪口位置
            Transform gunTransform = currentGun.transform;
            Vector3 muzzlePos = gunTransform.position;


            if (muzzlePos.y < character.transform.position.y + 0.5f)
            {
                muzzlePos = character.transform.position + Vector3.up * 1.2f;
            }
            else
            {
                muzzlePos += Vector3.up * 0.1f;
            }

            // 4. 算基准方向：直接指向玩家
            Vector3 baseDirection;
            var mainPlayer = LevelManager.Instance?.MainCharacter;

            if (mainPlayer != null)
            {
                // 获取玩家中心位置
                Vector3 targetPos = mainPlayer.transform.position + Vector3.up * 1.0f;
                baseDirection = (targetPos - muzzlePos).normalized;
            }
            else
            {
                baseDirection = character.transform.forward;
            }

            // 5. 发射偏转子弹
            SpawnBullet(character, currentGun, muzzlePos, baseDirection, -18f);
            SpawnBullet(character, currentGun, muzzlePos, baseDirection, 18f);
        }

        private void SpawnBullet(CharacterMainControl shooter, ItemSetting_Gun gun, Vector3 origin, Vector3 baseDir,
            float angleOffset)
        {
            if (LevelManager.Instance?.BulletPool == null) return;

            Projectile bullet = LevelManager.Instance.BulletPool.GetABullet(gun.bulletPfb);
            if (bullet == null) return;

            if (!bullet.gameObject.activeSelf) bullet.gameObject.SetActive(true);

            Quaternion rotation = Quaternion.AngleAxis(angleOffset, Vector3.up);
            Vector3 finalDirection = rotation * baseDir;

            bullet.transform.position = origin;
            bullet.transform.rotation = Quaternion.LookRotation(finalDirection, Vector3.up);

            int weaponId = gun.Item != null ? gun.Item.TypeID : 0;

            ProjectileContext context = new ProjectileContext
            {
                direction = finalDirection,
                firstFrameCheck = false,
                firstFrameCheckStartPoint = origin,

                team = shooter.Team,
                fromCharacter = shooter,
                fromWeaponItemID = weaponId,

                speed = 28f,
                damage = 10f,

                distance = 50f,
                halfDamageDistance = 25f,
                penetrate = 0,
                critRate = 0f,
                critDamageFactor = 1f,
                armorPiercing = 5f,
                ignoreHalfObsticle = false,

                element_Physics = 1f,
                element_Fire = 0f,
                element_Poison = 0f,
                element_Electricity = 0f,
                explosionRange = 0f
            };

            bullet.Init(context);

            if (gun.muzzleFxPfb != null)
            {
                UnityEngine.Object.Instantiate(gun.muzzleFxPfb, origin, Quaternion.LookRotation(finalDirection));
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
        }
    }
}