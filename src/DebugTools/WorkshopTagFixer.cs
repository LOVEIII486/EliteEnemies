using System;
using System.Collections;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace EliteEnemies.DebugTools
{
    /// <summary>
    /// 把创意工坊条目的**分类标签**重新写回去（F8）。
    ///
    /// <para><b>为什么需要它</b>：游戏的上传器有一个 bug——<c>SteamWorkshopManager.cs:225</c>
    /// 在按 <c>info.ini</c> 的 <c>tags</c> 设过一次之后，**紧接着又无条件地**
    /// <c>SteamUGC.SetItemTags(handle, ["Mod"])</c> 一次；而 Steam 的 <c>SetItemTags</c>
    /// 是**覆盖**语义，同一个 handle 上后写的赢。于是无论 <c>info.ini</c> 里填什么，
    /// 上传完标签都只剩 <c>Mod</c>，分类标签全丢——模组就不再属于任何类别。
    /// 工坊网页端又**没有**编辑标签的入口，两条常规途径都堵着。</para>
    ///
    /// <para><b>为什么这个工具能成</b>：它跑在游戏进程里，Steam API 已由游戏初始化、
    /// 登录身份就是条目创建者，所以能直接对条目调 UGC 接口。</para>
    ///
    /// <para>⚠ <b>每次发版之后都要按一次</b>——上传会把标签覆盖回 <c>["Mod"]</c>。
    /// 这不是一次性的活。</para>
    ///
    /// <para>标签的可用取值见工坊浏览页的筛选栏（appid 3167020，共 14 个）；
    /// 改完可以用 <c>GetPublishedFileDetails</c> 查 <c>tags</c> 字段确认。</para>
    /// </summary>
    public class WorkshopTagFixer : MonoBehaviour
    {
        private const string LogTag = "[EliteEnemies.WorkshopTagFixer]";

        /// <summary>游戏自己的 AppID（<c>SteamWorkshopManager.cs:204</c> 里也是硬编码这个值）。</summary>
        private const uint GameAppId = 3167020;

        /// <summary>本模组的工坊条目 ID，与 <c>info.ini</c> 的 <c>publishedFileId</c> 一致。</summary>
        private const ulong ItemId = 3602009885UL;

        /// <summary>提交时显示的改动说明（Steam 会记进条目的更新历史）。</summary>
        private const string ChangeNote = "修正工坊分类标签";

        /// <summary>
        /// 要写入的标签。**必须取自工坊开放的那 14 个**——写别的上去 Steam 会照收，
        /// 但筛选栏里点不到，等于没设。
        /// </summary>
        private static readonly string[] Tags =
        {
            "Gameplay",
            "Companion & NPC",
            "Loot & Economy",
        };

        /// <summary>触发键。F9 / F10 已被 PresetKeyLogger 与 LootCacheDumper 占用。</summary>
        public KeyCode fixKey = KeyCode.F8;

        private bool _busy;

        private void Update()
        {
            if (!_busy && Input.GetKeyDown(fixKey)) StartCoroutine(FixRoutine());
        }

        /// <summary>走一次 设置标签 → 提交 → 等 Steam 回调 的完整流程，并把结果打出来。</summary>
        private IEnumerator FixRoutine()
        {
            _busy = true;
            try
            {
                UGCUpdateHandle_t handle = SteamUGC.StartItemUpdate(
                    new AppId_t(GameAppId), new PublishedFileId_t(ItemId));

                SteamUGC.SetItemTags(handle, new List<string>(Tags));

                // 游戏自己的写法（SteamWorkshopManager.cs:236）：CallResult 接回调，
                // 每帧等它。**不要用 using 提前 Dispose**——回调还没回来就释放会丢结果。
                bool done = false;
                SubmitItemUpdateResult_t result = default;
                CallResult<SubmitItemUpdateResult_t> handler = CallResult<SubmitItemUpdateResult_t>.Create(
                    (SubmitItemUpdateResult_t r, bool failure) =>
                    {
                        result = r;
                        done = true;
                    });

                SteamAPICall_t call = SteamUGC.SubmitItemUpdate(handle, ChangeNote);
                handler.Set(call);

                Debug.Log($"{LogTag} 已提交标签 [{string.Join(", ", Tags)}]，等待 Steam 回调…");

                float timeout = 20f;
                while (!done && timeout > 0f)
                {
                    timeout -= Time.unscaledDeltaTime;
                    yield return null;
                }

                handler.Dispose();

                if (!done)
                {
                    Debug.LogError($"{LogTag} 等超时了（20 秒）——Steam 没回结果，标签**可能没设上**");
                    yield break;
                }

                if (result.m_eResult != EResult.k_EResultOK)
                {
                    Debug.LogError($"{LogTag} 设置失败：{result.m_eResult}");
                    yield break;
                }

                Debug.Log($"{LogTag} ✅ 标签已写入：{string.Join(" / ", Tags)}");

                if (result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
                {
                    Debug.LogWarning($"{LogTag} Steam 要求先接受创意工坊法律协议，正在打开页面");
                    SteamFriends.ActivateGameOverlayToWebPage($"steam://url/CommunityFilePage/{ItemId}");
                }
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
