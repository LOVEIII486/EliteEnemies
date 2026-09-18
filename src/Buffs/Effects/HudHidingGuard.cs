using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EliteEnemies.Buffs.Effects
{
    /// <summary>
    /// HUD「隐藏期」的**全局守卫**：所有 EMP 共用**一份完整快照**与**一个**还原流程。
    ///
    /// <para><b>为什么状态不能留在 Buff 实例上</b>：HUD 是**全局资源**（`LevelManager` 下只有一个
    /// <c>HUDCanvas</c>），而 Buff 实例是**每次施加各一份**。状态按实例存，就会出现两个实例
    /// 各持一份互不知情的"视图"：</para>
    /// <list type="bullet">
    /// <item>第二发 EMP 落在第一发的**还原途中**时，它抓到的是"此刻可见的那几个元素"——
    /// 而此刻只还原了一半 ⇒ **它只关掉半个 HUD，另外半个照常亮着**（效果的覆盖面被悄悄缩小）；</item>
    /// <item>第一发的还原协程**仍在跑**，于是"还原在开、闪烁在关"两个协程并发写同一批对象，
    /// 没有任何协调 ⇒ 玩家看到的是**恢复了一半的 HUD**，持续到第二发结束。</item>
    /// </list>
    /// <para>（`Buff模块审查与设计.md` 里"快照与协程改为每实例"那条修的是**两个玩家共用一份快照**——
    /// 对"玩家身份"是对的；但对全局资源，per-instance 恰恰是 split-brain 的来源。）</para>
    ///
    /// <para><b>关键一条：快照要活到还原**跑完**为止，而不是活到最后一个持有者离开为止。</b>
    /// 还原途中 <c>_holders</c> 已经是 0，若此时把快照丢掉，随后到来的 EMP 就只能抓到
    /// 残缺的可见集——正是上面第一个缺陷。所以 <see cref="RestoreSequence"/> 是**全部打开之后**
    /// 才清快照。</para>
    ///
    /// <para><b>协程宿主是 <see cref="LevelManager"/>，不是角色</b>——这里操作的只有场景 UI，
    /// 而 HUD 是 <c>LevelManager</c> 的子物体、可能比角色活得久。取角色做宿主会让还原随角色
    /// 一起被杀。宿主随关卡销毁时 HUD 也一起没了，所以不存在"留下没还原的状态"。</para>
    /// </summary>
    internal static class HudHidingGuard
    {
        private const string LogTag = "[EliteEnemies.EmpHud]";

        /// <summary>HUD 画布在关卡里的物体名。**这是场景里的名字，源码中查不到**，见 <see cref="CaptureSnapshot"/>。</summary>
        private const string HudCanvasName = "HUDCanvas";

        private const float GlitchDuration = 1f;

        /// <summary>HUD 的**完整**元素集。只在"隐藏期开始前"捕获，一直保留到还原跑完。</summary>
        private static readonly List<GameObject> Snapshot = new List<GameObject>();

        /// <summary>当前有多少发 EMP 处于隐藏期。归零才启动还原。</summary>
        private static int _holders;

        /// <summary>当前在跑的序列（闪烁或还原）。**同时只允许一个**，否则两者会互相打架。</summary>
        private static Coroutine _sequence;

        /// <summary>进入隐藏期。每发 EMP 调一次，**必须与 <see cref="Release"/> 成对**。</summary>
        internal static void Hold()
        {
            PruneSnapshot();

            // 快照为空有两种含义：① 第一次隐藏；② 换了关卡（上一局的元素全被销毁）。
            // 后者要顺带把持有者计数归零——上一局若没来得及 Release（角色被直接销毁时
            // 游戏不会发 onRemoveBuff），计数会永久卡在"隐藏中"，新关卡再也抓不到快照。
            if (Snapshot.Count == 0)
            {
                _holders = 0;
                CaptureSnapshot();
            }

            _holders++;

            // 无论快照是新抓的还是沿用的，都关一遍：沿用的情况下，上一发的还原可能已经
            // 打开了一部分，这一步把它们关回去。
            SetAllActive(false);

            StartSequence(GlitchSequence());
        }

        /// <summary>离开隐藏期。只有**最后一个**持有者离开时才启动还原。</summary>
        internal static void Release()
        {
            if (_holders > 0) _holders--;

            if (_holders > 0) return;

            StartSequence(RestoreSequence());
        }

        // ═══════════════ 内部 ═══════════════

        /// <summary>换序列：先停掉在跑的那个（消除"还原与闪烁并发"），再起新的。</summary>
        private static void StartSequence(IEnumerator routine)
        {
            var host = LevelManager.Instance;
            if (host == null) return;

            StopSequence();
            _sequence = host.StartCoroutine(routine);
        }

        /// <summary>
        /// 停掉在跑的序列。换了关卡时旧协程已随旧宿主销毁，这里只是把句柄清掉——
        /// 所以不需要记住"当初是谁起的"。
        /// </summary>
        private static void StopSequence()
        {
            if (_sequence == null) return;

            var host = LevelManager.Instance;
            if (host != null) host.StopCoroutine(_sequence);

            _sequence = null;
        }

        private static void PruneSnapshot() => Snapshot.RemoveAll(x => x == null);

        private static void SetAllActive(bool active)
        {
            foreach (var obj in Snapshot)
            {
                if (obj != null) obj.SetActive(active);
            }
        }

        /// <summary>
        /// 捕获 HUD 当前亮着的 UI 元素。**只在隐藏期开始前调**——那才是"完整"的时刻。
        ///
        /// <para>⚠ <c>"HUDCanvas"</c> 是**场景里的物体名，游戏源码中一处都没有**（全库搜过：
        /// 既无该字符串，<c>LevelManager</c> 也没有 HUD/Canvas 字段，游戏的 HUD 是一堆挂在
        /// 场景物体上的 <c>xxxHUD</c> 组件）。所以这个名字**无法从源码核实**，只能靠实机。
        /// 因此这里**刻意不静默**：找不到就报出来，否则整个词条会变成空转且毫无线索。</para>
        /// </summary>
        private static void CaptureSnapshot()
        {
            try
            {
                Snapshot.Clear();

                var levelManager = LevelManager.Instance;
                if (levelManager == null) return;

                Transform hudCanvas = null;
                foreach (Transform child in levelManager.transform)
                {
                    if (child.name == HudCanvasName)
                    {
                        hudCanvas = child;
                        break;
                    }
                }

                if (hudCanvas == null)
                {
                    Debug.LogError($"{LogTag} 在 LevelManager 下找不到名为 '{HudCanvasName}' 的子物体" +
                                   "——**本词条的效果不会发生**（HUD 不会被关掉）。" +
                                   "该名字来自场景而非源码，游戏改过 HUD 层级就会出现这种情况；" +
                                   "请对照实机层级修正本类的 HudCanvasName。");
                    return;
                }

                foreach (Transform child in hudCanvas)
                {
                    // 排除瞄准标记
                    if (child.name != "AimMarker" && child.gameObject.activeSelf)
                    {
                        Snapshot.Add(child.gameObject);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogTag} 建立 HUD 快照失败: {ex}");
            }
        }

        /// <summary>故障闪烁。结束时**全部关闭**（隐藏期的稳态），还原另由 <see cref="RestoreSequence"/> 负责。</summary>
        private static IEnumerator GlitchSequence()
        {
            if (Snapshot.Count == 0) yield break;

            float timer = 0f;
            while (timer < GlitchDuration)
            {
                int count = Random.Range(1, 3);
                var tempShown = new List<GameObject>();

                for (int i = 0; i < count; i++)
                {
                    if (Snapshot.Count == 0) break;
                    var obj = Snapshot[Random.Range(0, Snapshot.Count)];
                    if (obj != null)
                    {
                        obj.SetActive(true);
                        tempShown.Add(obj);
                    }
                }

                yield return new WaitForSeconds(Random.Range(0.05f, 0.1f));

                foreach (var obj in tempShown)
                {
                    if (obj != null) obj.SetActive(false);
                }

                float waitTime = Random.Range(0.05f, 0.2f);
                timer += waitTime;
                yield return new WaitForSeconds(waitTime);
            }

            SetAllActive(false);

            _sequence = null;
        }

        /// <summary>
        /// 有序重启：逐个打开，**全部打开之后才清空快照**。
        /// 提前清空会让"还原途中又中 EMP"只能抓到残缺的可见集，见类注释。
        /// </summary>
        private static IEnumerator RestoreSequence()
        {
            if (Snapshot.Count == 0) yield break;

            foreach (var obj in Snapshot)
            {
                if (obj != null) obj.SetActive(true);
                yield return new WaitForSeconds(0.1f);
            }

            Snapshot.Clear();
            _sequence = null;
        }
    }
}
