using EliteEnemies.Visuals;
using EliteEnemies.Core;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【自爆】词缀 - 敌人死亡时产生爆炸，伤害基于玩家最大生命值动态计算
    /// </summary>
    public class SelfDestructBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Explosive";

        // 基础配置
        private static readonly float ExplosionRadius = 5f;       // 爆炸半径
        private static readonly float MinExplosionDamage = 10f;   // 最低保底伤害
        private static readonly float MaxExplosionDamage = 30f;   // 最高伤害
        private static readonly float DamagePercentOfMaxHp = 0.25f; // 伤害倍率：造成玩家最大生命值 25% 的伤害
        private static readonly float ArmorPiercing = 2f;        // 破甲值
        private static readonly float ExplosionForce = 5f;        // 爆炸冲击力
        private static readonly int WeaponItemID = 24;            // 武器ID（用于伤害来源标识）

        // 特效配置
        private static readonly ExplosionFxTypes ExplosionType = ExplosionFxTypes.normal;

        private EliteGlowController _glowController;

        /// <summary>颜色闪烁的写入门控间隔（秒）。见 <see cref="UpdateInstabilityEffect"/> 的注释。</summary>
        private const float GlowWriteInterval = 0.05f;
        private float _nextGlowWriteTime;
        private Color _lastGlowColor;

        /// <summary>被本词条改之前的缩放，清理时还原（本词条每帧都在写 localScale）。</summary>
        private Vector3 _originalScale = Vector3.zero;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _glowController = new EliteGlowController(character);

            // 记下原始缩放：本词条每帧都在改 localScale，清理时要还原回去
            if (character != null) _originalScale = character.transform.localScale;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            UpdateInstabilityEffect(character);
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character == null) return;

            // 获取死亡位置并生成爆炸
            var deathPosition = character.transform.position;
            CreateExplosion(deathPosition, character, damageInfo.fromCharacter);
        }

        /// <summary>
        /// 创建动态伤害的爆炸效果
        /// </summary>
        /// <param name="killer">击杀者。**爆炸伤害按他的血量上限算**——理由见方法内的注释。</param>
        private void CreateExplosion(Vector3 position, CharacterMainControl deadCharacter, CharacterMainControl killer)
        {
            if (LevelManager.Instance == null || LevelManager.Instance.ExplosionManager == null)
            {
                return;
            }

            // ⚠ 参考血量取**击杀者**的，不是 `CharacterMainControl.Main`（本机玩家）。
            //   原先写死 Main：单人下击杀者通常就是本机玩家，所以看不出问题；
            //   联机下击杀者多半是**客机玩家**，而 Main 是主机玩家 ⇒ 伤害按错的人算。
            //
            //   击杀者取不到时（例如环境伤害致死）退回 Main——**保住单机原有的行为**，
            //   不让这条改动改变单人下的任何数值。
            var reference = killer != null ? killer : CharacterMainControl.Main;
            float calculatedDamage = MinExplosionDamage;

            // 动态计算伤害
            if (reference != null && reference.Health != null)
            {
                float dynamicDamage = reference.Health.MaxHealth * DamagePercentOfMaxHp;
                calculatedDamage = Mathf.Clamp(dynamicDamage,MinExplosionDamage,MaxExplosionDamage);
            }

            var dmgInfo = new DamageInfo(deadCharacter)
            {
                damageValue = calculatedDamage,
                fromWeaponItemID = WeaponItemID,
                armorPiercing = ArmorPiercing
            };

            LevelManager.Instance.ExplosionManager.CreateExplosion(
                position,
                ExplosionRadius,
                dmgInfo,
                ExplosionType,
                ExplosionForce,
                true
            );
        }

        private void UpdateInstabilityEffect(CharacterMainControl character)
        {
            // 身体脉冲膨胀效果
            float pulse = Mathf.Sin(Time.time * 15f) * 0.05f;
            float noise = UnityEngine.Random.Range(-0.08f, 0.08f);
            character.transform.localScale = Vector3.one * (1f + pulse + noise);

            // 颜色闪烁警示。
            //
            // ⚠ **必须门控**：`SetEmissionColor` 会遍历该角色的**全部**渲染器
            // （含装备、武器、未激活部件，见 `EliteGlowController` 的 `GetComponentsInChildren`）
            // 逐个 `GetPropertyBlock` + `SetPropertyBlock`。而闪烁本身是
            // `PingPong(Time.time * 5f, 1f)`——**周期 0.4 秒**，按 20Hz 采样已远超它，
            // 视觉上无从分辨，写入次数却降到约 1/3（60fps → 20Hz）。
            //
            // 这是**已经修过的同一模式**（`MimicBehavior` 的"每帧不再重复写 Renderer"，`b8962a1`）
            // 在本模块的漏网之鱼（`docs\Visuals模块代码审查.md` V4）。
            if (Time.time < _nextGlowWriteTime) return;
            _nextGlowWriteTime = Time.time + GlowWriteInterval;

            float emissionStrength = Mathf.PingPong(Time.time * 5f, 1f);
            Color targetColor = Color.red * emissionStrength * 2f;

            // 颜色没变就不写——闪烁在 PingPong 的两个极值附近会连续采样到同一个值。
            if (targetColor == _lastGlowColor) return;
            _lastGlowColor = targetColor;

            _glowController.SetEmissionColor(targetColor);
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _glowController?.Reset();

            // 每帧的脉冲缩放必须还原：精英被 StripElite（撤销精英化但角色还活着）时，
            // 不还原就会带着一个随机的缩放残留。原先这里只 Reset 了光效。
            //
            // ⚠ 本词条与 `GigantificationBehavior` 都直接写 localScale，两个词条同时出现时
            // 会互相覆盖（互斥表里没有这一对）。这里只负责把自己写的那份还原，不解决冲突——
            // 那是玩法问题，见 docs\词条模块审查与设计.md §6.4。
            if (character != null && _originalScale != Vector3.zero)
            {
                character.transform.localScale = _originalScale;
            }
        }
    }
}
