using System;
using EliteEnemies.Core;
using UnityEngine;
using EliteEnemies.Localization;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 词缀：弹匣诅咒
    /// </summary>
    public class MagazineCurseBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "MagazineCurse";
        
        private static readonly float Cooldown = 12.0f;

        /// <summary>弹在**玩家**头顶那条的本地化键。**转交时传键，不传译文**（见 PlayerEffectRelay）。</summary>
        private const string PlayerPopKey = "EliteEnemies_Affix_MagazineCurse_PopText_2";

        private float _lastTriggerTime = -999f;

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (Time.time - _lastTriggerTime < Cooldown) return;

            var attacker = damageInfo.fromCharacter;
            if (attacker == null) return;

            // ⚠ 原先这里只认 `IsMainCharacter`（**本机**玩家）⇒ **客机攻击时静默永不触发**。
            //   联机下判定在主机上跑，攻击者往往是客机玩家的**复制体**。
            if (!attacker.IsMainCharacter && !EliteEnemyCore.IsRemotePlayerCharacter(attacker)) return;

            var player = attacker;

            // 联机下把效果**转交给受害者那台机器**执行——主机上那个只是复制体，
            // 改它到不了真人（位置/背包/武器都是客机自报的快照）。
            // 单机、以及联机时主机自己的玩家 ⇒ 返回 false，照常本地执行。
            if (PlayerEffectRelay.TryRelay(player, PlayerEffectRelay.Kind.ForceReload))
            {
                _lastTriggerTime = Time.time;
                return;
            }

            if (!PlayerEffectActions.ForceReload(player)) return;   // 没枪 / 换不了 ⇒ 不进冷却

            PlayerEffectRelay.PopTextOnElite(character, "EliteEnemies_Affix_MagazineCurse_PopText_1", null);
            // 玩家侧弹字同理：主机弹在复制体上、真人看不到 ⇒ 联机下只让客机弹。
            // 单机 / 主机自己的玩家照样本地弹（`TryRelayPlayerPopText` 返回 false）。
            // **转交的是键**；本地那份在这里现解析（传译文会把主机那门语言焊死到客机）。
            if (!PlayerEffectRelay.TryRelayPlayerPopText(player, PlayerPopKey))
                player.PopText(LocalizationManager.GetText(PlayerPopKey));
            _lastTriggerTime = Time.time;
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;
            _lastTriggerTime = -999f;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _lastTriggerTime = -999f;
        }
    }
}