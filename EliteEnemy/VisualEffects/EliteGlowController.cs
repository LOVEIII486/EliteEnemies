using UnityEngine;

namespace EliteEnemies.EliteEnemy.VisualEffects
{
    /// <summary>
    /// 精英怪发光效果控制器
    /// 封装了 MaterialPropertyBlock 的底层操作，用于统一管理模型发光
    /// </summary>
    public class EliteGlowController
    {
        private readonly Renderer[] _renderers;
        private readonly MaterialPropertyBlock _propBlock;
        private readonly int _emissionColorId;
        private bool _isValid;

        public EliteGlowController(CharacterMainControl character)
        {
            if (character == null) return;
            

            _renderers = character.GetComponentsInChildren<Renderer>(true);
            _propBlock = new MaterialPropertyBlock();
            _emissionColorId = Shader.PropertyToID("_EmissionColor");
            _isValid = _renderers != null && _renderers.Length > 0;
        }

        public void SetEmissionColor(Color color)
        {
            if (!_isValid) return;

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

        /// <summary>
        /// 重置发光
        /// </summary>
        public void Reset()
        {
            if (!_isValid) return;
            _propBlock.Clear(); 

            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(_propBlock);
                }
            }
        }
    }
}