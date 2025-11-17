using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.Effects
{
    /// <summary>
    /// 精英光环管理器
    /// 负责光环对象池管理和生命周期
    /// </summary>
    public class EliteAuraManager : MonoBehaviour
    {
        // ========== 常量配置 ==========
        private const string LogTag = "[EliteEnemies.AuraManager]";
        private const int PoolPrewarmSize = 10;
        private const int PoolMaxSize = 60;
        private const float CleanupInterval = 5f;

        // ========== 单例 ==========
        private static EliteAuraManager _instance;
        public static EliteAuraManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("EliteAuraManager");
                    _instance = go.AddComponent<EliteAuraManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ========== 私有字段 ==========
        private readonly List<EliteAuraPoolable> _availableAuras = new List<EliteAuraPoolable>();
        private readonly List<EliteAuraPoolable> _activeAuras = new List<EliteAuraPoolable>();
        private GameObject _auraPrefab;
        private Transform _poolParent;

        // ========== Unity 生命周期 ==========

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePool();
                InvokeRepeating(nameof(CleanupDeadAuras), CleanupInterval, CleanupInterval);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        // ========== 初始化 ==========

        private void InitializePool()
        {
            GameObject poolObj = new GameObject("EliteAuraPool");
            _poolParent = poolObj.transform;
            _poolParent.SetParent(transform, false);

            _auraPrefab = CreateAuraPrefab();

            for (int i = 0; i < PoolPrewarmSize; i++)
            {
                EliteAuraPoolable aura = CreateNewAura();
                aura.NotifyPooled();
                _availableAuras.Add(aura);
            }

            Debug.Log($"{LogTag} 对象池初始化完成，预热: {PoolPrewarmSize}，上限: {PoolMaxSize}");
        }

        private GameObject CreateAuraPrefab()
        {
            GameObject prefab = new GameObject("EliteAuraPrefab");
            prefab.AddComponent<EliteAuraPoolable>();
            prefab.transform.SetParent(_poolParent, false);
            return prefab;
        }

        private EliteAuraPoolable CreateNewAura()
        {
            GameObject instance = Instantiate(_auraPrefab, _poolParent);
            instance.name = $"EliteAura_{_availableAuras.Count + _activeAuras.Count}";

            EliteAuraPoolable aura = instance.GetComponent<EliteAuraPoolable>();
            aura.OnCreated();
            return aura;
        }

        // ========== 清理 ==========

        private void CleanupDeadAuras()
        {
            int cleanedCount = 0;

            for (int i = _activeAuras.Count - 1; i >= 0; i--)
            {
                EliteAuraPoolable aura = _activeAuras[i];
                if (aura == null || !aura.IsTargetValid())
                {
                    ReleaseAura(aura);
                    cleanedCount++;
                }
            }

            if (cleanedCount > 0)
            {
                Debug.Log($"{LogTag} 清理了 {cleanedCount} 个无效光环，当前活跃: {_activeAuras.Count}");
            }
        }

        // ========== 公共接口 ==========

        public EliteAuraPoolable CreateAura(CharacterMainControl character, Color color)
        {
            return CreateAura(character, color, null);
        }

        public EliteAuraPoolable CreateAura(CharacterMainControl character, Color color, IList<string> affixNames)
        {
            if (character == null)
            {
                Debug.LogWarning($"{LogTag} 目标角色为空");
                return null;
            }

            EliteAuraPoolable aura;
            if (_availableAuras.Count > 0)
            {
                int lastIndex = _availableAuras.Count - 1;
                aura = _availableAuras[lastIndex];
                _availableAuras.RemoveAt(lastIndex);
            }
            else
            {
                if (_activeAuras.Count >= PoolMaxSize)
                {
                    Debug.LogWarning($"{LogTag} 达到对象池上限: {PoolMaxSize}");
                    return null;
                }
                aura = CreateNewAura();
            }

            aura.transform.SetParent(null);
            aura.NotifyReleased();
            aura.Initialize(character, color);
            aura.SetAffixNames(affixNames);

            _activeAuras.Add(aura);

            return aura;
        }

        public void ReleaseAura(EliteAuraPoolable aura)
        {
            if (aura == null) return;

            _activeAuras.Remove(aura);

            if (_availableAuras.Count < PoolMaxSize)
            {
                aura.NotifyPooled();
                aura.transform.SetParent(_poolParent, false);
                _availableAuras.Add(aura);
            }
            else
            {
                aura.OnDestroying();
                Destroy(aura.gameObject);
            }
        }

        public void ClearAll()
        {
            EliteAuraPoolable[] snapshot = _activeAuras.ToArray();
            foreach (EliteAuraPoolable aura in snapshot)
            {
                ReleaseAura(aura);
            }
        }
    }
}