using System.Collections.Generic;
using System.Text;
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

        /// <summary>弹幕文案。值是格式串：<c>{0}</c> = 颜色十六进制、<c>{1}</c> = 物品名。</summary>
        private const string PopTextKey = "EliteEnemies_Affix_Thief_PopText";

        /// <summary>
        /// 物品名在弹幕里允许占的**显示宽度**（半角字符算 1、全角算 2），超出就截断加省略号。
        ///
        /// <para>用显示宽度而不是字符数，是因为这个模组要同时伺候中英俄：
        /// 按字符数限制的话，中文 12 字已经很长、英文 12 字却只有 "Assault Rif"。
        /// 28 列 ≈ 14 个汉字或 28 个半角字符。</para>
        /// </summary>
        private const int MaxNameWidth = 28;

        private float _lastStealTime = -999f;

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (Time.time < _lastStealTime + StealInterval) return;

            // ⚠ 目标必须是 **victim（被打中的那个玩家）**，不是 `CharacterMainControl.Main`。
            //   联机下判定在主机上跑，而挨打的往往是**客机玩家的复制体**；
            //   写死 Main 会把效果挂到主机自己的玩家身上，客机什么都看不到。
            CharacterMainControl player = victim;
            if (player == null) return;

            // 用行为自己的宿主，而不是回调参数——被偷的东西要落到**这个**精英身上
            CharacterMainControl thief = Ctx?.Character;
            if (thief == null) return;

            // 联机下：从玩家背包里出去的那一半只能由他自己那台机器做
            //（主机拿不到客机的完整背包）。做得成不成会回报回来，
            // 主机再按回报把战利品加到精英身上（见 OnRemoteStealCompleted）。
            if (PlayerEffectRelay.TryRelaySteal(player, thief))
            {
                _lastStealTime = Time.time;   // 冷却照走，避免每颗弹丸都发一次请求
                return;
            }

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

            // 反馈：光"背包里少了一件"玩家未必立刻察觉，得让他看见**是谁、拿走了什么**。
            // 文案里的 {0}/{1} 由这里填：稀有度配色 + 截断过的物品名。
            string pop = string.Format(
                LocalizationManager.GetText(PopTextKey),
                QualityColor(stolen.DisplayQuality),
                Shorten(stolen.DisplayName, MaxNameWidth));
            thief.PopText(pop);
        }

        /// <summary>
        /// **联机**：客户端偷完回报之后，由主机把东西加到精英身上。
        ///
        /// <para>偷窃被拆成两半：<b>从玩家背包里出去</b>的那一半只能由客机做
        /// （主机那个只是复制体），而<b>加给精英</b>这一半在主机做（精英是主机权威的）。
        /// 这里就是后一半。被偷的只有 typeId 可用——按它重新实例化一件同类型的。</para>
        /// </summary>
        internal static void OnRemoteStealCompleted(CharacterMainControl thief, int stolenTypeId)
        {
            if (thief == null || stolenTypeId <= 0) return;

            var thiefBag = thief.CharacterItem;
            if (thiefBag == null || thiefBag.Inventory == null) return;

            try
            {
                Item stolen = ItemAssetsCollection.InstantiateSync(stolenTypeId);
                if (stolen == null) return;

                stolen.Initialize();
                stolen.Detach();
                if (!thiefBag.Inventory.AddAndMerge(stolen, 0))
                {
                    // 小偷背包塞不下——东西已经在客机那边扣掉了，但这里放不进去。
                    // **不静默**：这是玩家会察觉的损失（东西没了却没进小偷包里）。
                    Debug.LogWarning($"[EliteEnemies.Thief] 联机偷窃：小偷背包放不下 typeId={stolenTypeId}，" +
                                     "该物品已从玩家背包移出但未能进入小偷背包");
                    return;
                }

                // "击杀掉 2 件"的那第二件（与单机路径同一规则）。
                Item bonus = ItemAssetsCollection.InstantiateSync(stolenTypeId);
                if (bonus != null)
                {
                    bonus.Initialize();
                    bonus.Detach();
                    thiefBag.Inventory.AddAndMerge(bonus, 0);
                }

                string pop = string.Format(
                    LocalizationManager.GetText(PopTextKey),
                    QualityColor(stolen.DisplayQuality),
                    Shorten(stolen.DisplayName, MaxNameWidth));
                thief.PopText(pop);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EliteEnemies.Thief] 联机偷窃：把战利品加给精英时出错（已隔离）: {ex.Message}");
            }
        }

        /// <summary>
        /// 稀有度对应的十六进制颜色（不带 <c>#</c>，供 TMP 的 <c>&lt;color=#{0}&gt;</c> 用）。
        ///
        /// <para><b>为什么用自带配色表，而不是游戏那个 <c>UIStyle.GetDisplayQualityLook</c></b>：
        /// 后者取的是 <c>DisplayQualityLook.shadowColor</c>——那是**图标背后的辉光**，
        /// 不是文本色；而且某个品质没配资产时它会返回 <see cref="Color.black"/> 兜底
        /// （<c>GameplayDataSettings.cs:605/689</c>），弹幕上就是一坨纯黑、直接看不见。
        /// 这个失败模式没法离线验证，不如自己定一张对比度可控的表。</para>
        ///
        /// <para>取值对齐 <c>DisplayQuality</c> 枚举自身的命名（White/Green/Blue/…），
        /// 都挑的亮色，深色气泡上也读得清。</para>
        /// </summary>
        private static string QualityColor(DisplayQuality quality)
        {
            switch (quality)
            {
                case DisplayQuality.Green: return "5CD65C";
                case DisplayQuality.Blue: return "4DA6FF";
                case DisplayQuality.Purple: return "B266FF";
                case DisplayQuality.Orange: return "FFA64D";
                case DisplayQuality.Red: return "FF5C5C";
                case DisplayQuality.Q7: return "FFD700";
                case DisplayQuality.Q8: return "FF7AD9";
                default: return "FFFFFF";   // None / White，以及将来新增的档位
            }
        }

        /// <summary>
        /// 把物品名压到 <paramref name="maxWidth"/> 个显示宽度以内：
        /// **剥掉富文本标记**（它们不计宽度，也不该出现在结果里），超长则截断加省略号。
        ///
        /// <para>剥标记这步不能省：物品名来自本地化、且可能是别的模组写的，
        /// 里面带 <c>&lt;color=…&gt;</c> 这类标记是常态。直接在原文上按位置截断，
        /// 会从标签中间切断，弹幕上就会冒出一截裸标签。</para>
        /// </summary>
        private static string Shorten(string raw, int maxWidth)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            // 用 Mathf.Min 而不是 Math.Min：本文件不能 `using System;`——
            // 那会把 System.Random 引进来，与 UnityEngine.Random 撞名（CS0104，实测踩过）。
            var sb = new StringBuilder(Mathf.Min(raw.Length, maxWidth) + 1);
            int width = 0;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];

                if (c == '<')
                {
                    int close = raw.IndexOf('>', i + 1);
                    if (close >= 0)
                    {
                        i = close;      // 整个标记跳过：不计数、不输出
                        continue;
                    }
                }

                int w = IsWide(c) ? 2 : 1;
                if (width + w > maxWidth)
                {
                    sb.Append('…');
                    break;
                }

                sb.Append(c);
                width += w;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 这个字符是否按两个宽度算（CJK、谚文、全角标点等）。
        /// 近似 East Asian Width 的 Wide/Fullwidth 区段，够用且不引依赖。
        /// </summary>
        private static bool IsWide(char c)
        {
            return c >= 0x1100
                && (c <= 0x115F                                   // 谚文字母
                    || c == 0x2329 || c == 0x232A
                    || (c >= 0x2E80 && c <= 0xA4CF && c != 0x303F) // CJK 部首 ~ 彝文
                    || (c >= 0xAC00 && c <= 0xD7A3)               // 谚文音节
                    || (c >= 0xF900 && c <= 0xFAFF)               // CJK 兼容表意
                    || (c >= 0xFE30 && c <= 0xFE6F)               // CJK 兼容形式
                    || (c >= 0xFF00 && c <= 0xFF60)               // 全角形式
                    || (c >= 0xFFE0 && c <= 0xFFE6));
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
        /// <summary>
        /// 从背包里挑一件可偷的。
        ///
        /// <para>⚠ <b>改成 internal 是为了联机复用</b>：联机下主机拿不到客机的完整背包，
        /// 只能由<b>客机那边</b>挑并回报偷了什么。挑选标准必须**只有一份**，
        /// 否则两端会漂移（客机觉得能偷、主机觉得不能）。
        /// 调用点是 <c>CoopPlayerEffect.ExecuteSteal</c>。</para>
        /// </summary>
        internal static Item PickStealable(Inventory bag)
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
