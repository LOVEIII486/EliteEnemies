using System;
using System.Collections;
using ItemStatsSystem;
using UnityEngine;
using EliteEnemies.Visuals;

namespace EliteEnemies.Affixes.Behaviors
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

        /// <summary>
        /// 直击伤害的**基准值**。真正写进 context 的是它 <b>除以难度系数</b>后的值，
        /// 见 <see cref="DifficultyNormalizer"/>。
        /// </summary>
        private const float BaseDamage = 8f;

        /// <summary>
        /// 爆炸伤害的**基准值**。
        ///
        /// <para>⚠ 它与 <see cref="BaseDamage"/> 是**两个独立的伤害**，不是二选一：
        /// 子弹直接命中用 <c>context.damage</c>（<c>Projectile.cs:398</c>），
        /// 子弹死亡时若 <c>explosionRange &gt; 0</c> 再对范围内所有人用
        /// <c>context.explosionDamage</c>（<c>Projectile.cs:199</c>、<c>:214</c>）。
        /// **贴脸吃一发 = 两个都吃到。**</para>
        /// </summary>
        private const float BaseExplosionDamage = 12f;

        /// <summary>爆炸半径（米）。设 0 就没有爆炸伤害（见上面那条注释）。</summary>
        private const float ExplosionRange = 2f;

        private static readonly Color GlowColor = new Color(0f, 1f, 1f); // 青色
        private static readonly float FlashDuration = 0.5f; // 闪烁半秒

        private float _lastShootTime = -999f;
        private EliteGlowController _glowController;

        // 缓存弹道预制体
        private static Projectile _cachedProjectilePrefab;
        private static GameObject _cachedMuzzleFxPrefab;
        private static bool _prefabInitialized = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
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





        public override void OnCleanup(CharacterMainControl character)
        {
            _glowController?.Reset();
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

                // 两个伤害值都要先除掉难度系数——**这一步是玩家体验的关键**，理由见 DifficultyNormalizer。
                float norm = DifficultyNormalizer();

                // 配置弹道上下文 (直接复用原逻辑)
                ProjectileContext context = new ProjectileContext
                {
                    firstFrameCheck = true,
                    firstFrameCheckStartPoint = muzzlePos - direction * 0.5f,
                    direction = direction,
                    speed = 17f,
                    team = shooter.Team,
                    fromCharacter = shooter,
                    fromWeaponItemID = ProjectileSourceItemId,
                    damage = BaseDamage / norm,
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
                    explosionRange = ExplosionRange,
                    explosionDamage = BaseExplosionDamage / norm,
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
        /// 取出当前的「对玩家伤害倍率」，供发射时把基准伤害除掉——
        /// 让这发反击火箭弹的**落地伤害与难度无关**。
        ///
        /// <para><b>为什么必须这么做</b>：游戏的难度倍率加在**受害者那一侧**——
        /// <c>Health.Hurt</c>（<c>Health.cs:308</c>）里
        /// <c>if (!damageInfo.ignoreDifficulty &amp;&amp; team == Teams.player) damageValue *= LevelManager.Rule.DamageFactor_ToPlayer</c>
        /// （<c>:360-362</c>）。而 <c>DamageInfo</c> 是子弹**命中时游戏自己 new 的**
        /// （<c>Projectile.cs:397</c> 直击 / <c>:199</c> 爆炸），
        /// <c>ProjectileContext</c> 里**没有**对应字段 ⇒ 在我们造 context 的位置
        /// **够不到那份 DamageInfo**。</para>
        ///
        /// <para>⚠️ <b>别把这条理解成"开关是坏的"</b>（这里原先就是这么写的，害人）：
        /// <c>ignoreDifficulty</c> 的写入点是**一个都没有** ✓，但那指的是
        /// <b>游戏自己从不拨它</b>（所以原版伤害一律吃倍率），**不是"拨了没用"</b>。
        /// <c>:360</c> 那一行是**活的**：谁在这份 <c>DamageInfo</c> 上写 <c>true</c>，这一击就跳过倍率。
        /// 判据是**那份 DamageInfo 由谁 new**：</para>
        ///
        /// <list type="bullet">
        /// <item><b>我们自己 new 的</b>（例如自爆的爆炸）⇒ <b>直接设 <c>ignoreDifficulty = true</c> 即可</b>，
        /// 不需要反向除法；</item>
        /// <item><b>游戏在命中时自己 new 的</b>（弹道，就是本词条这种情况）⇒ 我们手上只有
        /// <c>ProjectileContext</c>，它没有这个字段 ⇒ <b>只能反向除</b>。</item>
        /// </list>
        ///
        /// <para>所以只能反过来：发射前先除掉它。玩家默认血量只有 <b>44</b>，
        /// 而高难度下这一发是 10×2 + 12×2 = <b>44</b> —— 正好秒杀。
        /// 反击伤害应当是可预期的，不该随难度翻倍。</para>
        ///
        /// <para><b>已知副作用（接受）</b>：爆炸对**非玩家**目标（开火者自己、旁边的其它敌人）
        /// 也会按除掉系数后的值结算，因为那些目标本来就不吃这条倍率 ⇒ 它们挨的更轻了。
        /// 反击火箭弹本来就是打玩家的，这个方向是对的。</para>
        ///
        /// <para>⚠ <b>必须挡住异常值</b>：系数为 0 时相除会得到 <c>Infinity</c>，
        /// 那一发就变成真正的秒杀——比不归一化还糟。</para>
        /// </summary>
        private static float DifficultyNormalizer()
        {
            try
            {
                // 用 var：Ruleset 在 Duckov.Rules 下，写死类型名要多一个 using，
                // 而这里只需要它的一个属性。
                var rule = LevelManager.Rule;
                float factor = rule != null ? rule.DamageFactor_ToPlayer : 1f;

                if (float.IsNaN(factor) || factor < 0.01f) return 1f;
                return factor;
            }
            catch (Exception ex)
            {
                // 读不到就按"不缩放"处理：宁可伤害按基准值走，也不要凭空放大或除零
                Debug.LogWarning($"{LogTag} 读取难度系数失败，本次不做归一化: {ex.Message}");
                return 1f;
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
