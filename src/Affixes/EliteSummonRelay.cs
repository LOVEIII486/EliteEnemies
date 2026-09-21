using UnityEngine;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// **召唤体显示名**在主机与客机之间转交的中立出口。
    ///
    /// <para><b>为什么需要它</b>：本模组给召唤体（鸡哥的小鸡、鸳鸯/守护的伴侣）
    /// 套的是一份<b>自定义显示名</b>，而那份额名字挂在<b>预设副本</b>上
    /// （<see cref="Core.EggSpawnHelper"/> 的 <c>CreateModifiedPreset</c>：
    /// <c>showName = true</c> + <c>SetOverrideText(nameKey, 名字)</c>）。
    /// 副本是主机<b>运行期</b> <c>Instantiate</c> 出来的，客机上<b>根本不存在那个对象</b>。</para>
    ///
    /// <para>而联机模组过网的字段是 <c>CharacterPresetKey = nameKey ?? name</c>
    /// （<c>AISyncService.cs:1485-1487</c>），客机拿着那个<b>带我们后缀的键</b>去本地解析
    /// （<c>ResolveCharacterPreset</c>，<c>:3680-3708</c>，按 <c>nameKey</c>/<c>name</c> 精确匹配）
    /// ⇒ <b>必然解析失败</b>。失败后它走兜底
    /// （<c>:3740-3745</c>：<c>cmc.characterPreset = CharacterMainControl.Main.characterPreset</c>），
    /// 而复制体是 <c>CharacterCreator.CreateCharacter</c> 造的、那条路<b>不设</b> <c>characterPreset</c>
    /// ⇒ 兜底生效，<b>客机的召唤体挂着"本机玩家的预设"</b>。症状就是名字不显示
    /// （<c>HealthBar.cs:269</c> 的 <c>preset.showName</c> 为假），顺带血条图标也是玩家的
    /// （<c>HealthBar.cs:259</c>）。</para>
    ///
    /// <para><b>所以这里传的是什么</b>：<b>不传渲染好的名字</b>，只传"怎么拼"的三样——
    /// 名字的本地化键、基预设的标识、要作为前缀的那个预设的标识。客机据此
    /// <b>在自己那门语言下重拼</b>（<c>AGENT.md §3.5</c>：传译文等于把主机那门语言焊死）。</para>
    ///
    /// <para>⚠ <b>刻意不传"召唤者的 aiId"</b>：那个前缀取自召唤者的
    /// <c>characterPreset.DisplayName</c>，而它<b>只取决于预设</b>、不取决于哪一只精英
    /// ——传预设标识就没有"召唤者还没同步过来"的时序问题（传 aiId 的话，
    /// 主人的 <c>AiSpawned</c> 比召唤体早不了多少，会多出一类"晚到就少个前缀"的静默差异）。</para>
    ///
    /// <para><b>与 <see cref="EliteStateRelay"/> 同一形状</b>：词条行为不认识联机模块
    /// （依赖方向），所以这里只留一个"口子"——主机侧把信息<b>报出去</b>。
    /// <b>没装联机模组 ⇒ 恒不接管 ⇒ 单机一行行为都不变。</b></para>
    /// </summary>
    internal static class EliteSummonRelay
    {
        /// <summary>
        /// 主机侧：本模组刚召唤出一个<b>带自定义名字</b>的敌人。
        ///
        /// <para>参数用<b>角色对象</b>而不是 aiId——调用方（词条行为）手上只有刚生成的角色，
        /// 而"角色 ↔ aiId"的映射是联机模块自己的知识：联机模组的
        /// <c>AiSpawned</c> 要等这只 AI 初始化完 <b>800ms</b> 才来，
        /// 所以这里只<b>记账</b>，等那个事件到了再广播。</para>
        ///
        /// <para><paramref name="prefixPresetResourceName"/> 为空 = 名字不带头部
        /// （小鸡就是这种）；非空 = 名字拼成 <c>{那个预设的 DisplayName} ({键的文本})</c>。</para>
        ///
        /// <para>返回 <c>true</c> = 联机模块已接管。调用方目前不看返回值（本机该做的事与它无关），
        /// 保留它是为了与 <see cref="EliteStateRelay"/> 的口子形状一致。</para>
        ///
        /// <para><b>默认 null ⇒ 什么都不做 ⇒ 单机与从前一字不差。</b></para>
        /// </summary>
        public static System.Func<CharacterMainControl, string, string, string, bool> SummonNameHandler
        {
            get;
            set;
        }

        /// <summary>
        /// 一个预设的**跨机标识**：<c>nameKey</c> 优先，空则退回资源名 <c>name</c>。
        ///
        /// <para>⚠ <b>与联机模组自己的取值口径逐字一致</b>——它过网的
        /// <c>CharacterPresetKey</c> 就是 <c>string.IsNullOrEmpty(nameKey) ? name : nameKey</c>
        /// （<c>AISyncService.cs:1485-1487</c>）。跟着它走有两个好处：
        /// ① 它那边的解析 <c>IsPresetMatch</c>（<c>:3719-3724</c>）也是"先比 nameKey、再比 name"；
        /// ② <b>绕开 <c>(Clone)</c></b>——<c>Instantiate</c> 只会把 <c>name</c> 改成
        /// <c>"XXX(Clone)"</c>，<c>nameKey</c> 是序列化字段、不受影响，
        /// 所以运行期被克隆过的预设用 nameKey 仍然找得回原对象。</para>
        /// </summary>
        public static string PresetKey(CharacterRandomPreset preset)
        {
            if (preset == null) return null;
            return string.IsNullOrEmpty(preset.nameKey) ? preset.name : preset.nameKey;
        }

        /// <summary>
        /// 主机侧：上报一个召唤体的显示名信息。<paramref name="summon"/> 是刚生成的召唤体。
        /// </summary>
        public static void RelaySummonName(CharacterMainControl summon,
                                           string nameKey,
                                           string basePresetResourceName,
                                           string prefixPresetResourceName)
        {
            var handler = SummonNameHandler;
            if (handler == null) return;
            if (summon == null || string.IsNullOrEmpty(nameKey)) return;

            handler(summon, nameKey, basePresetResourceName, prefixPresetResourceName);
        }
    }
}
