using UnityEngine;

namespace EliteEnemies.EliteEnemy.VisualEffects
{
    /// <summary>
    /// 精英怪发光效果控制器
    /// </summary>
    public class EliteGlowController
    {
        private readonly Renderer[] _renderers;
        private readonly MaterialPropertyBlock _propBlock;
        private readonly int _emissionColorId;
        private bool _isValid;

        // 闪烁状态变量
        private bool _isFlashing = false;
        private float _currentIntensity = 0f;
        private float _decaySpeed = 0f;
        private Color _flashColor = Color.white;

        public EliteGlowController(CharacterMainControl character)
        {
            if (character == null) return;
            
            // 获取渲染器 (包括未激活的，以防后续激活)
            _renderers = character.GetComponentsInChildren<Renderer>(true);
            _propBlock = new MaterialPropertyBlock();
            _emissionColorId = Shader.PropertyToID("_EmissionColor");
            _isValid = _renderers != null && _renderers.Length > 0;
        }

        /// <summary>
        /// 触发一次性的闪烁效果（自动随时间衰减）
        /// 需要在 Update 中调用 OnUpdate 方法
        /// </summary>
        /// <param name="color">闪烁颜色</param>
        /// <param name="duration">持续时间（秒）</param>
        /// <param name="intensity">初始亮度倍率（默认 2.0）</param>
        public void TriggerFlash(Color color, float duration, float intensity = 2.0f)
        {
            if (!_isValid) return;

            _flashColor = color;
            _currentIntensity = intensity;
            // 计算衰减速度：强度 / 时间 = 每秒减少多少
            _decaySpeed = (duration > 0) ? (intensity / duration) : 0f;
            _isFlashing = true;

            ApplyColor();
        }

        /// <summary>
        /// 每帧更新闪烁状态（必须在词缀的 OnUpdate 中调用）
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_isFlashing) return;

            _currentIntensity -= _decaySpeed * deltaTime;

            if (_currentIntensity <= 0f)
            {
                _currentIntensity = 0f;
                _isFlashing = false;
                Reset(); // 结束时重置
            }
            else
            {
                ApplyColor();
            }
        }

        // 手动设置颜色的接口
        public void SetEmissionColor(Color color)
        {
            if (!_isValid) return;
            
            // 如果手动设置颜色，打断当前的闪烁逻辑
            _isFlashing = false; 
            
            // 设置颜色逻辑
            InternalSetColor(color);
        }

        // 内部应用颜色逻辑
        private void ApplyColor()
        {
            InternalSetColor(_flashColor * _currentIntensity);
        }

        private void InternalSetColor(Color color)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(_propBlock);
                    _propBlock.SetColor(_emissionColorId, color);
                    renderer.SetPropertyBlock(_propBlock);
                }
            }
        }

        public void Reset()
        {
            InternalSetColor(Color.black);
            _isFlashing = false;
        }
    }
}