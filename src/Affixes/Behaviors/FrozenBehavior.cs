using EliteEnemies.Buffs;
using EliteEnemies.Buffs.Effects;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 严寒：命中玩家时叠加一层「寒冷」，叠满 5 层后转化为冻结。
    ///
    /// <para>叠加、减速与「满层转冻结」都在 <see cref="ChillBuff"/> 里——那个 Buff 挂在
    /// **玩家**身上，层数累计与"是哪个精英打的"无关（两个精英各打两下就该叠到 4 层）。
    /// 记在行为类里的话，每个精英各记各的，永远叠不满。</para>
    /// </summary>
    public class FrozenBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Frozen";

        /// <summary>
        /// 两次叠层之间的最短间隔，**每个精英各算各的**。
        ///
        /// <para>⚠ 不节流的话霰弹枪一枪就能叠满：<c>OnHitPlayer</c> 是**每颗弹丸**各触发一次
        /// （补丁打在 <c>DamageReceiver.Hurt</c> 上，见 <c>DamageReceiverPatches.cs</c>，
        /// 而游戏对每颗弹丸都会走一遍 <c>Hurt</c>）。机枪同理。</para>
        /// </summary>
        private const float StackInterval = 0.5f;

        /// <summary>上次成功叠层的时刻。初值取一个远早于 <c>Time.time</c> 的值，保证第一次命中必定叠上。</summary>
        private float _lastStackTime = -999f;

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (Time.time < _lastStackTime + StackInterval) return;

            // 冻结免疫中（冻结期间 + 冻结结束后 ChillBuff.FreezeImmunitySeconds 秒）不再叠寒冷，
            // 见 ChillBuff.IsFreezeImmune。
            // ⚠ 这一条**必须**有，否则会连锁冻结：冻结期间寒冷照叠，满层后同 ID 走刷新分支，
            //   把冻结时长重新刷满——只要精英持续命中，玩家就永远出不来。
            if (ChillBuff.IsFreezeImmune(CharacterMainControl.Main)) return;

            // 施加失败（玩家不存在等）时**不**推进计时——否则那一次间隔白等。
            if (!EliteBuffs.ApplyToPlayer<ChillBuff>(victim, attacker)) return;

            _lastStackTime = Time.time;
        }
    }
}
