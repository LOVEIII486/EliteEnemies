using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 噬弹：命中玩家时，从玩家**当前武器的弹匣里吃掉子弹**。
    ///
    /// <para>与既有的「弹匣诅咒」不是一回事：那个是强制玩家换弹（子弹还在），
    /// 这个是**真把子弹扣掉**。</para>
    /// </summary>
    public class AmmoEaterBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "AmmoEater";

        /// <summary>每次啃掉几发。</summary>
        private const int BulletsPerBite = 1;

        /// <summary>两次啃之间至少隔多久。不加节流的话，霰弹枪一轮多颗弹丸能瞬间清空弹匣。</summary>
        private const float BiteInterval = 0.5f;

        private float _lastBiteTime = -999f;

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (Time.time < _lastBiteTime + BiteInterval) return;

            // ⚠ 目标必须是 **victim（被打中的那个玩家）**，不是 `CharacterMainControl.Main`。
            //   联机下判定在主机上跑，而挨打的往往是**客机玩家的复制体**；
            //   写死 Main 会把效果挂到主机自己的玩家身上，客机什么都看不到。
            CharacterMainControl player = victim;
            if (player == null) return;

            // 联机下把效果**转交给受害者那台机器**执行——主机上那个只是复制体，
            // 改它到不了真人（位置/背包/武器都是客机自报的快照）。
            // 单机、以及联机时主机自己的玩家 ⇒ 返回 false，照常本地执行。
            if (PlayerEffectRelay.TryRelay(player, PlayerEffectRelay.Kind.ConsumeBullets, i: BulletsPerBite))
            {
                _lastBiteTime = Time.time;
                return;
            }

            // 本地：从真弹匣里扣（两道门都在 PlayerEffectActions 里，理由见那边的注释）
            if (PlayerEffectActions.ConsumeBullets(player, BulletsPerBite) > 0)
                _lastBiteTime = Time.time;
        }
    }
}
