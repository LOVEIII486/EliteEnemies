using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EliteEnemies.Localization
{
    /// <summary>
    /// 本地化管理器
    /// </summary>
    public static class LocalizationManager
    {
        private const string LogTag = "[EliteEnemies.Localization]";
        private const string LocalizationFolderName = "Localization";
        
        // 缓存已加载的语言提供者，避免重复IO
        private static readonly Dictionary<SystemLanguage, CSVFileLocalizor> LoadedProviders = new Dictionary<SystemLanguage, CSVFileLocalizor>();
        
        private static SystemLanguage _currentLanguage;
        private static CSVFileLocalizor _currentProvider;
        private static string _modDirectory; // 缓存模组路径供Refresh使用
        private static bool _isInitialized = false;

        // ==================== 初始化 ====================

        public static void Initialize(string modDirectory)
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"{LogTag} 本地化系统已初始化，跳过重复初始化");
                return;
            }

            _modDirectory = modDirectory;

            try
            {
                DetermineCurrentLanguage();
                
                // 尝试加载当前语言，如果失败则执行后备逻辑
                if (!LoadAndSetLanguage(_currentLanguage))
                {
                    Debug.LogWarning($"{LogTag} 无法加载语言 {_currentLanguage}，尝试后备语言...");
                    
                    // 优先使用英文作为后备
                    if (!LoadAndSetLanguage(SystemLanguage.English))
                    {
                        // 英文也失败了，尝试中文作为最后的保底
                        if (!LoadAndSetLanguage(SystemLanguage.ChineseSimplified) &&
                            !LoadAndSetLanguage(SystemLanguage.Chinese))
                        {
                            Debug.LogError($"{LogTag} 严重错误：无法加载任何语言文件（English/Chinese）！");
                        }
                    }
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 初始化失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 刷新本地化数据
        /// </summary>
        public static void Refresh()
        {
            if (!_isInitialized) return;

            try
            {
                var oldLanguage = _currentLanguage;
                DetermineCurrentLanguage();

                // 只有语言确实改变了，或者当前没有Provider时才重新加载
                if (_currentLanguage != oldLanguage || _currentProvider == null)
                {
                    if (LoadAndSetLanguage(_currentLanguage))
                    {
                        Debug.Log($"{LogTag} 语言已刷新: {_currentLanguage}");
                    }
                    else
                    {
                        // 如果新语言加载失败，保持旧的Provider或尝试后备
                        Debug.LogWarning($"{LogTag} 切换到 {_currentLanguage} 失败，尝试使用后备语言");
                        if (_currentProvider == null)
                        {
                            LoadAndSetLanguage(SystemLanguage.English);
                        }
                    }
                }
                
                // 重建字典
                _currentProvider?.BuildDictionary();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 刷新失败: {ex.Message}");
            }
        }

        public static void Cleanup()
        {
            LoadedProviders.Clear();
            _currentProvider = null;
            _modDirectory = null;
            _isInitialized = false;
        }

        // ==================== 公共接口 ====================

        public static string GetText(string key, string fallback = null)
        {
            if (!_isInitialized || _currentProvider == null)
            {
                return fallback ?? key;
            }

            try
            {
                if (_currentProvider.HasKey(key))
                {
                    return _currentProvider.Get(key);
                }
            }
            catch
            {
                // 忽略频繁的查找错误，避免刷屏
            }

            return fallback ?? key;
        }

        // ==================== 内部逻辑 ====================

        private static void DetermineCurrentLanguage()
        {
            _currentLanguage = SodaCraft.Localizations.LocalizationManager.Initialized
                ? SodaCraft.Localizations.LocalizationManager.CurrentLanguage
                : Application.systemLanguage;
        }

        /// <summary>
        /// 尝试加载并设置指定的语言
        /// </summary>
        /// <returns>是否成功设置了Provider</returns>
        private static bool LoadAndSetLanguage(SystemLanguage language)
        {
            // 1. 检查缓存
            if (LoadedProviders.TryGetValue(language, out var cachedProvider))
            {
                _currentProvider = cachedProvider;
                return true;
            }

            // 2. 检查文件是否存在
            string fileName = GetLanguageFileName(language);
            string filePath = Path.Combine(_modDirectory, LocalizationFolderName, fileName);

            if (!File.Exists(filePath))
            {
                // 针对中文的特殊处理：如果请求的是Chinese但没找到，尝试ChineseSimplified
                if (language == SystemLanguage.Chinese)
                {
                    string simPath = Path.Combine(_modDirectory, LocalizationFolderName, "ChineseSimplified.csv");
                    if (File.Exists(simPath))
                    {
                        filePath = simPath;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            // 3. 加载文件
            try
            {
                var provider = new CSVFileLocalizor(filePath);
                LoadedProviders[language] = provider;
                _currentProvider = provider;
                
                Debug.Log($"{LogTag} 已加载并切换语言: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 加载文件失败 {fileName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将系统语言映射到文件名
        /// </summary>
        private static string GetLanguageFileName(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    return "ChineseSimplified.csv";
                case SystemLanguage.ChineseTraditional:
                    return "ChineseTraditional.csv";
                case SystemLanguage.English:
                    return "English.csv";
                case SystemLanguage.Japanese:
                    return "Japanese.csv";
                case SystemLanguage.Korean:
                    return "Korean.csv";
                case SystemLanguage.Russian:
                    return "Russian.csv";
                default:
                    return $"{language}.csv";
            }
        }
    }
}