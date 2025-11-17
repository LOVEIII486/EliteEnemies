using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【自爆】词缀 - 敌人死亡时在死亡位置产生爆炸
    /// </summary>
    public class SelfDestructBehavior : AffixBehaviorBase
    {
        public override string AffixName => "Explosive";

        private static readonly float ExplosionRadius = 5f; // 爆炸半径
        private static readonly float ExplosionDamage = 20f; // 爆炸伤害
        private static readonly float ArmorPiercing = 10f; // 破甲值
        private static readonly float ExplosionForce = 2f; // 爆炸冲击力
        // 目前似乎只有 normal 和 flash 可选
        private static readonly ExplosionFxTypes ExplosionType = ExplosionFxTypes.normal; // 爆炸效果类型
        private static readonly int WeaponItemID = 24; // 武器ID（用于伤害来源标识）

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;
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

            // 封装伤害信息
            var dmgInfo = new DamageInfo(deadCharacter)
            {
                damageValue = ExplosionDamage,
                fromWeaponItemID = WeaponItemID,
                armorPiercing = ArmorPiercing
            };

            // 创建爆炸
            LevelManager.Instance.ExplosionManager.CreateExplosion(
                position, // 爆炸中心位置
                ExplosionRadius, // 爆炸半径
                dmgInfo, // 伤害信息
                ExplosionType, // 爆炸特效类型
                ExplosionForce, // 爆炸冲击力
                false // 是否伤害自身
            );
        }

        public override void OnCleanup(CharacterMainControl character)
        {
        }
    }
}