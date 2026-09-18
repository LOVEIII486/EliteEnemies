using System.Collections.Generic;
using Duckov.Utilities;
using EliteEnemies.Localization;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 小偷：命中玩家时**从玩家背包里偷走一件物品**，塞进自己背包；打死它就掉出来（两件）。
    ///
    /// <para><b>为什么是"偷物资"而不是"偷钱"</b>：这游戏的玩家出门通常不带现金、
    /// 只带物资，抢钱等于白板。</para>
    ///
    /// <para><b>"击杀掉 2 件"怎么成立</b>：偷的时候把原物塞进小偷背包，**再补一件同类**。
    /// 敌人死亡时背包内容会整体进掉落箱（<c>InteractableLootbox.CreateFromItem</c> 会遍历
    /// <c>item.Inventory</c>），所以这两件都会掉出来。</para>
    ///
    /// <para><b>只碰背包、不碰装备</b>：装备槽是另一套容器（<c>Item.Slots</c>），
    /// 不在 <c>Inventory.Content</c> 里——所以永远不会偷走玩家正拿着的枪或穿着的甲。</para>
    /// </summary>
    public class ThiefBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Thief";

        /// <summary>两次行窃之间至少隔多久。不节流的话一轮交火能把玩家背包掏空。</summary>
        private const float StealInterval = 10f;

        private const string PopTextKey = "EliteEnemies_Affix_Thief_PopText";

        private float _lastStealTime = -999f;

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            if (Time.time < _lastStealTime + StealInterval) return;

            CharacterMainControl player = CharacterMainControl.Main;
            if (player == null) return;

            // 用行为自己的宿主，而不是回调参数——被偷的东西要落到**这个**精英身上
            CharacterMainControl thief = Ctx?.Character;
            if (thief == null) return;

            Item playerBag = player.CharacterItem;
            Item thiefBag = thief.CharacterItem;
            if (playerBag == null || thiefBag == null) return;

            // 冷却无条件生效（哪怕这次没东西可偷）：
            // OnHitPlayer 是**每颗弹丸**触发一次，霰弹枪一枪就是 8 次——
            // 不做门控的话，玩家背包空着时每次命中都要白跑一遍挑选。
            _lastStealTime = Time.time;

            Item stolen = PickStealable(playerBag.Inventory);
            if (stolen == null) return;   // 背包里没有能偷的（玩家背包空着）

            int stolenTypeId = stolen.TypeID;

            stolen.Detach();
            if (!thiefBag.Inventory.AddAndMerge(stolen, 0))
            {
                // 小偷背包塞不下（满 15 格）——还回去，别把玩家的东西弄丢
                playerBag.Inventory.AddAndMerge(stolen, 0);
                return;
            }

            // "击杀掉 2 件"的那第二件。生成同类物品再塞进同一个背包，
            // 死亡时随掉落箱一起出来。
            Item bonus = ItemAssetsCollection.InstantiateSync(stolenTypeId);
            if (bonus != null)
            {
                bonus.Initialize();
                bonus.Detach();
                thiefBag.Inventory.AddAndMerge(bonus, 0);
            }

            // 反馈：光"背包里少了一件"玩家未必立刻察觉，得让他看见是谁干的
            thief.PopText(LocalizationManager.GetText(PopTextKey));
        }

        /// <summary>
        /// 从背包内容里随机挑一件能偷的。
        ///
        /// <para>过滤掉三类：<c>Sticky</c>（本来就是"不掉落"标记）、插在槽里的（保险）、
        /// 以及会在掉落箱里被销毁的标签——最后这条最关键：漏了它就会出现
        /// "说好打死能拿回来、结果拿不回来"。</para>
        ///
        /// <para>⚠ <b>刻意不用 <c>List</c> 收集候选</b>：本方法跑在命中回调里，
        /// 那里一条命中间隔可能只有几十毫秒，临时集合就是白白的 GC 压力。
        /// 两趟扫描（先数、再取第 N 个）在背包这个量级上更快，而且零分配。</para>
        /// </summary>
        private static Item PickStealable(Inventory bag)
        {
            if (bag == null) return null;

            List<Item> content = bag.Content;
            if (content == null) return null;

            int count = 0;
            for (int i = 0; i < content.Count; i++)
            {
                if (IsStealable(content[i])) count++;
            }
            if (count == 0) return null;

            int pick = Random.Range(0, count);
            for (int i = 0; i < content.Count; i++)
            {
                Item item = content[i];
                if (!IsStealable(item)) continue;
                if (pick-- == 0) return item;
            }
            return null;
        }

        /// <summary>这件东西能不能偷。<b>上面那两趟扫描必须用同一套判据</b>，否则会取空。</summary>
        private static bool IsStealable(Item item)
        {
            if (item == null) return false;
            if (item.Sticky) return false;
            if (item.PluggedIntoSlot != null) return false;
            if (item.Tags.Contains(GameplayDataSettings.Tags.DestroyOnLootBox)) return false;
            if (item.Tags.Contains("DestroyInBase")) return false;
            return true;
        }
    }
}
