using System.Collections.Generic;
using System.Text;
using UnityEngine;
using EliteEnemies.Settings;

namespace EliteEnemies.Effects
{
    /// <summary>
    /// 精英光环组件
    /// 显示脚底词缀文本（光环圆圈已禁用）
    /// </summary>
    public class EliteAuraPoolable : MonoBehaviour
    {
        // ========== 常量配置 ==========
        private const bool EnableAuraRing = false;
        
        private const int RingSegments = 48;
        private const int LayerCount = 1;
        private const float MinRadius = 0.6f;
        private const float MaxRadius = 0.6f;
        private const float BaseWidth = 0.18f;
        private const float BreathSpeed = 1.5f;
        private const float BreathScale = 0.10f;
        private const float HeightOffset = 0.03f;
        private const float VisibleDistance = 30f;
        private const float UpdateInterval = 0.5f;
        
        private const float TextHeightOffset = -0.40f;
        private const float TextCharacterSize = 0.065f;
        private const int TextFontSize = 35;

        // ========== 公共字段 ==========
        public LineRenderer[] rings;

        // ========== 私有字段 ==========
        private readonly List<TextMesh> _affixTexts = new List<TextMesh>();
        private CharacterMainControl _target;
        private Color _baseColor;
        private float _timer;
        private bool _isActive;
        private float _distanceCheckTimer;
        private bool _isVisible;
        private Renderer[] _targetRenderers;

        // ========== 对象池接口 ==========

        public void OnCreated()
        {
            if (EnableAuraRing)
            {
                if (rings == null || rings.Length == 0)
                    CreateRings();
            }
            else
            {
                rings = new LineRenderer[0];
            }
        }

        public void NotifyPooled()
        {
            _isActive = false;
            _target = null;
            _targetRenderers = null;
            _isVisible = false;

            gameObject.SetActive(false);

            if (rings != null)
            {
                foreach (LineRenderer ring in rings)
                {
                    if (ring != null) ring.enabled = false;
                }
            }

            ClearAffixTexts();
        }

        public void NotifyReleased()
        {
            _isActive = true;
            _timer = 0f;
            _distanceCheckTimer = 0f;
            _isVisible = false;

            gameObject.SetActive(true);
        }

        public void OnDestroying()
        {
            _target = null;
            ClearAffixTexts();
        }

        public bool IsTargetValid()
        {
            if (_target == null) return false;
            if (_target.Health == null) return false;
            if (_target.Health.IsDead) return false;
            return true;
        }

        // ========== 公共接口 ==========

        public void Initialize(CharacterMainControl target, Color color)
        {
            _target = target;
            _baseColor = color;
            _timer = 0f;
            _distanceCheckTimer = 0f;

            _targetRenderers = target.GetComponentsInChildren<Renderer>();

            if (EnableAuraRing)
            {
                if (rings == null || rings.Length == 0)
                    CreateRings();
                ApplyColor(color);
            }

            UpdateVisibility();
        }

        public void SetAffixNames(IList<string> affixKeys)
        {
            if (!GameConfig.ShowAffixFootText)
            {
                return;
            }

            ClearAffixTexts();

            if (affixKeys == null || affixKeys.Count == 0)
                return;

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < affixKeys.Count; i++)
            {
                string key = affixKeys[i];
                if (string.IsNullOrEmpty(key)) continue;

                if (EliteAffixes.TryGetAffix(key, out var affixData) && affixData != null)
                {
                    sb.Append(affixData.ColoredTag);
                }
                else
                {
                    sb.Append($"[{key}]");
                }
            }

            if (sb.Length == 0)
                return;

            string fullText = sb.ToString();
            Font builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject textObj = new GameObject("AffixText");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = new Vector3(0f, TextHeightOffset, 0f);

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = fullText;
            
            float scaledFontSize = GameConfig.AffixFootTextFontSize;
            textMesh.fontSize = Mathf.RoundToInt(scaledFontSize);
            textMesh.characterSize = TextCharacterSize * (scaledFontSize / 35f);
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.richText = true;

            if (builtinFont != null)
            {
                textMesh.font = builtinFont;
                MeshRenderer mr = textMesh.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = builtinFont.material;
                }
            }

            textMesh.color = Color.white;

