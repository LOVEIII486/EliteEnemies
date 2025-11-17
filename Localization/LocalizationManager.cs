using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SodaCraft.Localizations;

namespace EliteEnemies
{
    /// <summary>
    /// 精英敌人模组本地化管理器
    /// 自动加载并管理多语言CSV文件
    /// </summary>
    public static class LocalizationManager
    {
        private const string LogTag = "[EliteEnemies.Localization]";
        private const string LocalizationFolderName = "Localization";
        private const string CsvFilePattern = "*.csv";
        
        private static readonly Dictionary<SystemLanguage, CSVFileLocalizor> _providers = new Dictionary<SystemLanguage, CSVFileLocalizor>();
        private static SystemLanguage _currentLanguage;
        private static CSVFileLocalizor _currentProvider;
        private static bool _isInitialized = false;

        // ==================== 初始化 ====================

        /// <summary>
        /// 初始化本地化系统
        /// </summary>
        public static void Initialize(string modDirectory)
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"{LogTag} 本地化系统已初始化，跳过重复初始化");
                return;
            }

            try
            {
                DetermineCurrentLanguage();
                LoadLanguageFiles(modDirectory);
                SetCurrentProvider(_currentLanguage);

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 初始化失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 刷新本地化数据（语言切换时调用）
        /// </summary>
        public static void Refresh()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning($"{LogTag} 尚未初始化，跳过刷新");
                return;
            }

            try
            {
                DetermineCurrentLanguage();
                SetCurrentProvider(_currentLanguage);

                if (_currentProvider != null)
                {
                    _currentProvider.BuildDictionary();
                    Debug.Log($"{LogTag} 语言已切换: {_currentLanguage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 刷新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {
            _providers.Clear();
            _currentProvider = null;
            _isInitialized = false;
        }

        // ==================== 公共接口 ====================

        /// <summary>
        /// 获取本地化文本
        /// </summary>
        /// <param name="key">文本键</param>
        /// <param name="fallback">找不到时返回的默认值</param>
        public static string GetText(string key, string fallback = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning($"{LogTag} 尚未初始化，返回默认值: {key}");
                return fallback ?? key;
            }

            if (_currentProvider == null)
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
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 获取文本失败 (key={key}): {ex.Message}");
            }

            return fallback ?? key;
        }

        // ==================== 内部方法 ====================

        /// <summary>
        /// 确定当前语言
        /// </summary>
        private static void DetermineCurrentLanguage()
        {
            _currentLanguage = SodaCraft.Localizations.LocalizationManager.Initialized
                ? SodaCraft.Localizations.LocalizationManager.CurrentLanguage
                : Application.systemLanguage;
        }

        /// <summary>
        /// 加载目录下所有语言文件
        /// </summary>
        private static void LoadLanguageFiles(string modDirectory)
        {
            string localizationPath = Path.Combine(modDirectory, LocalizationFolderName);

            if (!Directory.Exists(localizationPath))
            {
                Debug.LogError($"{LogTag} 本地化目录不存在: {localizationPath}");
                return;
            }

            var csvFiles = Directory.GetFiles(localizationPath, CsvFilePattern);

            if (csvFiles.Length == 0)
            {
                Debug.LogWarning($"{LogTag} 在 {localizationPath} 中未找到任何CSV文件");
                return;
            }

            foreach (var filePath in csvFiles)
            {
                LoadLanguageFile(filePath);
            }

            if (_providers.Count == 0)
            {
                Debug.LogWarning($"{LogTag} 未加载任何有效的语言文件");
            }
            else
            {
                Debug.Log($"{LogTag} 已加载 {_providers.Count} 个语言文件");
            }
        }

        /// <summary>
        /// 加载单个语言文件
        /// </summary>
        private static void LoadLanguageFile(string filePath)
        {
            try
            {
                var provider = new CSVFileLocalizor(filePath);

                if (provider.Language != SystemLanguage.Unknown)
                {
                    _providers[provider.Language] = provider;
                    Debug.Log($"{LogTag} 已加载: {Path.GetFileName(filePath)} ({provider.Language})");
                }
                else
                {
                    Debug.LogWarning($"{LogTag} 跳过未知语言文件: {Path.GetFileName(filePath)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 加载文件失败 {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置当前语言提供者
        /// </summary>
        private static void SetCurrentProvider(SystemLanguage language)
        {
            if (_providers.TryGetValue(language, out var provider))
            {
                _currentProvider = provider;
                Debug.Log($"{LogTag} 使用语言: {language}");
            }
            else if (_providers.TryGetValue(SystemLanguage.Chinese, out var chineseFallback))
            {
                _currentProvider = chineseFallback;
                Debug.LogWarning($"{LogTag} 语言 {language} 不可用，使用中文作为后备");
            }
            else if (_providers.TryGetValue(SystemLanguage.English, out var englishFallback))
            {
                _currentProvider = englishFallback;
                Debug.LogWarning($"{LogTag} 语言 {language} 不可用，使用英文作为后备");
            }
            else if (_providers.Count > 0)
            {
                foreach (var kvp in _providers)
                {
                    _currentProvider = kvp.Value;
                    Debug.LogWarning($"{LogTag} 语言 {language} 不可用，使用 {kvp.Key} 作为后备");
                    break;
                }
            }
            else
            {
                Debug.LogError($"{LogTag} 没有可用的语言提供者");
            }
        }
    }
}