// EliteEnemies/AffixBehaviors/ObscurerBehavior.cs

using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 封弊者 - 隐藏其他词条名称显示为动态变化的乱码
    /// </summary>
    public class ObscurerBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Obscurer";

        private const string GarbledChars =
            "▓▒░█▉▊▋▌▍▎▏▀▄▐▌" +
            "■□◆◇▲▼◀▶●○◎" +
            "★☆※§¶†‡" +
            "，。！？；：、…·《》【】（）「」『』" +
            "#@$%&*?!~^|/\\+=-_:;,.`";

        // 动态刷新配置
        private const float RefreshInterval = 0.3f;  // 刷新间隔
        private const int MinGarbledLength = 4;      // 最小长度
        private const int MaxGarbledLength = 6;     // 最大长度

        private float _refreshTimer = 0f;
        private CharacterMainControl _character;
        
        private static readonly Dictionary<CharacterMainControl, string> _currentGarbledText 
            = new Dictionary<CharacterMainControl, string>();
        private static readonly Dictionary<CharacterMainControl, string> _currentColorHex
            = new Dictionary<CharacterMainControl, string>();

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;
            _character = character;
            
            // 初始化第一个乱码
            _currentGarbledText[character] = GenerateNewGarbledText();
            _currentColorHex[character] = GenerateRandomColorHex();
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (character == null) return;

            _refreshTimer += deltaTime;
            
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                
                _currentGarbledText[character] = GenerateNewGarbledText();
                _currentColorHex[character] = GenerateRandomColorHex();
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (character == null) return;
            
            _currentGarbledText.Remove(character);
            _currentColorHex.Remove(character);
        }

        /// <summary>
        /// 生成新的随机乱码
        /// </summary>
        private static string GenerateNewGarbledText()
        {
            int length = Random.Range(MinGarbledLength, MaxGarbledLength + 1);
            var sb = new System.Text.StringBuilder(length);
            
            for (int i = 0; i < length; i++)
            {
                sb.Append(GarbledChars[Random.Range(0, GarbledChars.Length)]);
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// 获取指定角色的当前乱码文本
        /// </summary>
        public static string GetCurrentGarbledText(CharacterMainControl character)
        {
            if (character == null) 
                return GenerateNewGarbledText();
            
            if (!_currentGarbledText.TryGetValue(character, out string text))
            {
                text = GenerateNewGarbledText();
                _currentGarbledText[character] = text;
            }
            
            return text;
        }
        
        /// <summary>
        /// 生成随机颜色的 Hex 代码
        /// </summary>
        private static string GenerateRandomColorHex(int min = 70, int max = 255)
        {
            min = Mathf.Clamp(min, 0, 255);
            max = Mathf.Clamp(max, 1, 255);

            int r = Random.Range(min, max + 1);
            int g = Random.Range(min, max + 1);
            int b = Random.Range(min, max + 1);

            return $"#{r:X2}{g:X2}{b:X2}";
        }
        
        public static string GetCurrentRandomColor(CharacterMainControl character)
        {
            if (character == null)
                return GenerateRandomColorHex();

            if (!_currentColorHex.TryGetValue(character, out string color))
            {
                color = GenerateRandomColorHex();
                _currentColorHex[character] = color;
            }

            return color;
        }
    }
}