using System;
using EliteEnemies.Localization;
using EliteEnemies.Core;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 词缀：粘性（Sticky）
    /// 玩家攻击该精英时让玩家掉落当前武器（仅触发一次）
    /// </summary>
    public class StickyBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Sticky";

        private string EnemyPopLine =>
            LocalizationManager.GetText("EliteEnemies_Affix_Sticky_PopText_1");

        private string PlayerPopLine =>
            LocalizationManager.GetText("EliteEnemies_Affix_Sticky_PopText_2");

        private static readonly bool ConsumeWhenNoWeapon = true;

        private bool _consumed = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _consumed = false;
        }
        
        public void OnDamaged(CharacterMainControl owner, DamageInfo dmg)
        {
            if (_consumed || owner == null)
                return;

            var attacker = dmg.fromCharacter;
            if (attacker == null) return;

            // ⚠ 原先只认 `IsMainCharacter`（本机玩家）⇒ 客机攻击时静默永不触发。
            if (!attacker.IsMainCharacter && !EliteEnemyCore.IsRemotePlayerCharacter(attacker)) return;

            var player = attacker;

            // 联机下转交给对方那台机器——主机改复制体的武器到不了真人。
            // ⚠ 这条路上无法知道对方手上是不是武器（那在客机那边），
            //   所以一律按“消耗掉这次机会”处理，不判 ConsumeWhenNoWeapon。
            if (PlayerEffectRelay.TryRelay(player, PlayerEffectRelay.Kind.DropWeapon))
            {
                _consumed = true;
                owner.PopText(EnemyPopLine);
                return;
            }

            if (!PlayerEffectActions.DropCurrentWeapon(player))
            {
                // 手上不是武器：按配置决定要不要消耗这次机会
                if (ConsumeWhenNoWeapon) _consumed = true;
                return;
            }

            owner.PopText(EnemyPopLine);
            player.PopText(PlayerPopLine);

            _consumed = true;
        }
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }



    }
}
