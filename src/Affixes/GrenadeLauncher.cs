using UnityEngine;
using Duckov;
using Duckov.Scenes;
using ItemStatsSystem;
using ItemStatsSystem.Items;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 手雷发射工具：让指定角色向目标位置投掷物品栏中的投掷物
    /// </summary>
    public static class GrenadeLauncher
    {
        private const string LogTag = "[EliteEnemies.GrenadeLauncher]";
        /// <summary>
        /// 让指定角色向目标位置投掷
        /// </summary>
        public static void LaunchGrenade(CharacterMainControl attacker, int itemId, Vector3 targetPos, float delay = 1.5f, bool canHurtSelf = false)
        {
            if (attacker == null) return;

            Item item = ItemAssetsCollection.InstantiateSync(itemId);
            if (item == null)
            {
                Debug.LogWarning($"{LogTag} 无效的物品 ID: {itemId}");
                return;
            }

            Skill_Grenade skill = item.GetComponent<Skill_Grenade>();
            if (skill == null)
            {
                Debug.LogWarning($"{LogTag} 物品 {item.DisplayName} (ID:{itemId}) 不包含 Skill_Grenade 组件");
                return;
            }

            skill.canHurtSelf = canHurtSelf;
            skill.delay = delay;

            // 指定落点
            SkillReleaseContext context = new SkillReleaseContext
            {
                releasePoint = targetPos
            };

            skill.ReleaseSkill(context, attacker);
        }

        /// <summary>
        /// 直接向玩家当前位置发射
        /// </summary>
        public static void LaunchGrenadeAtPlayer(CharacterMainControl attacker, int itemId, float delay = 1.5f)
        {
            if (LevelManager.Instance?.MainCharacter == null) return;
            Vector3 playerPos = LevelManager.Instance.MainCharacter.transform.position;
            LaunchGrenade(attacker, itemId, playerPos, delay);
        }
    }
}
