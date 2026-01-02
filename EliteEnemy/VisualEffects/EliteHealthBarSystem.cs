using System.Text;
using Duckov.UI;
using Duckov.Utilities;
using EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors;
using EliteEnemies.EliteEnemy.Core;
using EliteEnemies.Settings;
using TMPro;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.VisualEffects
{
    /// <summary>
    /// 精英血条管理器
    /// </summary>
    public class EliteHealthBarManager : MonoBehaviour
    {
        public static EliteHealthBarManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// 挂载在 HealthBar 游戏对象上的控制器
    /// 只负责显示额外的词缀和血量数值。
    /// 针对 Combo 怪，优先显示组合名称。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class EliteHealthBarUI : MonoBehaviour
    {
        private HealthBar _ownerBar;
        
        private GameObject _affixTextContainer;       // 词缀/Combo文本容器
        private TextMeshProUGUI _affixLabel;          // 词缀/Combo标签
        private GameObject _healthTextContainer;      // 血量文本容器
        private TextMeshProUGUI _healthValueLabel;    // 血量数值标签
        
        private Health _cachedTarget;
        private CharacterMainControl _cachedCmc;
        private EliteEnemyCore.EliteMarker _cachedMarker;
        
        private string _cachedAffixPrefix = null;     // 保存词缀标签或Combo名称
        private int _lastHp = -1;
        private int _lastMaxHp = -1;
        
        private string _lastAffixText = null;         // 词缀文本缓存
        private string _lastHealthText = null;        // 血量文本缓存
        private bool _isElite = false;
        
        private GameConfig.AffixTextDisplayPosition _lastPositionType;
        private float _lastVerticalOffset = -999f;
        
        private StringBuilder _sb = new StringBuilder(64);

        private void Awake()
        {
            _ownerBar = GetComponent<HealthBar>();
        }

        private void OnEnable()
        {
            _lastAffixText = null;
            _lastHealthText = null; 
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

            // 二次检查逻辑（针对生成后延迟标记的情况）
            if (!_isElite && _cachedCmc)
            {
                var marker = _cachedCmc.GetComponent<EliteEnemyCore.EliteMarker>();
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
            _cachedTarget = newTarget;
            _cachedCmc = newTarget ? newTarget.TryGetCharacter() : null;
            _cachedMarker = _cachedCmc ? _cachedCmc.GetComponent<EliteEnemyCore.EliteMarker>() : null;
            
            bool isFriendly = _cachedCmc && LevelManager.Instance?.MainCharacter && _cachedCmc.Team == LevelManager.Instance.MainCharacter.Team;
            bool isUIHidden = _cachedCmc != null && _cachedCmc.characterPreset != null && EliteEnemyCore.IsUIHidden(_cachedCmc.characterPreset.name);
            if (isFriendly || isUIHidden)
            {
                _isElite = false;
                return;
            }
            
            _isElite = _cachedMarker != null && 
                       (_cachedMarker.Affixes.Count > 0 || !string.IsNullOrEmpty(_cachedMarker.CustomDisplayName));
            
            if (_isElite)
            {
                if (!string.IsNullOrEmpty(_cachedMarker.CustomDisplayName))
                {
                    // 如果是 Combo 怪，直接使用特殊名称，不再构建词缀前缀
                    _cachedAffixPrefix = _cachedMarker.CustomDisplayName;
                }
                else
                {
                    // 普通精英怪：检查是否有封弊者乱码效果
                    bool hasObscurer = _cachedMarker.Affixes != null && _cachedMarker.Affixes.Contains("Obscurer");
                    if (hasObscurer)
                    {
                        _cachedAffixPrefix = "<OBSCURER_PLACEHOLDER>";
                    }
                    else
                    {
                        // 正常构建词缀标签列表：[词缀A][词缀B]
                        _cachedAffixPrefix = EliteEnemyCore.BuildColoredPrefix(_cachedMarker.Affixes);
                    }
                }
            }

            _lastHp = -1;
            _lastAffixText = null;
            _lastHealthText = null;
        }

        private void UpdateEliteUI()
        {
            bool showDetail = EliteEnemyCore.Config.ShowDetailedHealth;
            
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
                    // 动态调整字体大小
                    if (Mathf.Abs(_affixLabel.fontSizeMin - GameConfig.AffixFontSize) > 0.1f)
                    {
                        _affixLabel.fontSizeMin = (float)GameConfig.AffixFontSize;
                        _affixLabel.fontSizeMax = (float)GameConfig.AffixFontSize + 4f;
                    }
                    
                    string displayText = _cachedAffixPrefix;
        
                    // 处理封弊者乱码逻辑
                    if (displayText == "<OBSCURER_PLACEHOLDER>")
                    {
                        string garbled = ObscurerBehavior.GetCurrentGarbledText(_cachedCmc);
                        string color = ObscurerBehavior.GetCurrentRandomColor(_cachedCmc);
                        displayText = $"<color={color}>[{garbled}]</color>";
                    }
        
                    // 只有文本变动或乱码状态下才更新，减少消耗
                    if (displayText != _lastAffixText || _cachedAffixPrefix == "<OBSCURER_PLACEHOLDER>")
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
            _affixLabel.fontSizeMin = (float)GameConfig.AffixFontSize;
            _affixLabel.fontSizeMax = (float)GameConfig.AffixFontSize + 4f;
            _affixLabel.enableAutoSizing = true;
            _affixLabel.fontStyle = FontStyles.Bold;
            _affixLabel.enableWordWrapping = false;
            _affixLabel.overflowMode = TextOverflowModes.Overflow;
            _affixLabel.fontMaterial.EnableKeyword("OUTLINE_ON");
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
            _healthValueLabel.gameObject.name = "HealthValueText";
            _healthValueLabel.alignment = TextAlignmentOptions.Center;
            _healthValueLabel.fontSizeMin = 15f;
            _healthValueLabel.fontSizeMax = 17f;
            _healthValueLabel.enableAutoSizing = true;
            _healthValueLabel.fontStyle = FontStyles.Bold;
            _healthValueLabel.color = new Color(0.9f, 0.9f, 1.0f);
            _healthValueLabel.overflowMode = TextOverflowModes.Overflow;
        }

        private void CleanupAffixText()
        {
            if (_affixTextContainer != null)
            {
                Destroy(_affixTextContainer);
                _affixTextContainer = null;
                _affixLabel = null;
                _lastAffixText = null;
            }
        }

        private void CleanupHealthText()
        {
            if (_healthTextContainer != null)
            {
                Destroy(_healthTextContainer);
                _healthTextContainer = null;
                _healthValueLabel = null;
                _lastHealthText = null;
            }
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