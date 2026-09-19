using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 卡顿监视：记录**超过阈值的帧**与**场景加载边界**，带时间戳写进本模块的日志。
    ///
    /// <para><b>为什么需要它</b>：<c>Player.log</c> 没有时间戳，联机模组又把普通日志吞了，
    /// 所以"客机卡死在加载页面"这类问题<b>无从下手</b>——既不知道卡在哪一刻，
    /// 也不知道卡了多久、当时在做什么。这个类就是给那段空白补上刻度。</para>
    ///
    /// <para><b>单机下零开销</b>：它只在联机 API 激活后才被创建
    /// （见 <see cref="CoopEliteSync.Initialize"/>）。单机玩家永远不会走到这里，
    /// 连这个 <c>MonoBehaviour</c> 都不会被实例化——符合
    /// <c>docs\联机兼容可行性分析.md</c> §6 对单机零退化的要求。</para>
    ///
    /// <para>⚠️ <b>这是排查期的诊断设施</b>。按 <c>AGENT.md §3.8</c>，
    /// 卡顿问题定位后应当重新评估它是否还需要常开。</para>
    /// </summary>
    internal sealed class CoopStallWatch : MonoBehaviour
    {
        /// <summary>单帧超过这个秒数就记一条。</summary>
        private const float StallThreshold = 1.0f;

        /// <summary>卡顿条目的记录上限——持续卡顿时不至于把日志刷爆。</summary>
        private const int MaxStallEntries = 60;

        private static CoopStallWatch s_instance;

        private int s_stallCount;
        private int s_suppressed;
        private float s_worstFrame;
        private int s_frames;
        private float s_sceneStartedAt;
        private string s_sceneName = "(未知)";

        public static void Install()
        {
            if (s_instance != null) return;

            try
            {
                var go = new GameObject("[EliteEnemies.Coop.StallWatch]")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<CoopStallWatch>();

                SceneManager.sceneLoaded += s_instance.OnSceneLoaded;
                SceneManager.sceneUnloaded += s_instance.OnSceneUnloaded;

                CoopLog.Info("[卡顿监视] 已启动（阈值 " + StallThreshold.ToString("0.0") + " 秒/帧）");
            }
            catch (Exception ex)
            {
                CoopLog.Warn($"[卡顿监视] 启动失败，将不监视卡顿：{ex.Message}");
            }
        }

        public static void Uninstall()
        {
            if (s_instance == null) return;

            try
            {
                SceneManager.sceneLoaded -= s_instance.OnSceneLoaded;
                SceneManager.sceneUnloaded -= s_instance.OnSceneUnloaded;
            }
            catch
            {
                // 卸载路径不抛
            }

            try
            {
                UnityEngine.Object.Destroy(s_instance.gameObject);
            }
            catch
            {
                // 同上
            }

            s_instance = null;
        }

        private void Update()
        {
            try
            {
                float dt = Time.unscaledDeltaTime;
                s_frames++;

                if (dt > s_worstFrame) s_worstFrame = dt;
                if (dt <= StallThreshold) return;

                s_stallCount++;

                if (s_stallCount > MaxStallEntries)
                {
                    // 持续卡顿时只计数，不再逐条写——否则日志本身就是新的卡顿源。
                    s_suppressed++;
                    if (s_suppressed % 50 == 0)
                        CoopLog.Warn($"[卡顿监视] 已有 {s_stallCount} 次卡顿，后续只计数（累计 {s_suppressed} 条未记录）");

                    return;
                }

                CoopLog.Warn($"[卡顿] 单帧 {dt:0.000} 秒（第 {s_stallCount} 次，场景={s_sceneName}，累计帧数={s_frames}）");
            }
            catch
            {
                // 监视器自己绝不能成为问题。
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                float cost = Time.unscaledTime - s_sceneStartedAt;
                CoopLog.Info($"[场景] 加载完成 scene={scene.name} mode={mode} " +
                             $"距上次边界 {cost:0.000} 秒；本段最差帧 {s_worstFrame:0.000} 秒、帧数 {s_frames}");

                s_sceneName = scene.name;
                s_sceneStartedAt = Time.unscaledTime;
                s_worstFrame = 0f;
                s_frames = 0;
            }
            catch
            {
                // 同上
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            try
            {
                CoopLog.Info($"[场景] 开始卸载 scene={scene.name} " +
                             $"（停留 {Time.unscaledTime - s_sceneStartedAt:0.000} 秒）");
                s_sceneStartedAt = Time.unscaledTime;
            }
            catch
            {
                // 同上
            }
        }
    }
}
