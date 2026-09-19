using EliteEnemies.Localization;
using EliteEnemies.Modifiers;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 词缀：不死（Undead）
    /// 效果：生命值将跌破阈值（20%）时，抢在扣血前锁血并回血至 50%，同时获得 2.5 秒无敌。
    ///
    /// <para><b>为什么有两条伤害通道（缺一不可，别"顺手"删掉其中一条）</b>：</para>
    /// <list type="bullet">
    /// <item><b>预判通道</b> <see cref="OnDamaged"/>：框架经 <c>DamageReceiver.OnHurtEvent</c> 转发
    /// （<c>DamageReceiver.cs:95</c>），发生在**扣血之前**。这一条才是"不死"能**扛住致命一击**的原因——
    /// 它赶在 <c>Health.Hurt</c> 之前把血补回 50% 并给上无敌，而 `Health.Hurt` 开头就是
    /// <c>if (invincible) return false;</c>（<c>Health.cs:314</c>）⇒ 那一击被完全挡下。
    /// 子弹/近战/爆炸都走这条路。</item>
    /// <item><b>保底通道</b> <see cref="OnAnyHealthHurt"/>：静态 <c>Health.OnHurt</c>
    /// （<c>Health.cs:454</c>）覆盖**不经 DamageReceiver** 的伤害来源——环境伤害区
    /// （<c>ZoneDamage.cs:70</c>）、效果动作（<c>DamageAction.cs:49</c>）等。
    /// 代价是它在扣血**之后**才发（死亡判定还更早，见 <c>Health.cs:437-455</c>），
    /// 所以它救不了致命一击，只补"没死但掉进阈值"的情况。</item>
    /// </list>
    /// <para>历史：本行为原先**只有**保底通道，于是"不死"从来救不了致命一击
    /// （事后 <c>SetHealth</c> 也不重置 <c>isDead</c>）。</para>
    /// </summary>
    public class UndyingBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Undead";

        private static readonly float ThresholdRatio = 0.2f;  // 触发阈值 (20%)
        private static readonly float HealTargetRatio = 0.5f; // 回血目标 (50%)
        private static readonly float InvincibleDuration = 2.5f; // 无敌时间

        private bool _triggered;         // 是否已触发（仅限一次）
        private bool _isInvincible;      // 当前是否处于词条赋予的无敌状态
        private bool _originalInvincibleState; // 记录触发前的无敌状态（用于还原）
        private float _invincibleEndTime;      // 无敌结束时间戳

        private string PopLineStart =>
            LocalizationManager.GetText("EliteEnemies_Affix_Undead_PopText_1");

        private string PopLineEnd =>
            LocalizationManager.GetText("EliteEnemies_Affix_Undead_PopText_2");

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null || character.Health == null) return;

            _triggered = false;
            _isInvincible = false;

            Health.OnHurt += OnAnyHealthHurt;   // 保底通道，见类注释
        }

        /// <summary>
        /// 预判通道：扣血**之前**判断这一击会不会把血量打到阈值以下。
        ///
        /// <para>用的是 <c>damageInfo.damageValue</c>（**原始伤害**）——护甲/减伤换算要等
        /// <c>Health.Hurt</c> 里才做（<c>finalDamage</c>），这里拿不到。
        /// 宁可早触发，也不要漏掉致命一击。</para>
        /// </summary>
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_triggered) return;
            if (character == null || character.Health == null || character.Health.IsDead) return;

            float threshold = character.Health.MaxHealth * ThresholdRatio;
            float incoming = Mathf.Max(0f, damageInfo.damageValue);

            if (character.Health.CurrentHealth - incoming > threshold) return;

            TriggerUndying(character);
        }

        /// <summary>攻击不参与本词条逻辑（接口要求实现）。</summary>
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        /// <summary>保底通道：扣血**之后**（任意伤害来源）判断是否已掉进阈值。</summary>
        private void OnAnyHealthHurt(Health health, DamageInfo damageInfo)
        {
            if (health == null) return;

            CharacterMainControl character = Ctx?.Character;
            if (character == null || character.Health != health) return;

            // ⚠ 原先这里有一句调试探针 `UndyingDamageProbe.LogSettled(...)`（2026-09-18 随
            //    `src\DebugTools` 精简一并删除）。它当时是为了定位一种**已知但未修**的漏判：
            //    **预判通道读的是原始伤害**（`damageInfo.damageValue`，护甲换算还没做），
            //    而**保底通道在扣血之后**才发 ⇒ 绕过 `DamageReceiver` 的致命伤害救不回来
            //    （例如环境伤害区、DoT）。这一条**至今仍未解决**，只是探针没了。
            //    如果日后又有玩家报"不死没生效"，先从这里查，不要再重新发明那个探针。

            if (_triggered) return;
            if (health.IsDead) return;

            if (health.CurrentHealth <= health.MaxHealth * ThresholdRatio)
            {
                TriggerUndying(character);
            }
        }

        private void TriggerUndying(CharacterMainControl character)
        {
            _triggered = true;

            Health health = character.Health;

            float targetHp = health.MaxHealth * HealTargetRatio;
            if (health.CurrentHealth < targetHp)
            {
                health.SetHealth(targetHp);
            }

            _originalInvincibleState = health.Invincible;
            _isInvincible = true;
            _invincibleEndTime = Time.time + InvincibleDuration;
            health.SetInvincible(true);

            PlayerEffectRelay.PopTextOnElite(character, PopLineStart);

            ModifyAI(character, AIFields.ShootCanMove, true);
            ModifyAI(character, AIFields.CanDash, true);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (!_isInvincible) return;
            if (Time.time >= _invincibleEndTime) EndInvincibility(character);
        }

        private void EndInvincibility(CharacterMainControl character)
        {
            if (character != null && character.Health != null)
            {
                character.Health.SetInvincible(_originalInvincibleState);
                if (!character.Health.IsDead)
                {
                    PlayerEffectRelay.PopTextOnElite(character, PopLineEnd);
                }
            }
            _isInvincible = false;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);

            Health.OnHurt -= OnAnyHealthHurt;

            if (_isInvincible && character != null && character.Health != null)
            {
                character.Health.SetInvincible(_originalInvincibleState);
            }

            _isInvincible = false;
            _triggered = false;
        }
    }
}
