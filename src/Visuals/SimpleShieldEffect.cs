using UnityEngine;

namespace EliteEnemies.Visuals
{
    /// <summary>
    /// 球形护盾特效控制器
    /// </summary>
    public class SimpleShieldEffect
    {
        private GameObject _shieldObj;
        private Transform _targetRoot;
        private Material _shieldMat;

        private Color _baseColor;      // 基础颜色
        private float _currentAlpha;   // 实时Alpha
        private float _targetAlpha;    // 目标Alpha
        private float _sizeMultiplier;

        private const float FadeSpeed = 1.0f;

        public SimpleShieldEffect(Transform target, Color color, float sizeMultiplier = 1.1f)
        {
            _targetRoot = target;
            _baseColor = color;
            _sizeMultiplier = sizeMultiplier;
            _currentAlpha = 0f;
        }

        public void Show()
        {
            if (_shieldObj == null) CreateShieldObject();

            if (_shieldObj != null)
            {
                _shieldObj.SetActive(true);
                _targetAlpha = _baseColor.a;
            }
        }

        public void Hide()
        {
            _targetAlpha = 0f;
        }

        public void Update(float deltaTime)
        {
            if (_shieldObj == null || !_shieldObj.activeSelf) return;

            // 淡入淡出
            if (!Mathf.Approximately(_currentAlpha, _targetAlpha))
            {
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, FadeSpeed * deltaTime);

                if (_shieldMat != null)
                {
                    Color c = _baseColor;
                    c.a = _currentAlpha;
                    _shieldMat.color = c;
                }
                if (_currentAlpha <= 0.01f && _targetAlpha <= 0.01f)
                {
                    _shieldObj.SetActive(false);
                }
            }
        }

        public void Destroy()
        {
            if (_shieldObj != null)
            {
                UnityEngine.Object.Destroy(_shieldObj);
                _shieldObj = null;
            }

            // `_shieldMat` 是 `new Material(shader)` 造出来的**运行时材质**（见 CreateShieldObject），
            // 它**不归那个 GameObject 所有**——`Destroy(_shieldObj)` 不会连带销毁它，
            // 只能等 `Resources.UnloadUnusedAssets`（关卡结束时）回收。
            // 而护盾是**每次精英化各造一份**的 ⇒ 不在这里销毁就是每次都在漏一份。
            if (_shieldMat != null)
            {
                UnityEngine.Object.Destroy(_shieldMat);
                _shieldMat = null;
            }
        }

        private Vector3 _baseScale;

        private void CreateShieldObject()
        {
            if (_targetRoot == null) return;

            _shieldObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _shieldObj.name = "Elite_Visual_Shield";

            UnityEngine.Object.Destroy(_shieldObj.GetComponent<Collider>());

            _shieldObj.transform.SetParent(_targetRoot);
            _shieldObj.transform.localPosition = Vector3.zero;
            _shieldObj.transform.localRotation = Quaternion.identity;

            FitToCollider();

            var renderer = _shieldObj.GetComponent<Renderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

            // 缓存材质
            _shieldMat = new Material(shader);

            Color initColor = _baseColor;
            initColor.a = 0f;
            _shieldMat.color = initColor;
            _currentAlpha = 0f;

            renderer.material = _shieldMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void FitToCollider()
        {
            var capCol = _targetRoot.GetComponent<CapsuleCollider>();
            var charCtrl = _targetRoot.GetComponent<CharacterController>();

            float height = 2.0f;
            float radius = 0.5f;
            Vector3 center = Vector3.up * 1.0f;

            if (capCol != null)
            {
                height = capCol.height;
                radius = capCol.radius;
                center = capCol.center;
            }
            else if (charCtrl != null)
            {
                height = charCtrl.height;
                radius = charCtrl.radius;
                center = charCtrl.center;
            }

            float visualPadding = 0.25f;
            float targetHeight = height + (visualPadding * 2);
            float targetRadius = radius + visualPadding;

            float scaleY = targetHeight / 2.0f;
            float scaleXZ = targetRadius / 0.5f;

            scaleY *= _sizeMultiplier;
            scaleXZ *= _sizeMultiplier;

            _shieldObj.transform.localPosition = center;

            _baseScale = new Vector3(scaleXZ, scaleY, scaleXZ);
            _shieldObj.transform.localScale = _baseScale;
        }
    }
}
