using System;
using UnityEngine;
using Duckov.Buffs;
using EliteEnemies.EliteEnemy.AttributeModifier;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.BuffsSystem.Effects
{
    /// <summary>
    /// 撕裂效果：削弱护甲
    /// </summary>
    public class TearEffect : IEliteBuffEffect
    {
        private const string LogTag = "[EliteEnemies.TearEffect]";
        public string BuffName => "EliteBuff_Tear";
        
        private readonly Lazy<string> _popTextFmt = new(() => 
            LocalizationManager.GetText("Affix_Tear_PopText_1")
        );
        
        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null) return;

            try
            {
                float randomReduction = UnityEngine.Random.Range(-0.4f, -0.1f);

                var manager = EliteBuffModifierManager.Instance;

                manager.ApplyAndTrack(player, buff, StatModifier.Attributes.BodyArmor, randomReduction);
                manager.ApplyAndTrack(player, buff, StatModifier.Attributes.HeadArmor, randomReduction);

                string msg = string.Format(_popTextFmt.Value, (randomReduction * 100f).ToString("F1"));
                player.PopText(msg);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 应用撕裂效果失败: {ex.Message}");
            }
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player)
        {
            try
            {
                int buffId = buff.GetInstanceID();
                EliteBuffModifierManager.Instance.CleanupModifiers(buffId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 清理撕裂效果失败: {ex.Message}");
            }
        }
    }
}