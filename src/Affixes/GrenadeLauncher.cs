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
        /// <param name="target">**砸向哪个玩家**。由调用方给出，**不要在这里取
        /// <c>LevelManager.Instance.MainCharacter</c>**——那是「本机玩家」，
        /// 而联机下挨打的常常是**客机玩家**（主机上判定），写死 Main 会让手雷
        /// 永远只砸主机玩家。详见 <c>Docs/Coop/05-integration-gotchas.md</c> §11。</param>
        public static void LaunchGrenadeAtPlayer(CharacterMainControl attacker, CharacterMainControl target,
                                                 int itemId, float delay = 1.5f)
        {
            if (target == null) return;

            LaunchGrenade(attacker, itemId, target.transform.position, delay);
        }
    }
}
