using UnityEngine;
using Duckov;
using Duckov.Scenes;
using ItemStatsSystem;
using ItemStatsSystem.Items;

namespace EliteEnemies.EliteEnemy.AffixBehaviors
{
    /// <summary>
    /// 精英怪辅助工具类，封装通用的逻辑
    /// </summary>
    public static class EliteBehaviorHelper
    {
        private const string LogTag = "[EliteEnemies.EliteBehaviorHelper]";
        /// <summary>
        /// 让指定角色向目标位置投掷/发射物品（基于游戏原生 Skill_Grenade 系统）
        /// </summary>
        /// <param name="attacker">攻击者（精英怪）</param>
        /// <param name="itemId">投掷物 Item ID (如 67=手雷, 941=燃烧弹)</param>
        /// <param name="targetPos">目标世界坐标</param>
        /// <param name="delay">引爆延迟（秒），默认 2.0s</param>
        /// <param name="canHurtSelf">是否误伤自己，默认 false</param>
        public static void LaunchGrenade(CharacterMainControl attacker, int itemId, Vector3 targetPos, float delay = 2.0f, bool canHurtSelf = false)
        {
            if (attacker == null) return;

            // 1. 生成逻辑物品实例
            Item item = ItemAssetsCollection.InstantiateSync(itemId);
            if (item == null)
            {
                Debug.LogWarning($"{LogTag} 无效的物品 ID: {itemId}");
                return;
            }

            // 2. 获取技能组件
            Skill_Grenade skill = item.GetComponent<Skill_Grenade>();
            if (skill == null)
            {
                Debug.LogWarning($"{LogTag} 物品 {item.DisplayName} (ID:{itemId}) 不包含 Skill_Grenade 组件");
                return;
            }

            // 3. 配置技能参数
            skill.canHurtSelf = canHurtSelf;
            skill.delay = delay;
            
            // 4. 构造释放上下文（指定落点）
            SkillReleaseContext context = new SkillReleaseContext
            {
                releasePoint = targetPos
            };

            // 5. 执行释放 (触发投掷动作和物理生成)
            skill.ReleaseSkill(context, attacker);
        }

        /// <summary>
        /// 快捷方法：直接向玩家当前位置发射
        /// </summary>
        public static void LaunchGrenadeAtPlayer(CharacterMainControl attacker, int itemId, float delay = 2.0f)
        {
            if (LevelManager.Instance?.MainCharacter == null) return;
            Vector3 playerPos = LevelManager.Instance.MainCharacter.transform.position;
            LaunchGrenade(attacker, itemId, playerPos, delay);
        }
    }
}