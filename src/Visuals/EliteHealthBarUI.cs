using System.Text;
using Duckov.UI;
using Duckov.Utilities;
using EliteEnemies.Core;
using EliteEnemies.Localization;
using EliteEnemies.Settings;
using TMPro;
using UnityEngine;

namespace EliteEnemies.Visuals
{
    /// <summary>
    /// 挂载在 HealthBar 游戏对象上的控制器
    /// 只负责显示额外的词缀和血量数值。
    /// 针对 Combo 怪，优先显示组合名称。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class EliteHealthBarUI : MonoBehaviour
    {
        private const string LogTag = "[EliteEnemies.HealthBarUI]";

        private HealthBar _ownerBar;

        // 这条血条**原本**的配色。非精英时还原用它，见 RestoreVanillaBarColor 的注释。
        private Gradient _vanillaBarGradient;

        private GameObject _affixTextContainer;       // 词缀/Combo文本容器
        private TextMeshProUGUI _affixLabel;          // 词缀/Combo标签

        // 加描边用的 TMP 材质实例。`TMP_Text.fontMaterial` 的 getter 会**造一份实例**，
        // 而它不归那个 GameObject 所有 ⇒ 我们要自己留着引用、在清理时销毁。
        // 见 CreateAffixTextObject 与 CleanupAffixText 的注释。
        private Material _affixLabelMaterial;
        private GameObject _healthTextContainer;      // 血量文本容器
        private TextMeshProUGUI _healthValueLabel;    // 血量数值标签

        private Health _cachedTarget;
        private CharacterMainControl _cachedCmc;
        private EliteMarker _cachedMarker;

        private string _cachedAffixPrefix = null;     // 保存词缀标签或Combo名称
        // _cachedAffixPrefix 是按哪个语言版本构建的。语言变过之后必须重建它——
        // 否则已经建好的血条会一直显示上一门语言的词条名（血条比敌人活得久）。
        private int _affixPrefixVersion;
        private int _lastHp = -1;
        private int _lastMaxHp = -1;

        private string _lastAffixText = null;         // 词缀文本缓存

        private string _lastHealthText = null;        // 血量文本缓存
        private bool _isElite = false;

        // 「生成后延迟标记」的二次检查窗口（秒）。见 LateUpdate 里那段的注释：
        // 不节流的话，非精英血条每帧都要付一次 `GetComponent`，而血条数只增不减。
        private const float MarkerRecheckInterval = 0.5f;
        private float _nextMarkerRecheckTime;

        private GameConfig.AffixTextDisplayPosition _lastPositionType;
        private float _lastVerticalOffset = -999f;

        private StringBuilder _sb = new StringBuilder(64);

        private void Awake()
        {
            _ownerBar = GetComponent<HealthBar>();

            // 记下血条**原本**的配色。这一步必须早于任何染色，而 `Awake` 正是这个时机：
            // 本组件是 `HealthBar.Awake` 的补丁挂上来的，此刻该血条还没 Refresh 过，
            // `colorOverAmount` 还是预制体里的真值。
            // ⚠ 不要改成"用的时候再读"——那时它可能已经被我们染过色了。
            _vanillaBarGradient = _ownerBar != null ? _ownerBar.colorOverAmount : null;
        }

        /// <summary>
        /// 把血条配色**还原成它原本的样子**。
        ///
        /// <para><b>为什么必须有这一步</b>：血条是**池化复用**的——一条被精英染过色的血条会被
        /// 复用给普通敌人，不还原就会带着精英的颜色继续显示。</para>
        ///
        /// <para><b>为什么还原的是"存下来的真值"，而不是一个写死的颜色</b>：原先这里写的是
        /// 硬编码纯红 `#FF4D4D`，依据是注释里那句"原生红也是非精英血条的颜色"——**那句话没有出处**。
        /// <c>HealthBar.colorOverAmount</c> 是 <c>[SerializeField]</c>
        /// （<c>Duckov/UI/HealthBar.cs:41-42</c>），真值在预制体里、源码查不到；而游戏是拿它
        /// <c>Evaluate(血量比例)</c> 用的（<c>:348</c>）——**用 <c>Gradient</c> 接一个比例，
        /// 本来就可能随血量变化**。存真值之后，无论原版是纯色还是渐变都自动正确，我们也不必去猜。</para>
        ///
        /// <para>拿不到快照时**不动它**（保守）：宁可少还原一次，也不要写一个我们猜的颜色。</para>
        /// </summary>
        internal static void RestoreVanillaBarColor(HealthBar bar)
        {
            if (bar == null) return;

            var ui = bar.GetComponent<EliteHealthBarUI>();
            if (ui != null && ui._vanillaBarGradient != null)
            {
                bar.colorOverAmount = ui._vanillaBarGradient;
            }
        }

        private void OnEnable()
        {
            ResetTextCaches();
            _cachedTarget = null;
            _lastVerticalOffset = -999f;
        }

        private void LateUpdate()
        {
            if (!_ownerBar) return;

            if (!EliteEnemyCore.Config.ShowEliteName)
            {
                CleanupCustomTextObjects();
                return;
            }

            // 1. 检查 Target 是否变化
            if (_ownerBar.target != _cachedTarget)
            {
                RefreshTarget(_ownerBar.target);
            }

            // 二次检查逻辑（针对生成后延迟标记的情况）。
            //
            // ⚠ **必须节流。** 血条**不会因出画释放**——游戏那条释放路径
            // `HealthBar.UpdateFrame`（`Duckov/UI/HealthBar.cs:156-166`）**全库零调用点**，
            // 是它自己的死代码 ⇒ 血条数只增不减 ⇒ 不节流的话，"每帧一次 `GetComponent`"
            // 的成本就是**每帧 × 屏上血条总数**（`docs\Visuals模块代码审查.md` V3）。
            //
            // 这里要发现的只是"生成之后延迟挂上标记"这一幕，晚几百毫秒没有任何代价
            // （`_isElite` 只决定词条标签显不显示）。0.5 秒的窗口足够，而且
            // 精英走不到这一句（`_isElite` 为真时第一项就短路）⇒ 对它们零开销。
            if (!_isElite && _cachedCmc && Time.time >= _nextMarkerRecheckTime)
            {
                _nextMarkerRecheckTime = Time.time + MarkerRecheckInterval;

                var marker = _cachedCmc.GetComponent<EliteMarker>();
                if (marker != null && (marker.Affixes.Count > 0 || !string.IsNullOrEmpty(marker.CustomDisplayName)))
                {
                    RefreshTarget(_cachedTarget);
                }
            }

            // 2. 如果不是精英怪，清理UI
            if (!_isElite)
            {
                CleanupCustomTextObjects();
                return;
            }

            // 3. 更新显示
            UpdateEliteUI();
        }

        private void RefreshTarget(Health newTarget)
        {
            // 上一只的乱码状态不再需要（它可能再也没有血条了）
            EliteGarbledLabel.Forget(_cachedCmc);

            _cachedTarget = newTarget;
            _cachedCmc = newTarget ? newTarget.TryGetCharacter() : null;
            _cachedMarker = _cachedCmc ? _cachedCmc.GetComponent<EliteMarker>() : null;

            bool isFriendly = _cachedCmc && LevelManager.Instance?.MainCharacter && _cachedCmc.Team == LevelManager.Instance.MainCharacter.Team;
            bool isUIHidden = _cachedCmc != null && _cachedCmc.characterPreset != null && PresetDirectory.IsUIHidden(_cachedCmc.characterPreset.name);
            if (isFriendly || isUIHidden)
            {
                _isElite = false;
                return;
            }

            _isElite = _cachedMarker != null &&
                       (_cachedMarker.Affixes.Count > 0 || !string.IsNullOrEmpty(_cachedMarker.CustomDisplayName));

            if (_isElite)
            {
                BuildAffixPrefix();
            }

            ResetTextCaches();

            // 换了目标就立刻重查一次标记（不等 0.5 秒的窗口）——新目标本来就该马上判定。
            _nextMarkerRecheckTime = 0f;
        }

        /// <summary>
        /// 构建词缀/Combo 前缀，并记下它是按哪个语言版本构建的。
        ///
        /// <para>语言切换时不靠回调来刷新——回调要求每个缓存都记得登记自己，而漏登记
        /// 是无声的（本工程原先就漏了词条表与血条两层）。改为在
        /// <see cref="UpdateEliteUI"/> 里比对版本号：稳态是一次 int 比较，语言真变过
        /// 之后才重建一次。</para>
        /// </summary>
        private void BuildAffixPrefix()
        {
            if (!string.IsNullOrEmpty(_cachedMarker.CustomDisplayName))
            {
                // 如果是 Combo 怪，直接使用特殊名称，不再构建词缀前缀。
                // 该属性由 combo 定义现算，本身就跟随语言（见 EliteMarker.CustomDisplayName）。
                _cachedAffixPrefix = _cachedMarker.CustomDisplayName;
            }
            else if (_cachedMarker.Affixes != null && EliteGarbledLabel.AffixesContainsObscurer(_cachedMarker.Affixes))
            {
                // 封弊者：名字被随机乱码遮住，实际内容每帧在 UpdateEliteUI 里现生成
                _cachedAffixPrefix = EliteGarbledLabel.PlaceholderLabel;
            }
            else
            {
                // 正常构建词缀标签列表：[词缀A][词缀B]
                _cachedAffixPrefix = EliteEnemyCore.BuildColoredPrefix(_cachedMarker.Affixes);
            }

            _affixPrefixVersion = LocalizationManager.LanguageVersion;
        }

        private void UpdateEliteUI()
        {
            bool showDetail = EliteEnemyCore.Config.ShowDetailedHealth;

            // 语言切换过 → 手上这份前缀是上一门语言的，重建一次。
            // 稳态代价：一次 int 比较。**不要改成每帧重建**——那会每帧为每只精英拼一次字符串。
            if (_cachedMarker != null && _affixPrefixVersion != LocalizationManager.LanguageVersion)
            {
                BuildAffixPrefix();
            }

            // === 1. 更新词缀/Combo 标签 ===
            if (!string.IsNullOrEmpty(_cachedAffixPrefix))
            {
                if (_affixTextContainer == null)
                {
                    CreateAffixTextObject();
                }

                UpdateAffixContainerTransform();

                if (_affixLabel != null)
                {
                    // 动态调整字体大小。
                    //
                    // ⚠ 读的是 **`EliteEnemyCore.Config`（快照）**，不是 `GameConfig`（静态实时值）——
                    // 本类原先两种读法并存（字体走实时值、显示开关走快照），"设置改完是这一帧生效
                    // 还是下一帧生效"会取决于读的是哪一个。快照在 `GameConfig.NotifyChanged` 时被
                    // **同步**推送（`EliteEnemyCore.UpdateConfig`），所以统一到它，语义与实时值等价，
                    // 而"运行时该读哪一份"只有一个答案（`docs\Visuals模块代码审查.md` V7）。
                    if (Mathf.Abs(_affixLabel.fontSizeMin - EliteEnemyCore.Config.AffixFontSize) > 0.1f)
                    {
                        _affixLabel.fontSizeMin = (float)EliteEnemyCore.Config.AffixFontSize;
                        _affixLabel.fontSizeMax = (float)EliteEnemyCore.Config.AffixFontSize + 4f;
                    }

                    string displayText = _cachedAffixPrefix;

                    // 封弊者：换成乱码富文本。**刷新节奏与缓存都在 EliteGarbledLabel 里**——
                    // 稳态下这里只做一次 Time.time 比较、零分配（原先每帧插值一次字符串）。
                    if (displayText == EliteGarbledLabel.PlaceholderLabel)
                    {
                        displayText = EliteGarbledLabel.GetTag(_cachedCmc);
                    }

                    // 只有文本真的变了才写回（乱码分支也满足：上面给的是缓存好的标签）
                    if (displayText != _lastAffixText)
                    {
                        _affixLabel.text = displayText;
                        _lastAffixText = displayText;
                    }
                }
            }
            else
            {
                CleanupAffixText();
            }

            // === 2. 更新血量数值标签 ===
            if (!showDetail)
            {
                CleanupHealthText();
                return;
            }

            if (_cachedCmc.Health == null) return;

            if (_healthTextContainer == null)
            {
                CreateHealthTextObject();
            }

            if (_healthValueLabel != null)
            {
                int currentHp = Mathf.CeilToInt(_cachedCmc.Health.CurrentHealth);
                int maxHp = Mathf.CeilToInt(_cachedCmc.Health.MaxHealth);

                if (currentHp != _lastHp || maxHp != _lastMaxHp)
                {
                    _lastHp = currentHp;
                    _lastMaxHp = maxHp;

                    _sb.Clear();
                    _sb.Append(currentHp);
                    _sb.Append("  /  ");
                    _sb.Append(maxHp);

                    string newHealthText = _sb.ToString();
                    if (_lastHealthText != newHealthText)
                    {
                        _healthValueLabel.text = newHealthText;
                        _lastHealthText = newHealthText;
                    }
                }
            }
        }

        private void UpdateAffixContainerTransform()
        {
            if (_affixTextContainer == null) return;

            var currentPosType = EliteEnemyCore.Config.AffixDisplayPosition;
            var currentOffset = EliteEnemyCore.Config.AffixVerticalOffset;
            if (currentPosType != _lastPositionType || Mathf.Abs(currentOffset - _lastVerticalOffset) > 0.01f)
            {
                // 基础位置：头顶 55，脚底 -125
                float baseY = (currentPosType == GameConfig.AffixTextDisplayPosition.Overhead) ? 55f : -125f;
                float finalY = baseY + currentOffset;

                _affixTextContainer.transform.localPosition = new Vector3(0f, finalY, 0f);

                _lastPositionType = currentPosType;
                _lastVerticalOffset = currentOffset;
            }
        }

        private void CreateAffixTextObject()
        {
            bool showAbove = EliteEnemyCore.Config.AffixDisplayPosition == GameConfig.AffixTextDisplayPosition.Overhead;
            float yOffset = showAbove ? 55f : -125f;

            _affixTextContainer = new GameObject("EliteAffixTextObj");
            _affixTextContainer.transform.SetParent(_ownerBar.transform);
            _affixTextContainer.transform.localPosition = new Vector3(0f, yOffset, 0f);
            _affixTextContainer.transform.localScale = Vector3.one;
            _affixTextContainer.transform.localRotation = Quaternion.identity;

            _lastVerticalOffset = -999f;
            UpdateAffixContainerTransform();

            GameObject textObj = new GameObject("AffixText");
            textObj.transform.SetParent(_affixTextContainer.transform);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localScale = Vector3.one;

            _affixLabel = textObj.AddComponent<TextMeshProUGUI>();
            _affixLabel.alignment = TextAlignmentOptions.Center;
            _affixLabel.fontSizeMin = (float)EliteEnemyCore.Config.AffixFontSize;
            _affixLabel.fontSizeMax = (float)EliteEnemyCore.Config.AffixFontSize + 4f;
            _affixLabel.enableAutoSizing = true;
            _affixLabel.fontStyle = FontStyles.Bold;

            // _affixLabel.textWrappingMode = TextWrappingModes.NoWrap;
#pragma warning disable CS0618 // 新版标为 Obsolete；正式版(2022.2)是正常属性，只有它两版都有
            _affixLabel.enableWordWrapping = false;   // NoWrap：单行不折行。勿改回 textWrappingMode
#pragma warning restore CS0618

            _affixLabel.overflowMode = TextOverflowModes.Overflow;
            // ⚠ `fontMaterial` 的 getter 会**新建一份材质实例**（这是 TMP 的设计：改它不影响
            // 别的文本），而那份实例**不归这个 GameObject 所有**——`Destroy(容器)` 不会连带销毁它。
            // 所以必须**留引用**，否则每次重建标签都漏一份，要到 `Resources.UnloadUnusedAssets`
            // 才回收。销毁点在 CleanupAffixText。
            _affixLabelMaterial = _affixLabel.fontMaterial;
            _affixLabelMaterial.EnableKeyword("OUTLINE_ON");
            _affixLabel.outlineWidth = 0.25f;
            _affixLabel.outlineColor = new Color(0f, 0f, 0f, 1f);
        }

        private void CreateHealthTextObject()
        {
            _healthTextContainer = new GameObject("EliteHealthTextObj");
            _healthTextContainer.transform.SetParent(_ownerBar.transform);
            _healthTextContainer.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            _healthTextContainer.transform.localScale = Vector3.one;
            _healthValueLabel = UnityEngine.Object.Instantiate<TextMeshProUGUI>(
                GameplayDataSettings.UIStyle.TemplateTextUGUI,
                _healthTextContainer.transform
            );

            // `UIStyle` 是**懒加载**的 `Resources.Load`（`GameplayDataSettings.cs` 的 Default 路径）
            // ⇒ 资源缺失时模板为 null、`Instantiate(null)` 返回 null，下面一行会在 LateUpdate 里
            // 抛 NRE 并**每帧重复**。当前不会发生（游戏自带的 UI 模板），但代价只有两行。
            if (_healthValueLabel == null)
            {
                Debug.LogError($"{LogTag} 血量文本模板（UIStyle.TemplateTextUGUI）取不到，" +
                               "本次不建血量文本");
                Destroy(_healthTextContainer);
                _healthTextContainer = null;
                return;
            }

            _healthValueLabel.gameObject.name = "HealthValueText";
            _healthValueLabel.alignment = TextAlignmentOptions.Center;
            _healthValueLabel.fontSizeMin = 15f;
            _healthValueLabel.fontSizeMax = 17f;
            _healthValueLabel.enableAutoSizing = true;
            _healthValueLabel.fontStyle = FontStyles.Bold;
            _healthValueLabel.color = new Color(0.9f, 0.9f, 1.0f);
            _healthValueLabel.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// 复位"文本重绘"相关的缓存。
        ///
        /// <para><b>为什么要有这个统一入口</b>：写入门槛是"**值变了才写**"
        /// （`currentHp != _lastHp || maxHp != _lastMaxHp` 等），所以**任何销毁/重建文本容器的
        /// 路径都必须复位它们**——否则新标签是空的，而下一次循环因为"值没变"直接跳过写入
        /// ⇒ **数字要等到下次掉血才出现**（`docs\Visuals模块代码审查.md` V1）。</para>
        ///
        /// <para>⚠ 这三处原先各清各的，`CleanupHealthText` 就**漏了血量那一对**。
        /// 收敛成一个方法之后，**以后再加缓存字段只需要改这一处**。</para>
        /// </summary>
        private void ResetTextCaches()
        {
            _lastHp = -1;
            _lastMaxHp = -1;
            _lastAffixText = null;
            _lastHealthText = null;
        }

        private void CleanupAffixText()
        {
            if (_affixTextContainer != null)
            {
                Destroy(_affixTextContainer);
                _affixTextContainer = null;

                // 材质实例是我们自己留下来的引用（见 CreateAffixTextObject），
                // 不在这里销毁就是每次重建标签漏一份。
                if (_affixLabelMaterial != null)
                {
                    Destroy(_affixLabelMaterial);
                    _affixLabelMaterial = null;
                }

                _affixLabel = null;
            }

            ResetTextCaches();
        }

        private void CleanupHealthText()
        {
            if (_healthTextContainer != null)
            {
                Destroy(_healthTextContainer);
                _healthTextContainer = null;
                _healthValueLabel = null;
            }

            ResetTextCaches();
        }

        private void CleanupCustomTextObjects()
        {
            CleanupAffixText();
            CleanupHealthText();
        }

        private void OnDestroy()
        {
            CleanupCustomTextObjects();
        }
    }
}
