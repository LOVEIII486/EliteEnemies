using Duckov.Buffs;
using Duckov.Utilities;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 冻结
    /// </summary>
    public class FrozenBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Frozen";

        private Buff _frozenPrefab;
        
        private const float TriggerChance = 0.33f;
        private const float CooldownDuration = 15f;
        private float _lastTriggerTime = -999f;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _frozenPrefab = FindOriginBuffPrefab(1127);
            if (_frozenPrefab == null)
            {
                Debug.LogError($"[EliteEnemies.FrozenBehavior] 无法找到1127Buff");
            }
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            if (_frozenPrefab == null || CharacterMainControl.Main == null || CharacterMainControl.Main.Health.IsDead)
                return;

            if (Time.time < _lastTriggerTime + CooldownDuration)
                return;

            if (UnityEngine.Random.value > TriggerChance)
                return;

            CharacterMainControl.Main.AddBuff(_frozenPrefab, attacker);
            
            _lastTriggerTime = Time.time;
        }
        
        private Buff FindOriginBuffPrefab(int id)
        {
            try
            {
                var buffsData = GameplayDataSettings.Buffs;
                if (buffsData == null) return null;

                // allBuffs 是私有 List<Buff>（TeamSoda.Duckov.Core/Duckov/Utilities/GameplayDataSettings.cs:347），
                // 由 publicizer 在编译期公开，直接访问。
                List<Buff> list = buffsData.allBuffs;
                if (list != null)
                {
                    return list.Find(b => b.ID == id);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EliteEnemies.FrozenBehavior] 读取 Buff 列表失败: {ex}");
            }
            return null;
        }



    }
}