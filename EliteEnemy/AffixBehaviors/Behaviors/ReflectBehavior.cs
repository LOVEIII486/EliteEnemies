using System;
using System.Collections.Generic;
using UnityEngine;
using EliteEnemies.EliteEnemy.VisualEffects;
using EliteEnemies.Localization;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    public class ReflectBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Reflect";

        public static readonly HashSet<int> ActiveReflectors = new HashSet<int>();

        private CharacterMainControl _owner;
        private int _ownerID;
        private EliteGlowController _glowController;
        private float _timer;
        private bool _isReflecting;

        private const float CooldownTime = 5.0f; 
        private const float ActiveDuration = 5.0f;
        
        private readonly Lazy<string> _popText = new(() => LocalizationManager.GetText("Affix_Reflect_PopText"));

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _owner = character;
            _ownerID = character.GetInstanceID();
            _glowController = new EliteGlowController(character);
            
            _timer = CooldownTime; 
            _isReflecting = false;
        }

        private void StartReflect()
        {
            if (_isReflecting) return;

            _isReflecting = true;
            _timer = 0f;
            
            ActiveReflectors.Add(_ownerID);
            
            _glowController.TriggerFlash(new Color(1f, 0.84f, 0f), ActiveDuration);
            _owner.PopText(_popText.Value);
            //Debug.Log($"[Reflect] {_owner.name} 开启反弹！");
        }

        private void EndReflect()
        {
            if (!_isReflecting) return;

            _isReflecting = false;
            _timer = 0f;
            
            ActiveReflectors.Remove(_ownerID);
            //Debug.Log($"[Reflect] {_owner.name} 反弹结束，进入冷却。");
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

            if (_isReflecting)
            {
                if (_timer >= ActiveDuration)
                {
                    EndReflect();
                }
            }
            else
            {
                if (_timer >= CooldownTime)
                {
                    StartReflect();
                }
            }
            
            _glowController?.Update(deltaTime);
        }
    }
}