            _affixTexts.Add(textMesh);
            UpdateTextVisibility(_isVisible);
        }

        // ========== 内部方法 ==========

        private void ClearAffixTexts()
        {
            if (_affixTexts == null || _affixTexts.Count == 0) return;

            for (int i = 0; i < _affixTexts.Count; i++)
            {
                TextMesh tm = _affixTexts[i];
                if (tm != null)
                {
                    Destroy(tm.gameObject);
                }
            }

            _affixTexts.Clear();
        }

        private void UpdateTextVisibility(bool visible)
        {
            if (_affixTexts == null) return;

            foreach (TextMesh tm in _affixTexts)
            {
                if (tm != null)
                {
                    tm.gameObject.SetActive(visible);
                }
            }
        }

        private void CreateRings()
        {
            if (!EnableAuraRing)
            {
                rings = new LineRenderer[0];
                return;
            }

            rings = new LineRenderer[LayerCount];

            for (int i = 0; i < LayerCount; i++)
            {
                GameObject ringObj = new GameObject($"Ring_{i}");
                ringObj.transform.SetParent(transform, false);

                LineRenderer lr = ringObj.AddComponent<LineRenderer>();
                SetupLineRenderer(lr, MaxRadius);

                rings[i] = lr;
            }
        }

        private void SetupLineRenderer(LineRenderer lr, float radius)
        {
            lr.startWidth = BaseWidth;
            lr.endWidth = BaseWidth;
            lr.positionCount = RingSegments + 1;
            lr.useWorldSpace = false;
            lr.loop = true;

            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.allowOcclusionWhenDynamic = false;

            GenerateCircleVertices(lr, radius);
        }

        private void GenerateCircleVertices(LineRenderer lr, float radius)
        {
            float angleStep = 360f / RingSegments;

            for (int i = 0; i <= RingSegments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                lr.SetPosition(i, new Vector3(x, HeightOffset, z));
            }
        }

        private void ApplyColor(Color color)
        {
            _baseColor = color;

            if (!EnableAuraRing || rings == null) return;

            for (int i = 0; i < rings.Length; i++)
            {
                LineRenderer lr = rings[i];
                if (lr == null) continue;

                lr.startColor = color;
                lr.endColor = color;
            }
        }

        private void UpdateVisibility()
        {
            if (!GameConfig.ShowAffixFootText)
            {
                UpdateTextVisibility(false);
                return;
            }

            if (_target == null)
            {
                SetVisible(false);
                return;
            }

            CharacterMainControl player = CharacterMainControl.Main;
            if (player == null)
            {
                SetVisible(true);
                return;
            }

            float sqrDistance = (player.transform.position - _target.transform.position).sqrMagnitude;
            bool inRange = sqrDistance <= (VisibleDistance * VisibleDistance);

            if (!inRange)
            {
                SetVisible(false);
                return;
            }

            bool inView = IsTargetInView();
            bool shouldBeVisible = inRange && inView;

            if (shouldBeVisible != _isVisible)
            {
                SetVisible(shouldBeVisible);
            }
        }

        private bool IsTargetInView()
        {
            if (_targetRenderers == null || _targetRenderers.Length == 0)
                return true;

            foreach (Renderer renderer in _targetRenderers)
            {
                if (renderer != null && renderer.isVisible)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;

            if (EnableAuraRing && rings != null)
            {
                foreach (LineRenderer ring in rings)
                {
                    if (ring != null) ring.enabled = visible;
                }
            }

            UpdateTextVisibility(visible);
        }

        // ========== Unity 生命周期 ==========

        private void Update()
        {
            if (!_isActive || _target == null) return;

            _distanceCheckTimer += Time.deltaTime;
            if (_distanceCheckTimer >= UpdateInterval)
            {
                _distanceCheckTimer = 0f;
                UpdateVisibility();
            }

            if (!_isVisible) return;

            transform.position = _target.transform.position;

            if (EnableAuraRing && rings != null && rings.Length > 0)
            {
                _timer += Time.deltaTime;
                float breath = 1f + Mathf.Sin(_timer * BreathSpeed) * BreathScale;

                foreach (LineRenderer ring in rings)
                {
                    if (ring != null)
                    {
                        ring.startWidth = BaseWidth * breath;
                        ring.endWidth = BaseWidth * breath;
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (!_isActive || _target == null || !_isVisible) return;

            Camera cam = Camera.main;
            if (cam == null || _affixTexts == null || _affixTexts.Count == 0) return;

            Quaternion lookRot = Quaternion.LookRotation(cam.transform.forward, Vector3.up);

            foreach (TextMesh tm in _affixTexts)
            {
                if (tm != null)
                {
                    tm.transform.rotation = lookRot;
                }
            }
        }
    }
}