using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Duckov.Utilities;

namespace EliteEnemies.DebugTool
{
    public class PresetKeyLogger : MonoBehaviour
    {
        private const string LogTag = "[EliteEnemies.PresetKeyLogger]";
        
        public KeyCode dumpKey = KeyCode.F9;

        private void Update()
        {
            if (Input.GetKeyDown(dumpKey))
            {
                DumpPresets();
            }
        }

        [ContextMenu("Dump All Presets")]
        public void DumpPresets()
        {
            try
            {
                List<CharacterRandomPreset> presets = GameplayDataSettings.CharacterRandomPresetData?.presets;

                if (presets == null || presets.Count == 0)
                {
                    Debug.LogWarning($"{LogTag} GameplayDataSettings 未就绪，尝试从 Resources 加载...");
                    presets = new List<CharacterRandomPreset>(Resources.FindObjectsOfTypeAll<CharacterRandomPreset>());
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== CharacterRandomPreset 映射清单 ===");
                sb.AppendLine($"导出时间: {DateTime.Now} | 触发方式: Key({dumpKey})");
                sb.AppendLine("格式: [资源名 (name)] -> [本地化键名 (nameKey)]");
                sb.AppendLine("--------------------------------------------------");

                HashSet<string> processed = new HashSet<string>();
                int count = 0;

                foreach (var preset in presets)
                {
                    if (preset == null || preset.name.Contains("(Clone)")) continue;
                    if (processed.Contains(preset.name)) continue;

                    sb.AppendLine($"{preset.name} \t| {preset.nameKey}");
                    processed.Add(preset.name);
                    count++;
                }

                sb.AppendLine("--------------------------------------------------");
                sb.AppendLine($"总计: {count} 个唯一预设。");
                sb.AppendLine("========================================");

                Debug.Log(sb.ToString());

                string outputPath = Application.persistentDataPath + "/EliteEnemies_PresetMapping.txt";
                System.IO.File.WriteAllText(outputPath, sb.ToString());
                
                Debug.Log($"{LogTag} 成功！映射清单已导出至: {outputPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 导出失败: {ex.Message}");
            }
        }
    }
}