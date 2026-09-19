using System;
using System.Collections.Generic;
using UnityEngine;
using EliteEnemies.Visuals;
using EliteEnemies.Localization;

namespace EliteEnemies.Affixes.Behaviors
{
    public class ReflectBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Reflect";
        /// <summary>
        /// 正处于反射状态的角色实例 ID。**私有**：它被弹道补丁读取，但那是本模组的内部协议，
        /// 不该是公开可变字段（原先任何代码都能 Clear/Add 它）。读取方用 <see cref="IsReflecting"/>。
        /// </summary>
        private static readonly HashSet<int> ActiveReflectorIDs = new HashSet<int>();

        /// <summary>该角色实例当前是否处于反射状态（供弹道补丁查询）。</summary>
        public static bool IsReflecting(int characterInstanceID) => ActiveReflectorIDs.Contains(characterInstanceID);

        private int _ownerID;
        private SimpleShieldEffect _visualShield;

        private float _timer;
        private bool _isReflecting;

        private const float CooldownTime = 4.5f; 
        private const float ActiveDuration = 3.0f;
        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _ownerID = character.GetInstanceID();
            
            _visualShield = new SimpleShieldEffect(
                character.transform, 
                new Color(1f, 0.84f, 0f, 0.35f),
                1.3f
            );
            
            _timer = CooldownTime; 
            _isReflecting = false;
        }

        private void StartReflect()
        {
            if (_isReflecting) return;

            _isReflecting = true;
            _timer = 0f;
            ActiveReflectorIDs.Add(_ownerID);
            
            // 弹字要自己转交——联机模组把通用的 PopText 补丁注释掉了（见 PlayerEffectRelay）。
            PlayerEffectRelay.PopTextOnElite(Ctx.Character, "EliteEnemies_Affix_Reflect_PopText", null);
            _visualShield.Show();
        }

        private void EndReflect()
        {
            if (!_isReflecting) return;

            _isReflecting = false;
            _timer = 0f;
            ActiveReflectorIDs.Remove(_ownerID);
            _visualShield.Hide();
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (_isReflecting) ActiveReflectorIDs.Remove(_ownerID);
            
            _visualShield?.Destroy();
            

        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (Ctx == null || Ctx.Character == null || !Ctx.Character.gameObject.activeInHierarchy) return;

            _timer += deltaTime;

            if (_isReflecting)
            {
                if (_timer >= ActiveDuration) EndReflect();
            }
            else
            {
                if (_timer >= CooldownTime) StartReflect();
            }
            
            _visualShield?.Update(deltaTime);
        }
    }
}