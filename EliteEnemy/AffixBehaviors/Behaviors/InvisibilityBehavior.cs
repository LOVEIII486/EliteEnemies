// InvisibilityBehavior.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 隐身词缀 - 受伤后触发隐身，然后周期性闪烁
    /// </summary>
    public class InvisibilityBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Invisible";
        
        private const char  MessageSeparator = '|';  // 本地化的分隔符
        
        private static readonly float VisibleInterval = 6.0f;  // 每隔多少秒触发一次显形效果
        private static readonly float FlashInterval   = 0.15f; // 闪烁间隔（秒）
        private static readonly int   FlashCount      = 3;     // 每次闪烁次数
        
        private float _timer;
        private float _flashTimer;
        private int _flashStep;
        private bool _isFlashing;
        private bool _isVisible;
        private bool _isActive;
        private bool _hasBeenHit; // 是否已被攻击过
        
        private List<string> _messages = new List<string>();
        private int _lastMsgIndex = -1; // 避免连续重复

        /// <summary>
        /// 从聚合字符串初始化台词
        /// </summary>
        private void InitMessages(string raw, char separator = MessageSeparator)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                _messages = new List<string> { "……" };
                return;
            }

            _messages = raw
                .Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            if (_messages.Count == 0)
                _messages.Add("……");
        }

        /// <summary>
        /// 从消息列表中随机取一句
        /// </summary>
        private string GetRandomMessage()
        {
            if (_messages == null || _messages.Count == 0) return "……";
            int index;
            do
            {
                index = UnityEngine.Random.Range(0, _messages.Count);
            } while (_messages.Count > 1 && index == _lastMsgIndex);

            _lastMsgIndex = index;
            return _messages[index];
        }

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _isActive = true;
            _isVisible = true;
            _isFlashing = false;
            _hasBeenHit = false;
            _timer = 0f;
            _flashTimer = 0f;
            _flashStep = 0;
            
            character.Show();
            
            string raw = LocalizationManager.GetText(
                "Affix_Invisible_Messages",
                "你刚才是不是看到我了？|别眨眼，我又消失啦～|嘘……什么都没发生。|幻觉？还是我太快了？|我就在你背后……开玩笑的。"
            );
            InitMessages(raw);
            EnhanceAIBehavior(character);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (!_hasBeenHit)
            {
                _hasBeenHit = true;
                character.Hide();
                _isVisible = false;
                _timer = 0f; // 重置计时器，开始周期循环
                
                character.PopText(GetRandomMessage());
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 首次受伤时立即隐身并开始计时
            if (!_hasBeenHit)
            {
                _hasBeenHit = true;
                character.Hide();
                _isVisible = false;
                _timer = 0f; // 重置计时器，开始周期循环
                
                character.PopText(GetRandomMessage());
            }
        }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (!_isActive || !_hasBeenHit) return; // 未受伤前不更新

            _timer += deltaTime;

            // 周期显形触发
            if (!_isFlashing && _timer >= VisibleInterval)
            {
                _timer = 0f;
                _isFlashing = true;
                _flashStep = 0;
                _flashTimer = 0f;
                character.PopText(GetRandomMessage());
            }

            // 闪烁逻辑
            if (_isFlashing)
            {
                _flashTimer += deltaTime;

                if (_flashTimer >= FlashInterval)
                {
                    _flashTimer = 0f;
                    _flashStep++;

                    if (_isVisible)
                    {
                        character.Hide();
                        _isVisible = false;
                    }
                    else
                    {
                        character.Show();
                        _isVisible = true;
                    }
                    if (_flashStep >= FlashCount * 2)
                    {
                        _isFlashing = false;
                        _flashStep = 0;
                        character.Hide();
                        _isVisible = false;
                    }
                }
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            character.Show();
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            character.Show();
            _isActive = false;
            _isVisible = false;
            _isFlashing = false;
            _hasBeenHit = false;
            _lastMsgIndex = -1;
            _messages.Clear();
        }
        
        private void EnhanceAIBehavior(CharacterMainControl enemy)
        {
            var aiEnhancements = new Dictionary<string, float>
            {
                [AIFieldModifier.Fields.ReactionTime] = 0.15f,
                [AIFieldModifier.Fields.ShootDelay] = 0.2f,
                [AIFieldModifier.Fields.ShootCanMove] = 1f,
                [AIFieldModifier.Fields.CanDash] = 1f,
                [AIFieldModifier.Fields.SightDistance] = 1.5f,
                [AIFieldModifier.Fields.SightAngle] = 1.3f,
                [AIFieldModifier.Fields.HearingAbility] = 1.5f,
                [AIFieldModifier.Fields.NightReactionTimeFactor] = 0.5f,
                [AIFieldModifier.Fields.PatrolTurnSpeed] = 1.3f,
                [AIFieldModifier.Fields.CombatTurnSpeed] = 1.5f
            };
 
            AIFieldModifier.ModifyDelayedBatch(enemy, aiEnhancements, multiply: true);
        }
    }
}