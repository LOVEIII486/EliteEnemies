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

        /// <summary>
        /// 该角色实例当前是否处于反射状态（供弹道补丁查询）。
        ///
        /// <para>⚠ <b>联机下这个判定发生在"开枪那台机器"上，而不是主机。</b>
        /// 子弹由开枪方本地模拟（联机侧只给远端投射物造"假"实例），
        /// 所以客机射出的子弹是在客机上跑到这里的——而客机的复制体
        /// <b>没有本行为类</b>（客机只挂 <c>EliteMarker</c>，不跑任何词条行为），
        /// <see cref="ActiveReflectorIDs"/> 恒空。</para>
        ///
        /// <para>⇒ 必须再问一次联机模块（<see cref="EliteStateRelay.ReflectQueryHandler"/>，
        /// 客机侧按主机广播来的权威状态回答）。<b>单机下那个钩子是 null，恒为 false，
        /// 于是这里与从前一字不差。</b></para>
        /// </summary>
        public static bool IsReflecting(int characterInstanceID)
            => ActiveReflectorIDs.Contains(characterInstanceID)
               || EliteStateRelay.QueryReflectState(characterInstanceID);

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

            // 告知联机模块：客机要靠这条状态才能在自己那边把子弹弹开
            // （判定发生在开枪方，见 IsReflecting 的注释）。单机下没人接管。
            EliteStateRelay.RelayReflectState(Ctx.Character, true);

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

            EliteStateRelay.RelayReflectState(Ctx.Character, false);

            _visualShield.Hide();
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (_isReflecting)
            {
                ActiveReflectorIDs.Remove(_ownerID);

                // 清理时也要报一次"结束"：精英可能只是被 StripElite（撤销精英化但仍活着），
                // 那时复制体还在客机上、还停在上一次的"反射中" ⇒ 客机会一直把子弹弹开。
                EliteStateRelay.RelayReflectState(character, false);
            }

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