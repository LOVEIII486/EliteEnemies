using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Duckov.Buffs;
using EliteEnemies.Localization;
using Random = UnityEngine.Random;

namespace EliteEnemies.EliteEnemy.BuffsSystem.Effects
{
    /// <summary>
    /// 电磁干扰效果：禁用 HUD
    /// </summary>
    public class EMPEffect : IEliteBuffEffect
    {
        private const string LogTag = "[EliteEnemies.EMPEffect]";
        public string BuffName => "EliteBuff_EMP";

        // 快照 只记录第一次命中时亮着的 UI
        private List<GameObject> _uiSnapshot = new List<GameObject>();
        
        private Coroutine _activeCoroutine;

        private readonly Lazy<string> _hideHUDTextFmt = new(() =>
            LocalizationManager.GetText("Affix_EMP_PopText_1")
        );
        private readonly Lazy<string> _restoreHUDTextFmt = new(() =>
            LocalizationManager.GetText("Affix_EMP_PopText_2")
        );

        public void OnBuffSetup(Buff buff, CharacterMainControl player)
        {
            if (player == null) return;

            if (_activeCoroutine != null)
            {
                player.StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }

            // 维护快照名单
            _uiSnapshot.RemoveAll(x => x == null);

            if (_uiSnapshot.Count == 0)
            {
                CaptureSnapshot();
            }

            foreach (var obj in _uiSnapshot)
            {
                if (obj != null) obj.SetActive(false);
            }

            player.PopText(_hideHUDTextFmt.Value);
            _activeCoroutine = player.StartCoroutine(GlitchSequence(player));
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl player)
        {
            if (player == null) return;
            if (_activeCoroutine != null) player.StopCoroutine(_activeCoroutine);

            player.PopText(_restoreHUDTextFmt.Value);

            // 有序重启
            _activeCoroutine = player.StartCoroutine(RebootSequence(player));
        }

        /// <summary>
        /// 捕获快照
        /// </summary>
        private void CaptureSnapshot()
        {
            try
            {
                _uiSnapshot.Clear();
                var levelManager = LevelManager.Instance;
                if (levelManager == null) return;

                // 查找 HUDCanvas
                Transform hudCanvas = null;
                foreach (Transform child in levelManager.transform)
                {
                    if (child.name == "HUDCanvas")
                    {
                        hudCanvas = child;
                        break;
                    }
                }

                if (hudCanvas == null) return;
 
                // 记录当前所有开启状态的 UI
                foreach (Transform child in hudCanvas)
                {
                    // 排除 AimMarker
                    if (child.name != "AimMarker" && child.gameObject.activeSelf)
                    {
                        _uiSnapshot.Add(child.gameObject);
                    }
                }
                
                // Debug.Log($"{LogTag} 快照已建立，包含 {_uiSnapshot.Count} 个 UI 元素");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 建立 HUD 快照失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 故障闪烁
        /// </summary>
        private IEnumerator GlitchSequence(CharacterMainControl player)
        {
            if (_uiSnapshot.Count == 0) yield break;

            float duration = 1.0f; // 闪烁阶段持续 1 秒
            float timer = 0f;

            while (timer < duration)
            {
                if (player == null) yield break;
                
                int count = Random.Range(1, 3);
                List<GameObject> tempShown = new List<GameObject>();

                for (int i = 0; i < count; i++)
                {
                    if (_uiSnapshot.Count == 0) break;
                    var obj = _uiSnapshot[Random.Range(0, _uiSnapshot.Count)];
                    
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

            // 闪烁结束时全部关闭
            foreach (var obj in _uiSnapshot)
            {
                if (obj != null) obj.SetActive(false);
            }
            
            _activeCoroutine = null;
        }

        /// <summary>
        /// 系统重启
        /// </summary>
        private IEnumerator RebootSequence(CharacterMainControl player)
        {
            if (_uiSnapshot.Count == 0) yield break;

            foreach (var obj in _uiSnapshot)
            {
                if (player == null) yield break;
                if (obj != null)
                {
                    obj.SetActive(true);
                }

                yield return new WaitForSeconds(0.1f);
            }
            
            // 只有在完全恢复后，才销毁快照
            _uiSnapshot.Clear();
            _activeCoroutine = null;
        }
    }
}