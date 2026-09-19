using System;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 「等联机模组出现」的**低频泵**。
    ///
    /// <para><b>为什么必须有它</b>：联机模组的程序集若在本模组**之后**加载，
    /// 我们只能靠 <c>AppDomain.AssemblyLoad</c> 事件知道它来了。而那个回调是
    /// <b>mono 在 <c>DoAssemblyLoad</c> 内部同步调用的</b>——那一刻新程序集
    /// <b>还没注册完</b>，对它（或此刻的全量程序集）做反射可能让 mono
    /// <b>原生崩溃</b>：实测玩家日志里崩在
    /// <c>System.Reflection.Assembly:InternalGetType</c> 与
    /// <c>RuntimeType:GetPropertiesByName_native</c>，而调用者正是本模块的
    /// <c>FindType</c> / <c>GetProperty</c>（完整栈见 <c>docs/当前状态.md</c>）。</para>
    ///
    /// <para>所以那个回调里<b>只记一个待办</b>，真正的探测挪到这里按低频执行。
    /// 延迟一次轮询（≤0.25 秒）对玩家不可感知，却把「在程序集加载中途做反射」
    /// 整类问题消掉。</para>
    ///
    /// <para>⚠ 只有「<b>联机模组比本模组晚加载</b>」时才会创建这个泵；
    /// 顺序相反时 <see cref="CoopApi.Initialize"/> 一次就探到了，它根本不存在。
    /// 这正是作者自测（联机模组在前，也是文档推荐的顺序）碰不到那个崩溃的原因。</para>
    ///
    /// <para>⚠ 这里一律用 <c>Debug.Log</c> 而**不是** <c>CoopLog</c>：走到这个泵
    /// 就意味着联机 API **还没激活**，那时联机模组的日志过滤器也还没装上——
    /// 而 <c>CoopLog</c> 是借 <c>Debug.LogError</c> 穿透过滤器的，
    /// 在这里用会给每个单机玩家每次启动刷一条红字。
    /// 同一条判据见 <see cref="CoopApi.Initialize"/> 里那句注释。</para>
    /// </summary>
    internal sealed class CoopActivationPump : MonoBehaviour
    {
        /// <summary>轮询间隔。取值只影响「联机模组到位后多久接上」，0.25 秒对人不可感知。</summary>
        private const float Interval = 0.25f;

        private static CoopActivationPump s_instance;

        private float _nextAt;

        public static void EnsureInstalled()
        {
            if (s_instance != null) return;

            try
            {
                var go = new GameObject("[EliteEnemies.Coop.ActivationPump]")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<CoopActivationPump>();
            }
            catch (Exception ex)
            {
                // 起不来只是「联机模组晚加载时接不上」，不该拦住任何东西。
                Debug.Log($"[EliteEnemies.Coop] 接入轮询器创建失败（联机模组若晚于本模组加载，将无法自动接入）: {ex.Message}");
            }
        }

        public static void Remove()
        {
            if (s_instance == null) return;

            try
            {
                UnityEngine.Object.Destroy(s_instance.gameObject);
            }
            catch
            {
                // 卸载路径不抛
            }

            s_instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextAt) return;
            _nextAt = Time.unscaledTime + Interval;

            try
            {
                CoopApi.PumpActivation();
            }
            catch (Exception ex)
            {
                // 兜异常：这是个每 0.25 秒跑一次的循环，漏出去会变成刷屏。
                Debug.Log($"[EliteEnemies.Coop] 接入轮询失败（下次再试）: {ex.Message}");
            }
        }
    }
}
