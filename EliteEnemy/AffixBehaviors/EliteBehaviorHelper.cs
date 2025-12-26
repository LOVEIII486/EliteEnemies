using UnityEngine;
using Duckov;
using Duckov.Scenes;
using ItemStatsSystem;
using ItemStatsSystem.Items;

namespace EliteEnemies.EliteEnemy.AffixBehaviors
{
    /// <summary>
    /// 精英怪辅助工具类，封装通用的逻辑
    /// </summary>
    public static class EliteBehaviorHelper
    {
        private const string LogTag = "[EliteEnemies.EliteBehaviorHelper]";
        /// <summary>
        /// 让指定角色向目标位置投掷
        /// </summary>
        public static void LaunchGrenade(CharacterMainControl attacker, int itemId, Vector3 targetPos, float delay = 1.5f, bool canHurtSelf = false)
        {
            if (attacker == null) return;
            
            Item item = ItemAssetsCollection.InstantiateSync(itemId);
            if (item == null)
            {
                Debug.LogWarning($"{LogTag} 无效的物品 ID: {itemId}");
                return;
            }
            
            Skill_Grenade skill = item.GetComponent<Skill_Grenade>();
            if (skill == null)
            {
                Debug.LogWarning($"{LogTag} 物品 {item.DisplayName} (ID:{itemId}) 不包含 Skill_Grenade 组件");
                return;
            }
            
            skill.canHurtSelf = canHurtSelf;
            skill.delay = delay;
            
            // 指定落点
            SkillReleaseContext context = new SkillReleaseContext
            {
                releasePoint = targetPos
            };
            
            skill.ReleaseSkill(context, attacker);
        }

        /// <summary>
        /// 直接向玩家当前位置发射
        /// </summary>
        public static void LaunchGrenadeAtPlayer(CharacterMainControl attacker, int itemId, float delay = 1.5f)
        {
            if (LevelManager.Instance?.MainCharacter == null) return;
            Vector3 playerPos = LevelManager.Instance.MainCharacter.transform.position;
            LaunchGrenade(attacker, itemId, playerPos, delay);
        }
        
        
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
}