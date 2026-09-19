using System;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 低频重压客机上"该藏着"的复制体（每 0.5 秒一次）。
    ///
    /// <para><b>为什么需要它</b>：主机侧的隐身在 <c>MimicBehavior</c> 里是**每帧**压制的
    /// （关渲染器那一步的注释写着"模型可能被别的系统重新启用，所以要持续压制"）。
    /// 而客机只从我们这里收到**一条**"藏起来"的消息——一旦被同一个机制翻回来，
    /// 就再也回不去了（<c>Hide()</c> 是边沿触发，没法用它再压一次）。
    /// 实测症状：<b>拟态敌人在客机一直显形</b>。</para>
    ///
    /// <para>这里不是"发明一个机制跟游戏对着干"，而是<b>让客机跟上主机本来就有的节奏</b>——
    /// 主机每帧压、客机 2Hz 压，量级差得不远，而每次成本只是"对一个角色调一次 Hide + 遍历它自己的渲染器列表"。</para>
    ///
    /// <para>⚠️ 没有隐藏中的目标时它什么都不做（<see cref="CoopEliteSync.ReassertHiddenVisuals"/>
    /// 头一行就早退），所以<b>只在真的有隐身怪时才有一点开销</b>。</para>
    ///
    /// <para>与 <see cref="CoopStallWatch"/> 同样是"只在联机 API 激活后才创建"的组件，
    /// 单机玩家连这个 GameObject 都不会有。</para>
    /// </summary>
    internal sealed class CoopVisualWatch : MonoBehaviour
    {
        /// <summary>重压间隔。取 0.5 秒的理由：与"血条 UI 重查窗口"同一个量级，
        /// 肉眼不会看出延迟，而每秒只有两次。</summary>
        private const float Interval = 0.5f;

        private static CoopVisualWatch s_instance;

        private float _nextAt;

        public static void Install()
        {
            if (s_instance != null) return;

            try
            {
                var go = new GameObject("[EliteEnemies.Coop.VisualWatch]")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<CoopVisualWatch>();
            }
            catch (Exception ex)
            {
                // 起不来只是"隐身可能又被翻回去"，不该拦住联机初始化。
                CoopLog.Warn($"[视觉重压] 启动失败，客机的隐身/显隐将只在收到消息时套用一次：{ex.Message}");
            }
        }

        public static void Uninstall()
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
                CoopEliteSync.ReassertHiddenVisuals();
            }
            catch (Exception ex)
            {
                // 这里是个每 0.5 秒跑一次的循环，异常不兜住会变成每帧刷屏。
                CoopLog.Warn($"[视觉重压] 执行失败（本次跳过）：{ex.Message}");
            }
        }
    }
}
