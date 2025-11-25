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
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class EliteHealthBarUI : MonoBehaviour
    {
        private HealthBar _ownerBar;
        private TextMeshProUGUI _nameLabel;           // 名字标签引用，仅用于获取字体样式等，不修改内容
        private GameObject _affixTextContainer;       // 词缀文本容器
        private TextMeshProUGUI _affixLabel;          // 词缀标签
        private GameObject _healthTextContainer;      // 血量文本容器
        private TextMeshProUGUI _healthValueLabel;    // 血量数值标签
        
        private Health _cachedTarget;
        private CharacterMainControl _cachedCmc;
        private EliteEnemyCore.EliteMarker _cachedMarker;
        
        private string _cachedAffixPrefix = null;     // 保存词缀前缀
        private int _lastHp = -1;
        private int _lastMaxHp = -1;
        private string _lastAffixText = null;         // 词缀文本缓存
        private string _lastHealthText = null;        // 血量文本缓存
        private bool _isElite = false;
        
        private StringBuilder _sb = new StringBuilder(64);

        private void Awake()
        {
            _ownerBar = GetComponent<HealthBar>();
            _nameLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void OnEnable()
        {
            // 每次激活时强制刷新一次状态，防止对象池复用导致的脏数据
            _lastAffixText = null;
            _lastHealthText = null; 
            _cachedTarget = null;
        }

        private void LateUpdate()
        {
            if (!_ownerBar) return;
            
            if (!EliteEnemyCore.Config.ShowEliteName)
            {
                // 清理所有自定义文本对象
                CleanupCustomTextObjects();
                return;
            }

            // 1. 检查 Target 是否变化 (对象池复用逻辑)
            if (_ownerBar.target != _cachedTarget)
            {
                RefreshTarget(_ownerBar.target);
            }

            // 如果当前认为不是精英，再次尝试获取组件
            if (!_isElite && _cachedCmc)
            {
                var marker = _cachedCmc.GetComponent<EliteEnemyCore.EliteMarker>();
                if (marker != null && marker.Affixes.Count > 0)
                {
                    RefreshTarget(_cachedTarget);
                }
            }

            // 2. 如果不是精英怪，清理并归还控制权
            if (!_isElite)
            {
                CleanupCustomTextObjects();
                return;
            }

            // 3. 更新 UI (仅附加信息)
            UpdateEliteUI();
        }

        private void RefreshTarget(Health newTarget)
        {
            _cachedTarget = newTarget;
            _cachedCmc = newTarget ? newTarget.TryGetCharacter() : null;
            _cachedMarker = _cachedCmc ? _cachedCmc.GetComponent<EliteEnemyCore.EliteMarker>() : null;
            
            // 排除玩家自己
            if (_cachedCmc && LevelManager.Instance?.MainCharacter && _cachedCmc.Team == LevelManager.Instance.MainCharacter.Team)
            {
                _isElite = false;
                return;
            }
            
            _isElite = _cachedMarker != null && 
                       (_cachedMarker.Affixes.Count > 0 || !string.IsNullOrEmpty(_cachedMarker.CustomDisplayName));
            
            if (_isElite)
            {
                bool hasObscurer = _cachedMarker.Affixes != null && _cachedMarker.Affixes.Contains("Obscurer");
                
                // 词缀前缀处理
                _cachedAffixPrefix = "";
                if (_cachedMarker.Affixes != null && _cachedMarker.Affixes.Count > 0)
                {
                    if (hasObscurer)
                    {
                        _cachedAffixPrefix = "<OBSCURER_PLACEHOLDER>";
                    }
                    else
                    {
                        _cachedAffixPrefix = EliteEnemyCore.BuildColoredPrefix(_cachedMarker.Affixes);
                    }
                }
                
                // 确保目标显示名字和血条 (但不修改名字内容)
                if (_cachedCmc.characterPreset != null)
                {
                    _cachedCmc.characterPreset.showName = true;
                    _cachedCmc.characterPreset.showHealthBar = true;
                }
            }

            // 重置显示状态
            _lastHp = -1;
            _lastAffixText = null;
            _lastHealthText = null;
        }

        private void UpdateEliteUI()
        {
            bool showDetail = EliteEnemyCore.Config.ShowDetailedHealth;
            
            // 注意：此处不再更新 _nameLabel.text，让原游戏逻辑控制名字显示
            
            // === 1. 更新词缀标签 ===
            if (!string.IsNullOrEmpty(_cachedAffixPrefix))
            {
                if (_affixTextContainer == null)
                {
                    CreateAffixTextObject();
                }
    
                if (_affixLabel != null)
                {
                    if (Mathf.Abs(_affixLabel.fontSizeMin - GameConfig.AffixFontSize) > 0.1f)
                    {
                        _affixLabel.fontSizeMin = (float)GameConfig.AffixFontSize;
                        _affixLabel.fontSizeMax = (float)GameConfig.AffixFontSize + 4f;
                    }
                    
                    string displayText = _cachedAffixPrefix;
        
                    // 检查是否是封弊者占位符
                    if (displayText == "<OBSCURER_PLACEHOLDER>")
                    {
                        string garbled = ObscurerBehavior.GetCurrentGarbledText(_cachedCmc);
                        string color = ObscurerBehavior.GetCurrentRandomColor(_cachedCmc);
                        displayText = $"<color={color}>[{garbled}]</color>";
                    }
        
                    // 强制每帧更新
                    if (displayText != _lastAffixText || _cachedAffixPrefix == "<OBSCURER_PLACEHOLDER>")
                    {
                        _affixLabel.text = displayText;
                        _lastAffixText = displayText;
                    }
                }
            }
            else
            {
                // 没有词缀时清理词缀文本对象
                if (_affixTextContainer != null)
                {
                    Destroy(_affixTextContainer);
                    _affixTextContainer = null;
                    _affixLabel = null;
                    _lastAffixText = null;
                }
            }
            
            // === 2. 处理血量显示 ===
            if (!showDetail)
            {
                if (_healthTextContainer != null)
                {
                    Destroy(_healthTextContainer);
                    _healthTextContainer = null;
                    _healthValueLabel = null;
                }
                return;
            }

            if (_cachedCmc.Health == null) return;

            if (_healthTextContainer == null)
            {
                CreateHealthTextObject();
            }
            
            if (_healthValueLabel == null) return;

            int currentHp = Mathf.CeilToInt(_cachedCmc.Health.CurrentHealth);
            int maxHp = Mathf.CeilToInt(_cachedCmc.Health.MaxHealth);

            // 只在血量变化时更新
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
        
        /// <summary>
        /// 创建词缀文本对象
        /// </summary>
        private void CreateAffixTextObject()
        {
            bool showAbove = EliteEnemyCore.Config.AffixDisplayPosition == GameConfig.AffixTextDisplayPosition.Overhead;
            float yOffset = showAbove ? 55f : -125f;

            _affixTextContainer = new GameObject("EliteAffixTextObj");
            _affixTextContainer.transform.SetParent(_ownerBar.transform);
            _affixTextContainer.transform.localPosition = new Vector3(0f, yOffset, 0f);
            _affixTextContainer.transform.localScale = Vector3.one;
            _affixTextContainer.transform.localRotation = Quaternion.identity;

            GameObject textObj = new GameObject("AffixText");
            textObj.transform.SetParent(_affixTextContainer.transform);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localScale = new Vector3(1f, 1f, 1f);
            textObj.transform.localRotation = Quaternion.identity;

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
        
        /// <summary>
        /// 创建血量文本对象
        /// </summary>
        private void CreateHealthTextObject()
        {
            _healthTextContainer = new GameObject("EliteHealthTextObj");
            _healthTextContainer.transform.SetParent(_ownerBar.transform);
            _healthTextContainer.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            _healthTextContainer.transform.localScale = Vector3.one;
            _healthTextContainer.transform.localRotation = Quaternion.identity;
            
            // 尝试复用游戏内的 UI 模板，如果需要可以改为 new GameObject
            if (GameplayDataSettings.UIStyle.TemplateTextUGUI != null)
            {
                _healthValueLabel = UnityEngine.Object.Instantiate<TextMeshProUGUI>(
                    GameplayDataSettings.UIStyle.TemplateTextUGUI, 
                    _healthTextContainer.transform
                );
            }
            else
            {
                // 后备创建逻辑
                 _healthValueLabel = _healthTextContainer.AddComponent<TextMeshProUGUI>();
            }

            _healthValueLabel.gameObject.name = "HealthValueText";
            
            _healthValueLabel.alignment = TextAlignmentOptions.Center;
            _healthValueLabel.fontSizeMin = 15f;
            _healthValueLabel.fontSizeMax = 17f;
            _healthValueLabel.enableAutoSizing = true;
            _healthValueLabel.fontStyle = FontStyles.Bold;
            _healthValueLabel.enableWordWrapping = false;

            _healthValueLabel.color = new Color(0.9f, 0.9f, 1.0f);
            _healthValueLabel.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// 清理所有自定义文本对象
        /// </summary>
        private void CleanupCustomTextObjects()
        {
            if (_affixTextContainer != null)
            {
                Destroy(_affixTextContainer);
                _affixTextContainer = null;
                _affixLabel = null;
            }
            
            if (_healthTextContainer != null)
            {
                Destroy(_healthTextContainer);
                _healthTextContainer = null;
                _healthValueLabel = null;
            }
        }

        private void OnDestroy()
        {
            CleanupCustomTextObjects();
        }
    }
}