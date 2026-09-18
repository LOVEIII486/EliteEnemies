using UnityEngine;

namespace EliteEnemies.Visuals
{
    /// <summary>
    /// 精英怪发光效果控制器
    /// </summary>
    public class EliteGlowController
    {
        // ⚠ 不是 readonly：模型被换过之后要能重取，见 RefreshRenderers。
        private Renderer[] _renderers;

        private readonly MaterialPropertyBlock _propBlock;
        private readonly int _emissionColorId;
        private bool _isValid;

        private readonly CharacterMainControl _character;

        /// <summary>重取渲染器的冷却（秒）。**没有它会变成反向优化**，见 <see cref="InternalSetColor"/>。</summary>
        private const float RescanCooldown = 1f;
        private float _nextRescanTime;

        // 闪烁状态变量
        private bool _isFlashing = false;
        private float _currentIntensity = 0f;
        private float _decaySpeed = 0f;
        private Color _flashColor = Color.white;

        public EliteGlowController(CharacterMainControl character)
        {
            if (character == null) return;

            _character = character;
            _propBlock = new MaterialPropertyBlock();
            _emissionColorId = Shader.PropertyToID("_EmissionColor");
            RefreshRenderers();
        }

        /// <summary>
        /// 取一次该角色的全部渲染器（含未激活的，以防后续激活）。
        ///
        /// <para>⚠ <b>渲染器集合不是一成不变的</b>：装了第三方模型模组（如 DuckovCustomModel）时，
        /// 角色模型会在**运行期被替换** ⇒ 构造时取到的那批引用全部变成 Unity 假空，
        /// 而写入循环里的 <c>renderer != null</c> 会逐个跳过它们 ⇒ **闪烁静默消失**
        /// （本工程最忌讳的那类失效：不报错，只是不生效）。
        /// 所以 <see cref="InternalSetColor"/> 发现"一个都没写上"时会重取一次。</para>
        /// </summary>
        private void RefreshRenderers()
        {
            _renderers = _character != null
                ? _character.GetComponentsInChildren<Renderer>(true)
                : null;

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
            if (WriteToRenderers(color)) return;

            // 一个都没写上 ⇒ 多半是模型被换过、旧引用全成了假空。重取一次再写。
            //
            // ⚠ **必须限频**：若角色确实暂时没有渲染器（还没构建完之类），不限频就会退化成
            // **每帧一次 `GetComponentsInChildren`**——那是**反向优化**。冷却期内直接放弃。
            if (Time.time < _nextRescanTime) return;
            _nextRescanTime = Time.time + RescanCooldown;

            RefreshRenderers();
            WriteToRenderers(color);
        }

        /// <summary>把颜色写进全部渲染器。返回值：是否**至少写中了一个**。</summary>
        private bool WriteToRenderers(Color color)
        {
            if (_renderers == null) return false;

            bool any = false;
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;   // 已销毁的渲染器在这里被跳过

                renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(_emissionColorId, color);
                renderer.SetPropertyBlock(_propBlock);
                any = true;
            }
            return any;
        }

        public void Reset()
        {
            InternalSetColor(Color.black);
            _isFlashing = false;
        }
    }
}