using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace EliteEnemies
{
    /// <summary>
    /// Harmony 库加载器
    /// 从嵌入资源加载 0Harmony.dll 或复用进程中已有的程序集
    /// </summary>
    public static class HarmonyLoad
    {
        private const string LogTag = "[EliteEnemies.HarmonyLoad]";
        private static Assembly _harmonyAssembly;

        /// <summary>
        /// 加载 Harmony 程序集
        /// </summary>
        public static Assembly Load0Harmony()
        {
            if (_harmonyAssembly != null)
            {
                return _harmonyAssembly;
            }

            // 检查进程中是否已有 Harmony
            _harmonyAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a =>
                {
                    string name = a.GetName().Name;
                    return name.Equals("0Harmony", StringComparison.OrdinalIgnoreCase) ||
                           name.Equals("HarmonyLib", StringComparison.OrdinalIgnoreCase);
                });

            if (_harmonyAssembly != null)
            {
                Debug.Log($"{LogTag} 使用已加载的 Harmony: {_harmonyAssembly.FullName}");
                return _harmonyAssembly;
            }

            // 从嵌入资源加载
            try
            {
                Assembly executingAssembly = Assembly.GetExecutingAssembly();
                string resourceName = executingAssembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(".0Harmony.dll", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(resourceName))
                {
                    Debug.LogError($"{LogTag} 未找到嵌入资源 '.0Harmony.dll'，请确保 Build Action = Embedded Resource");
                    return null;
                }

                using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Debug.LogError($"{LogTag} 无法读取嵌入资源: {resourceName}");
                        return null;
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        byte[] assemblyData = ms.ToArray();

                        if (assemblyData.Length == 0)
                        {
                            Debug.LogError($"{LogTag} 嵌入资源为空");
                            return null;
                        }

                        _harmonyAssembly = Assembly.Load(assemblyData);
                        Debug.Log($"{LogTag} 已从嵌入资源加载 Harmony: {_harmonyAssembly.FullName}");
                        return _harmonyAssembly;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 加载 Harmony 失败: {ex.Message}");
                return null;
            }
        }
    }
}