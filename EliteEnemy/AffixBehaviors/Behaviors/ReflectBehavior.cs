using System.Collections.Generic;
using UnityEngine;
using EliteEnemies.EliteEnemy.VisualEffects;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    public class ReflectBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Reflect";

        // 静态注册表
        public static readonly HashSet<int> ActiveReflectors = new HashSet<int>();

        private CharacterMainControl _owner;
        private int _ownerID;
        private EliteGlowController _glowController;
        private float _timer;
        private bool _isReflecting;

        // --- 配置参数 ---
        private const float CooldownTime = 5.0f; 
        private const float ActiveDuration = 3.0f; // 持续3秒

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _owner = character;
            _ownerID = character.GetInstanceID();
            _glowController = new EliteGlowController(character);
            
            // 【修改1】让 timer 初始化为 CooldownTime，这样生成后第一帧就会触发反弹
            // 方便你进游戏立刻看到效果
            _timer = CooldownTime; 
            _isReflecting = false;
        }

        private void StartReflect()
        {
            if (_isReflecting) return;

            _isReflecting = true;
            _timer = 0f;
            
            ActiveReflectors.Add(_ownerID);
            
            // 开启金色闪光
            _glowController.TriggerFlash(new Color(1f, 0.84f, 0f), ActiveDuration);
            Debug.Log($"[Reflect] {_owner.name} 开启反弹！");
        }

        private void EndReflect()
        {
            if (!_isReflecting) return;

            _isReflecting = false;
            _timer = 0f;
            
            ActiveReflectors.Remove(_ownerID);
            Debug.Log($"[Reflect] {_owner.name} 反弹结束，进入冷却。");
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (_isReflecting) ActiveReflectors.Remove(_ownerID);
            _glowController?.Reset();
            _owner = null;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_owner == null || !_owner.gameObject.activeInHierarchy) return;

            _timer += deltaTime;

            // 【修改2】修正后的状态机逻辑
            if (_isReflecting)
            {
                // 持续期间
                if (_timer >= ActiveDuration)
                {
                    EndReflect();
                }
            }
            else
            {
                // 冷却期间
                if (_timer >= CooldownTime)
                {
                    StartReflect();
                }
            }
            
            // 更新特效
            _glowController?.Update(deltaTime);
        }
    }
}