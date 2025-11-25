// ChaosOnHitBehavior.cs

using System;
using System.Collections.Generic;
using UnityEngine;
using Duckov;
using Duckov.Buffs;
using Duckov.Utilities;
using Random = UnityEngine.Random;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 词缀：混沌（Chaos On Hit）
    /// </summary>
    public class ChaosOnHitBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Chaos";
        
        private static readonly float CooldownSeconds = 0.7f;
        // 内置全局冷却，共享
        private static float _lastApplyTime = -999f;
        
        private readonly Lazy<string> _chaosPopTextFmt = new(() =>
            LocalizationManager.GetText(
                "Affix_Chaos_PopText_1",
                "<color=#9400D3>混沌侵蚀！{0}</color>"
            )
        );

        private string ChaosPopTextFmt => _chaosPopTextFmt.Value;

        // 预定义的负面 Buff 列表
        private static readonly Buff[] NegativeDebuffs =
        {
            GameplayDataSettings.Buffs.BleedSBuff, // Bleeding
            GameplayDataSettings.Buffs.Poison, // Poison
            GameplayDataSettings.Buffs.Pain, // Pain
            GameplayDataSettings.Buffs.Electric, // Electric
            GameplayDataSettings.Buffs.Burn, // Burning
            GameplayDataSettings.Buffs.Space, // Space
            // 这三个不会自动取消
            // TryAdd(GameplayDataSettings.Buffs.Weight_Overweight);          // Weight 
            // TryAdd(GameplayDataSettings.Buffs.Starve);            // Starve
            // TryAdd(GameplayDataSettings.Buffs.Thirsty);           // Thirsty
        };

        public void OnAttack(CharacterMainControl attacker, DamageInfo dmg)
        {
        }
        
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo)
        {
            if (Time.time - _lastApplyTime < CooldownSeconds)
                return;
            // 从预定义列表中随机挑选一个 Buff
            var pick = NegativeDebuffs[Random.Range(0, NegativeDebuffs.Length)];
            if (!pick) return;
            
            var player = CharacterMainControl.Main;
            player.AddBuff(pick, attacker, 0);
            
            string text = string.Format(ChaosPopTextFmt, pick.DisplayName);
            attacker.PopText(text);
            _lastApplyTime = Time.time;
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo dmg)
        {
        }

        public override void OnEliteInitialized(CharacterMainControl c)
        {
        }

        public override void OnEliteDeath(CharacterMainControl c, DamageInfo dmg)
        {
            foreach (var buff in NegativeDebuffs)
            {
                if (buff)
                {
                    CharacterMainControl.Main.RemoveBuffsByTag(buff.ExclusiveTag, false);
                }
            }
        }

        public override void OnCleanup(CharacterMainControl c)
        {
        }
    }
}