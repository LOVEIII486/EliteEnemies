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

        /// <summary>弹在**玩家**头顶那条的本地化键。**转交时传键，不传译文**（见 PlayerEffectRelay）。</summary>
        private const string PlayerPopKey = "EliteEnemies_Affix_Sticky_PopText_2";

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
                PlayerEffectRelay.PopTextOnElite(owner, "EliteEnemies_Affix_Sticky_PopText_1", null);
                return;
            }

            if (!PlayerEffectActions.DropCurrentWeapon(player))
            {
                // 手上不是武器：按配置决定要不要消耗这次机会
                if (ConsumeWhenNoWeapon) _consumed = true;
                return;
            }

            PlayerEffectRelay.PopTextOnElite(owner, "EliteEnemies_Affix_Sticky_PopText_1", null);
            // 玩家侧弹字同理：主机弹在复制体上、真人看不到 ⇒ 联机下只让客机弹。
            // **转交的是键**；本地那份在这里现解析。
            if (!PlayerEffectRelay.TryRelayPlayerPopText(player, PlayerPopKey))
                player.PopText(LocalizationManager.GetText(PlayerPopKey));

            _consumed = true;
        }
        
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

    }
}
