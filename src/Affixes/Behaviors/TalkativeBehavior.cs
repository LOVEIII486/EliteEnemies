using System;
using System.Collections.Generic;
using System.Linq;
using EliteEnemies.Localization;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【话痨】词缀 - 定期在敌人头顶显示随机台词
    /// </summary>
    public class TalkativeBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Talkative";
        
        private static readonly float TalkInterval = 5f; // 说话间隔
        
        private float _talkTimer = 0f;
        private int _lastIndex = -1; // 避免连续重复
        private List<string> _messages;   // 由 InitMessages 赋值（原先 `= new ()` 立刻被覆盖，白分配一次）
        
        /// <summary>
        /// 从聚合字符串初始化台词
        /// 翻译者只需维护单个 value 字段，用分隔符 '|' 连接多条语句。
        /// </summary>
        private void InitMessages(string raw, char separator = '|')
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                _messages = new List<string> { "..." };
                return;
            }

            _messages = raw
                .Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            if (_messages.Count == 0)
                _messages.Add("...");
        }

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            // 基于中文为原文的本地化键，值为默认聚合字符串（可被 CSV 覆盖）
            string localized = LocalizationManager.GetText(
                "EliteEnemies_Affix_Talkative_Messages",
                "我是精英！|来战啊！|你打不过我的！|哈哈哈！|太弱了！"
            );

            InitMessages(localized);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            // 注意：CharacterMainControl 典型为 UnityEngine.Object 派生，== 会触发 Unity 的销毁检查
            if (character == null || _messages == null || _messages.Count == 0)
                return;

            _talkTimer += deltaTime;

            if (_talkTimer >= TalkInterval)
            {
                _talkTimer = 0f;

                // 随机选择一条语句，并尽量避免与上一条重复
                // 随机选一条并避开上一条。**不再是 do/while 重掷**：条数少时那是个无上界的循环。
                int index;
                if (_messages.Count <= 1 || _lastIndex < 0)
                {
                    index = UnityEngine.Random.Range(0, _messages.Count);
                }
                else
                {
                    index = UnityEngine.Random.Range(0, _messages.Count - 1);
                    if (index >= _lastIndex) index++;   // 跳过上一次那条，且保持均匀
                }

                _lastIndex = index;
                string message = _messages[index];

                PlayerEffectRelay.PopTextOnElite(character, message);
            }
        }



        public override void OnCleanup(CharacterMainControl character)
        {
            _talkTimer = 0f;
            _lastIndex = -1;
        }
    }
}