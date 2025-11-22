using System.Text;
using UnityEngine;
using Duckov.UI;
using Duckov.Utilities;
using EliteEnemies.Settings;
using TMPro;

namespace EliteEnemies.VisualEffects
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
        private TextMeshProUGUI _nameLabel;           // 原名字标签，现在只显示基础名字
        private GameObject _affixTextContainer;       // 词缀文本容器
        private TextMeshProUGUI _affixLabel;          // 词缀标签
        private GameObject _healthTextContainer;      // 血量文本容器
        private TextMeshProUGUI _healthValueLabel;    // 血量数值标签
        private MonoBehaviour _randomNpcController; 
        
        private Health _cachedTarget;
        private CharacterMainControl _cachedCmc;
        private EliteEnemyCore.EliteMarker _cachedMarker;
        
        private string _cachedBaseName = null;        // 只保存基础名字
        private string _cachedAffixPrefix = null;     // 保存词缀前缀
        private int _lastHp = -1;
        private int _lastMaxHp = -1;
        private string _lastNameText = null;          // 名字文本缓存
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
            _lastNameText = null;
            _lastAffixText = null;
            _lastHealthText = null; 
            _cachedTarget = null;
        }

        private void LateUpdate()
        {
            if (!_ownerBar || !_nameLabel) return;
            
            if (!EliteEnemyCore.Config.ShowEliteName)
            {
                // 清理所有自定义文本对象
                CleanupCustomTextObjects();
                
                // 确保不误伤 RandomNpc
                if (_randomNpcController != null && !_randomNpcController.enabled)
                {
                    _randomNpcController.enabled = true;
                }
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
                
                if (_randomNpcController != null && !_randomNpcController.enabled)
                {
                    _randomNpcController.enabled = true;
                }
                return;
            }

            // 3. 是精英怪：禁用 RandomNpc 组件
            DisableRandomNpcController();

            // 4. 更新 UI
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

            // 判定条件放宽：只要有词缀 OR 有自定义名字，都算作我们需要接管的对象
            _isElite = _cachedMarker != null && 
                       (_cachedMarker.Affixes.Count > 0 || !string.IsNullOrEmpty(_cachedMarker.CustomDisplayName));
            
            if (_isElite)
            {
                // === 分离前缀和名字 ===
                
                // 词缀前缀处理
                _cachedAffixPrefix = "";
                if (_cachedMarker.Affixes != null && _cachedMarker.Affixes.Count > 0)
                {
                    _cachedAffixPrefix = EliteEnemyCore.BuildColoredPrefix(_cachedMarker.Affixes);
                }

                // 基础名字解析
                string baseName;
                if (!string.IsNullOrEmpty(_cachedMarker.CustomDisplayName))
                {
                    baseName = _cachedMarker.CustomDisplayName;
                }
                else if (!string.IsNullOrEmpty(_cachedMarker.BaseName))
                {
                    baseName = _cachedMarker.BaseName;
                }
                else
                {
                    baseName = EliteEnemyCore.ResolveBaseName(_cachedCmc);
                }

                if (baseName.Contains("_")) baseName = "???";

                _cachedBaseName = baseName;
                
                // 强制开启名字显示
                if (_cachedCmc.characterPreset != null)
                {
                    _cachedCmc.characterPreset.showName = true;
                    _cachedCmc.characterPreset.showHealthBar = true;
                }
            }

            // 重置显示状态
            _lastHp = -1;
            _lastNameText = null;
            _lastAffixText = null;
            _lastHealthText = null;
        }

        private void UpdateEliteUI()
        {
            bool showDetail = EliteEnemyCore.Config.ShowDetailedHealth;
            
            // === 1. 更新名字标签（只显示基础名字） ===
            if (_nameLabel.text != _cachedBaseName)
            {
                _nameLabel.text = _cachedBaseName;
                _lastNameText = _cachedBaseName;
            }
            
            // === 2. 更新词缀标签 ===
            if (!string.IsNullOrEmpty(_cachedAffixPrefix))
            {
                // 确保词缀文本对象存在
                if (_affixTextContainer == null)
                {
                    CreateAffixTextObject();
                }
                
                if (_affixLabel != null && _lastAffixText != _cachedAffixPrefix)
                {
                    _affixLabel.text = _cachedAffixPrefix;
                    _lastAffixText = _cachedAffixPrefix;
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
            
            // === 3. 处理血量显示 ===
            if (!showDetail)
            {
                // 不显示详细血量 - 销毁血量文本对象
                if (_healthTextContainer != null)
                {
                    Destroy(_healthTextContainer);
                    _healthTextContainer = null;
                    _healthValueLabel = null;
                }
                return;
            }

            // 显示详细血量 - 动态血量模式
            if (_cachedCmc.Health == null) return;

            // 确保血量文本对象存在
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
            // ============ 根据配置决定词缀文本的位置 ============
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
            textObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            textObj.transform.localRotation = Quaternion.identity;

            _affixLabel = textObj.AddComponent<TextMeshProUGUI>();
    
            _affixLabel.alignment = TextAlignmentOptions.Center;
            _affixLabel.fontSizeMin = 20f;
            _affixLabel.fontSizeMax = 24f;
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
            
            _healthValueLabel = UnityEngine.Object.Instantiate<TextMeshProUGUI>(
                GameplayDataSettings.UIStyle.TemplateTextUGUI, 
                _healthTextContainer.transform
            );
            _healthValueLabel.gameObject.name = "HealthValueText";
            
            _healthValueLabel.transform.localPosition = Vector3.zero;
            _healthValueLabel.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            _healthValueLabel.transform.localRotation = Quaternion.identity;
            
            _healthValueLabel.alignment = TextAlignmentOptions.Center;
            _healthValueLabel.fontSizeMin = 15f;
            _healthValueLabel.fontSizeMax = 18f;
            _healthValueLabel.enableAutoSizing = true;
            _healthValueLabel.fontStyle = FontStyles.Bold;
            _healthValueLabel.enableWordWrapping = false;

            _healthValueLabel.color = new Color(0.9f, 0.9f, 1.0f);
            _healthValueLabel.overflowMode = TextOverflowModes.Overflow;
            
            _healthValueLabel.fontWeight = FontWeight.Black;
            _healthValueLabel.fontMaterial.EnableKeyword("OUTLINE_ON");
            _healthValueLabel.outlineWidth = 0.4f;
            _healthValueLabel.outlineColor = new Color(0.1f, 0.1f, 0.1f);
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

        private void DisableRandomNpcController()
        {
            if (_randomNpcController == null)
            {
                Component[] components = GetComponents<MonoBehaviour>();
                foreach (var comp in components)
                {
                    if (comp.GetType().FullName.Contains("HealthBarNameController"))
                    {
                        _randomNpcController = (MonoBehaviour)comp;
                        break;
                    }
                }
            }

            if (_randomNpcController != null && _randomNpcController.enabled)
            {
                _randomNpcController.enabled = false;
            }
        }

        private void OnDestroy()
        {
            CleanupCustomTextObjects();
        }
    }
}