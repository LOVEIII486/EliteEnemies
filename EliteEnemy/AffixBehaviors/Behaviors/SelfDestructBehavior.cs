using EliteEnemies.EliteEnemy.VisualEffects;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【自爆】词缀 - 敌人死亡时在死亡位置产生爆炸
    /// </summary>
    public class SelfDestructBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Explosive";

        private static readonly float ExplosionRadius = 5f; // 爆炸半径
        private static readonly float ExplosionDamage = 20f; // 爆炸伤害
        private static readonly float ArmorPiercing = 10f; // 破甲值

        private static readonly float ExplosionForce = 2f; // 爆炸冲击力

        // 目前似乎只有 normal 和 flash 可选
        private static readonly ExplosionFxTypes ExplosionType = ExplosionFxTypes.normal; // 爆炸效果类型
        private static readonly int WeaponItemID = 24; // 武器ID（用于伤害来源标识）
        
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
            var deathPosition = character.transform.position;
 
            CreateExplosion(deathPosition, character);
        }
        
        /// <summary>
        /// 创建爆炸效果
        /// </summary>
        private void CreateExplosion(Vector3 position, CharacterMainControl deadCharacter)
        {
            if (LevelManager.Instance == null || LevelManager.Instance.ExplosionManager == null)
            {
                return;
            }

            // var player = CharacterMainControl.Main; 
            //
            //
            // var source = player != null ? player : null;

            var dmgInfo = new DamageInfo(deadCharacter)
            {
                damageValue = ExplosionDamage,
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
            float pulse = Mathf.Sin(Time.time * 15f) * 0.05f;
            float noise = UnityEngine.Random.Range(-0.08f, 0.08f);
            character.transform.localScale = Vector3.one * (1f + pulse + noise);

            // 颜色闪烁
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