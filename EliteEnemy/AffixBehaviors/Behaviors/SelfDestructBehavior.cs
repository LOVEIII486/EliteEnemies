using EliteEnemies.EliteEnemy.VisualEffects;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【自爆】词缀 - 敌人死亡时产生爆炸，伤害基于玩家最大生命值动态计算
    /// </summary>
    public class SelfDestructBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Explosive";

        // 基础配置
        private static readonly float ExplosionRadius = 5f;       // 爆炸半径
        private static readonly float MinExplosionDamage = 12f;   // 最低保底伤害
        private static readonly float MaxExplosionDamage = 35f;   // 最高伤害
        private static readonly float DamagePercentOfMaxHp = 0.30f; // 伤害倍率：造成玩家最大生命值 30% 的伤害
        private static readonly float ArmorPiercing = 2f;        // 破甲值
        private static readonly float ExplosionForce = 5f;        // 爆炸冲击力
        private static readonly int WeaponItemID = 24;            // 武器ID（用于伤害来源标识）
        
        // 特效配置
        private static readonly ExplosionFxTypes ExplosionType = ExplosionFxTypes.normal; 
        
        private EliteGlowController _glowController;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            base.OnEliteInitialized(character);
            _glowController = new EliteGlowController(character);
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
            CreateExplosion(deathPosition, character);
        }
        
        /// <summary>
        /// 创建动态伤害的爆炸效果
        /// </summary>
        private void CreateExplosion(Vector3 position, CharacterMainControl deadCharacter)
        {
            if (LevelManager.Instance == null || LevelManager.Instance.ExplosionManager == null)
            {
                return;
            }
            
            var mainPlayer = CharacterMainControl.Main;
            float calculatedDamage = MinExplosionDamage;

            // 动态计算伤害
            if (mainPlayer != null && mainPlayer.Health != null)
            {
                float dynamicDamage = mainPlayer.Health.MaxHealth * DamagePercentOfMaxHp;
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

            // 颜色高频闪烁警示
            float emissionStrength = Mathf.PingPong(Time.time * 5f, 1f); 
            Color targetColor = Color.red * emissionStrength * 2f;
            
            _glowController.SetEmissionColor(targetColor);
        }
        
        public override void OnCleanup(CharacterMainControl character)
        {
            _glowController?.Reset();
        }
    }
}