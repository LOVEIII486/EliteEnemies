using System;
using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.Core
{
    /// <summary>
    /// 预设名录：**哪些敌人会被本模组视为精英候选**，以及它们的分类。
    ///
    /// <para>它是「精英化」这个概念的数据面——只回答「这个预设属于哪一类」，
    /// 不参与任何决策（选不选、选几条在 <see cref="EliteEnemyCore"/> 与
    /// <c>AffixSelector</c> 里）。</para>
    ///
    /// <para>从 <see cref="EliteEnemyCore"/> 拆出来的理由只有一个：这些是**字面量表**，
    /// 而改「哪些敌人能精英化」的人不该在 571 行的文件里翻找它们。</para>
    ///
    /// <para><b>数据来源有两条，方向相反，改动前先看清是哪一条：</b></para>
    /// <list type="bullet">
    /// <item>内置分类来自 <see cref="NPCPresetNames"/> 的常量表——编译期固定，
    /// 在静态构造器里一次性灌进来</item>
    /// <item><see cref="TryAutoRegisterExternalPreset"/> 会**往表里追加**外部模组的敌人
    /// ——运行期发现，是唯一的写入路径</item>
    /// </list>
    /// </summary>
    public static class PresetDirectory
    {
        private const string LogTag = "[EliteEnemies.PresetDirectory]";

        #region 预设集合

        internal static readonly HashSet<string> EligiblePresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<string> BossPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<string> MerchantPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<string> IgnoredGenericPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<string> ExternalEligiblePresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static PresetDirectory()
        {
            BossPresets.UnionWith(NPCPresetNames.Boss.All);
            MerchantPresets.UnionWith(NPCPresetNames.Merchant.All);

            EligiblePresets.UnionWith(NPCPresetNames.Enemies.All);
            EligiblePresets.UnionWith(NPCPresetNames.Animal.All);
            EligiblePresets.UnionWith(NPCPresetNames.Minions.All);
            // 测试炮台是否兼容精英化系统
            //EligiblePresets.Add(NPCPresetNames.Special.GunTurret);

            IgnoredGenericPresets.UnionWith(NPCPresetNames.Test.All);
            IgnoredGenericPresets.UnionWith(NPCPresetNames.Unknown.All);
            IgnoredGenericPresets.UnionWith(NPCPresetNames.Special.All);
            IgnoredGenericPresets.UnionWith(NPCPresetNames.Vehicle.All);
            IgnoredGenericPresets.UnionWith(NPCPresetNames.Quest.All);
            // 测试炮台是否兼容精英化系统
            //IgnoredGenericPresets.Remove(NPCPresetNames.Special.GunTurret);

            Debug.Log($"{LogTag} 预设白名单初始化完成。普通敌人: {EligiblePresets.Count}, Boss: {BossPresets.Count}, 商人: {MerchantPresets.Count}, 忽略: {IgnoredGenericPresets.Count}");
        }

        #endregion

        public static readonly HashSet<string> UIHiddenPresets = new HashSet<string>
        {
            NPCPresetNames.Enemies.JLabInvisible
        };

        public static bool IsUIHidden(string presetName)
        {
            return !string.IsNullOrEmpty(presetName) && UIHiddenPresets.Contains(presetName);
        }

        /// <summary>
        /// 检查预设是否满足精英化基础条件
        /// </summary>
        internal static bool IsEligiblePreset(CharacterRandomPreset preset)
        {
            if (preset == null) return false;
            string rName = preset.name;

            // 1. 优先排除强制忽略列表
            if (IgnoredGenericPresets.Contains(rName)) return false;

            // 2. 检查是否在普通敌人或 Boss 列表中
            return EligiblePresets.Contains(rName) || BossPresets.Contains(rName);
        }

        #region 外部预设注册

        /// <summary>
        /// 尝试自动注册未知的外部敌人预设
        /// </summary>
        internal static bool TryAutoRegisterExternalPreset(CharacterRandomPreset preset)
        {
            if (preset == null) return false;

            string rName = preset.name;
            if (EligiblePresets.Contains(rName) ||
                BossPresets.Contains(rName) ||
                MerchantPresets.Contains(rName) ||
                IgnoredGenericPresets.Contains(rName))
                return false;

            if (!LooksLikeEnemyPreset(rName)) return false;
            if (rName.IndexOf("NonElite", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (!ExternalEligiblePresets.Add(rName)) return true;

            EligiblePresets.Add(rName);
            Debug.Log($"{LogTag} 自动发现并注册外部敌人类型: {rName}");
            return true;
        }

        /// <summary>
        /// 判定该资源名是否具有敌人的基本特征
        /// </summary>
        private static bool LooksLikeEnemyPreset(string rName)
        {
            if (string.IsNullOrEmpty(rName)) return false;
            if (rName.Contains("Dummy")) return false;
            if (rName.Contains("MatPreset")) return false;
            if (rName.Contains("PetPreset")) return false;
            if (rName.Contains("Merchant")) return false;
            if (rName.Contains("QuestGiver")) return false;
            return true;
        }

        #endregion
    }
}
