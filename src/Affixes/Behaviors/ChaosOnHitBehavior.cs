using System;
using System.Collections.Generic;
using Duckov.Buffs;
using Duckov.Utilities;
using EliteEnemies.Localization;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EliteEnemies.Affixes.Behaviors
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

        /// <summary>
        /// **本精英**成功施加过 debuff 的那些玩家。死亡时要按这批人逐个撤——
        /// 单人下就是本机玩家一个，联机下可能是好几个（也可能一个都不是）。
        /// </summary>
        private readonly HashSet<CharacterMainControl> _victims = new HashSet<CharacterMainControl>();

        private string ChaosPopTextFmt =>
            LocalizationManager.GetText("EliteEnemies_Affix_Chaos_PopText_1");

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

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (Time.time - _lastApplyTime < CooldownSeconds)
                return;

            // ⚠ 目标是**被打中的那个玩家**（`victim`），不是 `CharacterMainControl.Main`。
            //   后者是"本机玩家"——联机下主机判定时挨打的交给常是客机玩家，
            //   挂到 Main 就等于"精英打中客机、debuff 挂到主机玩家身上"，客机看不到。
            //   这也是本词条原先**绕过** `EliteBuffs.ApplyToPlayer` 直接 `AddBuff` 留下的坑。
            if (victim == null) return;

            // 从预定义列表中随机挑选一个 Buff
            var pick = NegativeDebuffs[Random.Range(0, NegativeDebuffs.Length)];
            if (!pick) return;

            victim.AddBuff(pick, attacker, 0);
            _victims.Add(victim);   // 记下来，死亡时要按这批人逐个撤

            string text = string.Format(ChaosPopTextFmt, pick.DisplayName);
            PlayerEffectRelay.PopTextOnEliteResolved(attacker, text);
            _lastApplyTime = Time.time;
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo dmg)
        {
        }

        public override void OnEliteDeath(CharacterMainControl c, DamageInfo dmg)
        {
            // ⚠ 这里原先也只对 `CharacterMainControl.Main` 撤——单人下没错，联机下错。
            //   精英临死要撤的是**它自己施加过的那几个人**，所以按 `_victims` 逐个撤。
            //   一只精英完全可能打过不止一个玩家。
            foreach (var player in _victims)
            {
                if (!player) continue;   // 可能已经销毁

                foreach (var buffPrefab in NegativeDebuffs)
                {
                    if (buffPrefab != null)
                    {
                        player.RemoveBuff(buffPrefab.ID, false);
                    }
                }
            }

            _victims.Clear();
        }

    }
}