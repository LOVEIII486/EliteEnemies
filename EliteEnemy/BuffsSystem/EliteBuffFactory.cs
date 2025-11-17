using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Duckov.Buffs;
using Duckov.Utilities;

namespace EliteEnemies.BuffsSystem
{
    /// <summary>
    /// 精英 Buff 工厂 - 统一创建和管理精英专用 Buff
    /// </summary>
    public static class EliteBuffFactory
    {
        private const string LogTag = "[EliteEnemies.BuffFactory]";

        // 缓存已创建的共享 Buff
        private static readonly Dictionary<int, Buff> SharedBuffs = new Dictionary<int, Buff>();

        // 反射缓存
        private static FieldInfo _idField;
        private static FieldInfo _limitedLifeTimeField;
        private static FieldInfo _totalLifeTimeField;
        private static bool _fieldsInitialized = false;

        /// <summary>
        /// Buff 配置
        /// </summary>
        public struct BuffConfig
        {
            public string Name;
            public int Id;
            public bool LimitedLifeTime;
            public float Duration;

            public BuffConfig(string name, int id, float duration = 5f, bool limitedLifeTime = true)
            {
                Name = name;
                Id = id;
                Duration = duration;
                LimitedLifeTime = limitedLifeTime;
            }
        }

        /// <summary>
        /// 创建或获取共享 Buff（单例模式）
        /// </summary>
        public static Buff GetOrCreateSharedBuff(BuffConfig config)
        {
            // 如果已存在，直接返回
            if (SharedBuffs.TryGetValue(config.Id, out Buff existingBuff))
            {
                if (existingBuff != null)
                    return existingBuff;
                else
                    SharedBuffs.Remove(config.Id); // 清理无效引用
            }

            // 创建新的共享 Buff
            return CreateSharedBuff(config);
        }

        /// <summary>
        /// 创建共享 Buff
        /// </summary>
        private static Buff CreateSharedBuff(BuffConfig config)
        {
            try
            {
                // 获取 BaseBuff
                Buff baseBuff = GameplayDataSettings.Buffs.BaseBuff;
                if (baseBuff == null)
                {
                    Debug.LogError($"{LogTag} 找不到 BaseBuff");
                    return null;
                }

                // 实例化
                Buff newBuff = UnityEngine.Object.Instantiate(baseBuff);
                newBuff.name = config.Name;
                UnityEngine.Object.DontDestroyOnLoad(newBuff.gameObject);

                // 初始化反射字段
                if (!_fieldsInitialized)
                {
                    InitializeReflectionFields();
                }

                // 设置 Buff 属性
                SetBuffProperties(newBuff, config);

                // 缓存
                SharedBuffs[config.Id] = newBuff;

                Debug.Log($"{LogTag} 创建共享 Buff: {config.Name} (ID:{config.Id}, 持续:{config.Duration}秒)");
                return newBuff;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 创建 Buff 失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 初始化反射字段（只执行一次）
        /// </summary>
        private static void InitializeReflectionFields()
        {
            if (_fieldsInitialized) return;

            var buffType = typeof(Buff);
            _idField = buffType.GetField("id", BindingFlags.Instance | BindingFlags.NonPublic);
            _limitedLifeTimeField = buffType.GetField("limitedLifeTime", BindingFlags.Instance | BindingFlags.NonPublic);
            _totalLifeTimeField = buffType.GetField("totalLifeTime", BindingFlags.Instance | BindingFlags.NonPublic);

            _fieldsInitialized = true;
        }

        /// <summary>
        /// 设置 Buff 属性（通过反射）
        /// </summary>
        private static void SetBuffProperties(Buff buff, BuffConfig config)
        {
            if (_idField != null)
                _idField.SetValue(buff, config.Id);

            if (_limitedLifeTimeField != null)
                _limitedLifeTimeField.SetValue(buff, config.LimitedLifeTime);

            if (_totalLifeTimeField != null)
                _totalLifeTimeField.SetValue(buff, config.Duration);
        }

        /// <summary>
        /// 安全地给玩家添加 Buff
        /// </summary>
        public static bool TryAddBuffToPlayer(Buff buff, CharacterMainControl attacker, int stackCount = 0)
        {
            if (buff == null)
            {
                Debug.LogWarning($"{LogTag} Buff 为 null，无法添加");
                return false;
            }

            var player = CharacterMainControl.Main;
            if (player == null)
            {
                Debug.LogWarning($"{LogTag} 找不到玩家");
                return false;
            }

            try
            {
                player.AddBuff(buff, attacker, stackCount);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 添加 Buff 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清理所有共享 Buff（游戏结束时调用）
        /// </summary>
        public static void ClearAll()
        {
            foreach (var kvp in SharedBuffs)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value.gameObject);
                }
            }
            SharedBuffs.Clear();
            Debug.Log($"{LogTag} 已清理所有共享 Buff");
        }

        /// <summary>
        /// 获取已缓存的 Buff 数量
        /// </summary>
        public static int CachedBuffCount => SharedBuffs.Count;
    }
}