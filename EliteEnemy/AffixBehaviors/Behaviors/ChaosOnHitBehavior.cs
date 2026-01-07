using System;
using Duckov.Buffs;
using Duckov.Utilities;
using EliteEnemies.Localization;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 混沌
    /// </summary>
    public class ChaosOnHitBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Chaos";

        private static readonly float CooldownSeconds = 0.5f;

        // 内置全局冷却，共享
        private static float _lastApplyTime = -999f;

        private readonly Lazy<string> _chaosPopTextFmt = new(() =>
            LocalizationManager.GetText(
                "Affix_Chaos_PopText_1")
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
            var player = CharacterMainControl.Main;
            if (player == null) return;

            foreach (var buffPrefab in NegativeDebuffs)
            {
                if (buffPrefab != null)
                {
                    player.RemoveBuff(buffPrefab.ID, false);
                }
            }
        }

        public override void OnCleanup(CharacterMainControl c)
        {
        }
    }
}