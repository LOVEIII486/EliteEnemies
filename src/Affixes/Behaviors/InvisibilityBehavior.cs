using System.Collections.Generic;
using System.Linq;
using EliteEnemies.Modifiers;
using EliteEnemies.Localization;
using ItemStatsSystem.Stats;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 隐身词缀 - 受伤后触发隐身，然后周期性闪烁
    /// </summary>
    public class InvisibilityBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Invisible";
        
        private const char MessageSeparator = '|';
        
        private static readonly float VisibleInterval = 5f;  // 每隔多少秒触发一次显形效果
        private static readonly float FlashInterval   = 0.15f; // 闪烁间隔（秒）
        private static readonly int   FlashCount      = 3;     // 每次闪烁次数
        
        private float _timer;
        private float _flashTimer;
        private int _flashStep;
        private bool _isFlashing;
        private bool _isVisible;
        private bool _isActive;
        private bool _hasBeenHit; 
        
        private List<string> _messages = new List<string>();
        private int _lastMsgIndex = -1;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            InitMessages();

            _isActive = true;
            _isVisible = true;
            _hasBeenHit = false;
            _timer = 0f;
            
            EnhanceAIBehavior(character);
        }

        /// <summary>
        /// 从本地化系统加载嘲讽语句
        /// </summary>
        private void InitMessages()
        {
            _messages.Clear();
            string raw = LocalizationManager.GetText("EliteEnemies_Affix_Invisible_Messages");
            
            if (!string.IsNullOrEmpty(raw))
            {
                _messages = raw.Split(MessageSeparator).ToList();
            }
        }

        /// <summary>
        /// 强化 AI 能力
        /// </summary>
        private void EnhanceAIBehavior(CharacterMainControl enemy)
        {
            Modify(enemy, StatKeys.ViewDistance, 1.5f, true);
            Modify(enemy, StatKeys.ViewAngle, 1.3f, true);
            Modify(enemy, StatKeys.HearingAbility, 1.5f, true);
            Modify(enemy, StatKeys.TurnSpeed, 1.3f, true);
            Modify(enemy, StatKeys.AimTurnSpeed, 1.5f, true);
            ModifyAI(enemy, AIFields.CanDash, true);
            ModifyAI(enemy, AIFields.ShootCanMove, true);
        }

        /// <summary>
        /// 显隐的统一出口。**为什么收口**：这个行为里 Hide/Show 有 8 处调用，
        /// 逐个加转交必然漏；而显隐本身**不在**联机模组的 <c>AISyncEntry</c> 里，
        /// 不转交的话客机看到的是一个**始终可见**的精英（隐身词条等于不存在）。
        ///
        /// <para>顺带把当前体型一起报过去——两条视觉状态共用一个报文，
        /// 免得两边各自维护、互相覆盖。</para>
        /// </summary>
        private static void SetHidden(CharacterMainControl character, bool hidden)
        {
            if (character == null) return;

            if (hidden) character.Hide();
            else character.Show();

            PlayerEffectRelay.RelayEliteVisual(character, character.transform.localScale, hidden);
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (!_hasBeenHit)
            {
                _hasBeenHit = true;
                _isVisible = false;
                _timer = VisibleInterval;
                SetHidden(character, true);
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 首次受击触发隐身
            if (!_hasBeenHit)
            {
                _hasBeenHit = true;
                _timer = VisibleInterval;
                _isVisible = false;
                SetHidden(character, true);
                // ShowRandomMessage(character);
            }
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (!_isActive || !_hasBeenHit) return;

            // 处理闪烁
            if (_isFlashing)
            {
                _flashTimer -= deltaTime;
                if (_flashTimer <= 0f)
                {
                    _flashTimer = FlashInterval;
                    _flashStep--;

                    if (_flashStep <= 0)
                    {
                        _isFlashing = false;
                        _isVisible = false;
                        SetHidden(character, true);
                        _timer = VisibleInterval;
                    }
                    else
                    {
                        // 切换显隐状态
                        if (_isVisible)
                        {
                            SetHidden(character, true);
                            _isVisible = false;
                        }
                        else
                        {
                            SetHidden(character, false);
                            _isVisible = true;
                        }
                    }
                }
                return;
            }

            // 处理周期性显形
            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                _isFlashing = true;
                _flashStep = FlashCount * 2; // 开+关算一次，所以乘2
                _flashTimer = FlashInterval;
                
                ShowRandomMessage(character);
            }
            
            // ⚠⚠ **这里只做本地压制，绝不能调 `SetHidden`。**
            //
            // 这段原先调的是 `SetHidden`——而它是 `RelayEliteVisual` 的唯一出口，
            // 于是非闪烁期的**每一帧**都往联机广播一条 `EliteVisual`：
            // 触发后是每约 5 秒一轮的循环，期间约 **60 条/秒/只**，
            // 每条 20+ 字节、每次还分配一个 `byte[]`。
            // 日志有上限（`CoopEliteSync.MaxHostVisualLogged`）所以从日志上完全看不出来，
            // 只有摘要里的 `主机上报视觉=` 会一路涨。
            //
            // **为什么这段本来就没必要转交**：`_isVisible` 在非闪烁期**根本不变化**
            // （它只在首次受击与闪烁分支里被写），真正的翻转早已由那些分支转发过了。
            // 而 `Hide()`/`Show()` 是**边沿触发**的（`if (!hidden)` 早退，
            // `CharacterMainControl.cs:2545`）⇒ 每帧重复调用**只是空转**，
            // 唯一有副作用的恰恰就是那次多余的网络广播。
            //
            // **但也不能整段删掉**：它承担"持续压制"——模型可能被别的系统重新启用
            // （与 `MimicBehavior` 同一理由）。所以拆成两件事：
            // 本地该压的照压，过网交给真正的状态翻转。
            if (_isVisible) character.Show();
            else character.Hide();
        }

        private void ShowRandomMessage(CharacterMainControl character)
        {
            if (_messages.Count == 0) return;

            int idx = UnityEngine.Random.Range(0, _messages.Count);
            if (idx == _lastMsgIndex && _messages.Count > 1)
            {
                idx = (idx + 1) % _messages.Count;
            }
            _lastMsgIndex = idx;
            
            PlayerEffectRelay.PopTextOnEliteResolved(character, _messages[idx]);
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            OnCleanup(character);
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);
            
            if (character != null)
            {
                SetHidden(character, false);
            }
            
            _isActive = false;
            _isVisible = true;
            _isFlashing = false;
            _hasBeenHit = false;
            _lastMsgIndex = -1;
        }
    }
}