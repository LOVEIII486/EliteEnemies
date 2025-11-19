using System.Text;
using UnityEngine;
using Duckov.UI;
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
        private TextMeshProUGUI _nameLabel;
        private MonoBehaviour _randomNpcController; 
        
        private Health _cachedTarget;
        private CharacterMainControl _cachedCmc;
        private EliteEnemyCore.EliteMarker _cachedMarker;
        
        private string _cachedBaseString = null;
        private int _lastHp = -1;
        private int _lastMaxHp = -1;
        private string _lastFinalText = null;
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
            _lastFinalText = null; 
            _cachedTarget = null;
        }

        private void LateUpdate()
        {
            if (!_ownerBar || !_nameLabel) return;

            // 1. 检查 Target 是否变化 (对象池复用逻辑)
            if (_ownerBar.target != _cachedTarget)
            {
                RefreshTarget(_ownerBar.target);
            }

            // 如果当前认为不是精英，再次尝试获取组件
            // 因为 EliteMarker 可能在血条生成后的几帧内才被 AddComponent
            if (!_isElite && _cachedCmc)
            {
                var marker = _cachedCmc.GetComponent<EliteEnemyCore.EliteMarker>();
                if (marker != null && marker.Affixes.Count > 0)
                {
                    RefreshTarget(_cachedTarget);
                }
            }

            // 2. 如果不是精英怪，尝试归还控制权
            if (!_isElite)
            {
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
                // 前缀处理：如果没有词缀，就不显示前缀
                string prefix = "";
                if (_cachedMarker.Affixes != null && _cachedMarker.Affixes.Count > 0)
                {
                    prefix = EliteEnemyCore.BuildColoredPrefix(_cachedMarker.Affixes);
                }

                // 名字解析逻辑
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

                _cachedBaseString = $"{prefix}{baseName}";
                
                // 强制开启名字显示
                if (_cachedCmc.characterPreset != null)
                    _cachedCmc.characterPreset.showName = true;
            }

            // 重置显示状态
            _lastHp = -1;
            _lastFinalText = null;
        }

        private void UpdateEliteUI()
        {
            bool showDetail = EliteEnemyCore.Config.ShowDetailedHealth;
            
            // 静态文本模式 
            if (!showDetail)
            {
                // 即使 _lastFinalText 没变，也要检查 _nameLabel.text 是否被别人改了
                if (_nameLabel.text != _cachedBaseString)
                {
                    _nameLabel.text = _cachedBaseString;
                    _lastFinalText = _cachedBaseString;
                }
                return;
            }

            // 动态血量模式
            if (_cachedCmc.Health == null) return;

            int currentHp = Mathf.CeilToInt(_cachedCmc.Health.CurrentHealth);
            int maxHp = Mathf.CeilToInt(_cachedCmc.Health.MaxHealth);

            // 只有数值变化 或 文本被外部篡改时 才重新计算
            // 这里增加了一个 check，防止 RandomNpc 在我们之后运行覆盖了文本
            string currentLabelText = _nameLabel.text;
            
            if (currentHp != _lastHp || maxHp != _lastMaxHp || _lastFinalText == null || currentLabelText != _lastFinalText)
            {
                _lastHp = currentHp;
                _lastMaxHp = maxHp;

                _sb.Clear();
                _sb.Append(_cachedBaseString);
                _sb.Append(" <color=#FFD700>[");
                _sb.Append(currentHp);
                _sb.Append("/");
                _sb.Append(maxHp);
                _sb.Append("]</color>");

                string newText = _sb.ToString();
                
                // 最终赋值
                if (currentLabelText != newText)
                {
                    _nameLabel.text = newText;
                    _lastFinalText = newText;
                }
            }
        }

        private void DisableRandomNpcController()
        {
            if (_randomNpcController == null)
            {
                Component[] components = GetComponents<MonoBehaviour>();
                foreach (var comp in components)
                {
                    // 字符串查找，解耦
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
    }
}