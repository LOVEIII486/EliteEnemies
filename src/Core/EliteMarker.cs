using System.Collections.Generic;
using EliteEnemies.Combos;
using UnityEngine;

namespace EliteEnemies.Core
{
    /// <summary>
    /// 精英标记组件：挂在敌人身上，记录其基础名称、词条列表与 combo 归属。
    /// </summary>
    public class EliteMarker : MonoBehaviour
    {
        public string BaseName;
        public List<string> Affixes = new List<string>();

        /// <summary>combo 精英的 combo 定义；非 combo 精英为 null。</summary>
        private EliteComboDefinition _combo;

        /// <summary>把这只敌人标记为 combo 精英。生成时由 <c>AffixSelector</c> 调用。</summary>
        public void SetCombo(EliteComboDefinition combo) => _combo = combo;

        /// <summary>
        /// combo 精英的显示名（带色号）；非 combo 精英返回 null。
        ///
        /// <para><b>存 combo 定义、现算名字，而不是存生成时那一刻的字符串快照。</b>
        /// 快照的毛病是无声的：切换语言后，**当时已经活着**的 combo 精英会一直顶着
        /// 旧语言的称号，直到它被销毁重建为止。存引用则由
        /// <see cref="EliteComboDefinition.GetColoredTitle"/> 跟着语言版本走。</para>
        ///
        /// <para>⚠ <b>不要把它改成现拼字符串。</b> 血条的
        /// <c>EliteHealthBarUI.Update</c> 里有一段对**非精英**敌人每帧执行、且会读到本属性的
        /// 判定（<c>EliteHealthBarUI.LateUpdate</c>，<c>EliteHealthBarUI.cs:77-85</c>）。现拼就等于给地图上每个非精英敌人
        /// 每帧分配一个字符串。非 combo 精英在这里直接由 <c>?.</c> 短路，不做任何工作。</para>
        /// </summary>
        public string CustomDisplayName => _combo?.GetColoredTitle();
    }
}
