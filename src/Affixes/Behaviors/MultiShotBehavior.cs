using ItemStatsSystem;
using EliteEnemies.Core;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
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

        /// <summary>
        /// 当前持握枪械的缓存。**按"换了武器才重取"**：原先每次开火都
        /// <c>character.GetComponentInChildren&lt;ItemSetting_Gun&gt;()</c>——
        /// 那是**遍历整条角色层级**（角色 + 模型 + 全部挂件），而本词条配的往往是自动武器，
        /// 一秒十几发 ⇒ 这是本次评审里最贵的一处热路径查找。
        ///
        /// <para>取法本身沿用游戏自己的写法（在物品上取组件：<c>item.GetComponent&lt;ItemSetting_Gun&gt;()</c>），
        /// 并且改成从**当前持握物**取——比"在角色层级里找任意一把枪"更准：
        /// 后者会连挂在身上的备用枪一起找到，可能从一把收起来的枪上取枪口。</para>
        /// </summary>
        private Item _cachedGunItem;
        private ItemSetting_Gun _cachedGun;



        /// <summary>
        /// 取当前持握武器上的枪械配置；持握物没变就直接用缓存（见字段注释）。
        /// </summary>
        private ItemSetting_Gun GetCurrentGun(CharacterMainControl character)
        {
            var heldAgent = character.CurrentHoldItemAgent;
            Item held = heldAgent != null ? heldAgent.Item : null;

            if (held == _cachedGunItem) return _cachedGun;

            _cachedGunItem = held;
            _cachedGun = held != null ? held.GetComponent<ItemSetting_Gun>() : null;
            return _cachedGun;
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 1. 基础校验
            if (character == null) return;
            if (Time.time - _lastGlobalTriggerTime < MinTriggerInterval) return;
            _lastGlobalTriggerTime = Time.time;

            // 2. 取当前枪械（按持握物缓存，见字段注释）
            ItemSetting_Gun currentGun = GetCurrentGun(character);
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

            // 4. 算基准方向：指向**最近的玩家**
            //
            //    ⚠ 原先取 `LevelManager.Instance.MainCharacter`——那是「本机玩家」。
            //    联机下判定在主机上跑，客机玩家是另一个角色对象 ⇒
            //    **额外子弹永远飞向主机玩家**，哪怕正在交火的是客机。
            //    用 FindNearestPlayer 才是"指向正在打的那个人"的合理近似。
            Vector3 baseDirection;
            var targetPlayer = EliteEnemyCore.FindNearestPlayer(character.transform.position, out _);

            if (targetPlayer != null)
            {
                Vector3 targetPos = targetPlayer.transform.position + Vector3.up * 1.0f;
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
                damage = 8f,

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






    }
}
