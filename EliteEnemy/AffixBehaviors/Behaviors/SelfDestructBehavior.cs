using UnityEngine;

namespace EliteEnemies.AffixBehaviors
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
        
        private Renderer[] _cachedRenderers; 
        private MaterialPropertyBlock _propBlock;
        private int _emissionColorId;
        

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            base.OnEliteInitialized(character);
    
            // 1. 获取 Shader 属性 ID
            _emissionColorId = Shader.PropertyToID("_EmissionColor");
    
            // 2. 初始化属性块
            _propBlock = new MaterialPropertyBlock();
    
            // 3. 获取所有子物体的渲染器
            _cachedRenderers = character.GetComponentsInChildren<Renderer>();
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

            // 修复：传入 null 作为伤害来源，防止 AI 追溯已销毁的对象
            // 原代码：var dmgInfo = new DamageInfo(deadCharacter)
            var dmgInfo = new DamageInfo(null) 
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
                false 
            );
        }

        private void UpdateInstabilityEffect(CharacterMainControl character)
        {
            if (character == null) return;

            // --- 缩放效果  ---
            float pulseSpeed = 15f; 
            float jitterAmount = 0.08f;
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.05f;
            float noise = UnityEngine.Random.Range(-jitterAmount, jitterAmount);
            character.transform.localScale = Vector3.one * (1f + pulse + noise);

            // --- 颜色闪烁效果 ---
            if (_cachedRenderers != null && _cachedRenderers.Length > 0)
            {
                // 计算颜色
                float emissionStrength = Mathf.PingPong(Time.time * 5f, 1f); 
                Color targetColor = Color.red * emissionStrength * 2f;

                // 遍历所有部件
                foreach (var renderer in _cachedRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.GetPropertyBlock(_propBlock);
                        _propBlock.SetColor(_emissionColorId, targetColor);
                        renderer.SetPropertyBlock(_propBlock);
                    }
                }
            }
        }
        
        public override void OnCleanup(CharacterMainControl character)
        {
        }
    }
}