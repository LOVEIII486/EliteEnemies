using System.Collections.Generic;
using Duckov.Buffs;
using EliteEnemies.Core;
using ItemStatsSystem;
using UnityEngine;
using SodaCraft.Localizations;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【分裂】词缀 - 敌人残血时分裂成多个较弱的小怪
    /// </summary>
    public class SplitBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "Split";

        private static readonly int MinSplitCount = 2;
        private static readonly int MaxSplitCount = 4;
        private static readonly float SplitRadius = 2.0f;
        private static readonly float SplitHealthRatio = 0.6f;
        private static readonly float SplitDamageRatio = 0.7f;
        private static readonly float SplitSpeedRatio = 1.15f;

        // 全局活跃分裂体计数器
        public static int GlobalActiveSplitClones { get; private set; } = 0;

        private CharacterMainControl _originalCharacter;
        private bool _hasSplit = false;

        /// <summary>熔断的告警**每只只报一次**。见 <see cref="TriggerSplit"/> 里对它的说明。</summary>
        private bool _breakerWarned = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GlobalActiveSplitClones = 0;
        }

        public class SplitCloneMarker : MonoBehaviour
        {
            private void Start()
            {
                SplitBehavior.GlobalActiveSplitClones++;
            }

            private void OnDestroy()
            {
                SplitBehavior.GlobalActiveSplitClones = Mathf.Max(0, SplitBehavior.GlobalActiveSplitClones - 1);
            }
        }

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _originalCharacter = character;
            _hasSplit = false;
            _breakerWarned = false;

            // 保底通道：覆盖**不经 DamageReceiver** 的伤害来源——环境伤害区（`ZoneDamage.cs:70`）、
            // 效果动作（`DamageAction.cs:49`）等都是直接 `health.Hurt(...)`，不会触发框架的
            // `OnDamaged`。两条通道覆盖面不同，**缺一不可**（同 `UndyingBehavior` 的取舍与理由）。
            Health.OnHurt += OnAnyHealthHurt;
        }

        /// <summary>
        /// 预判通道：框架经 <c>DamageReceiver.OnHurtEvent</c> 转发（<c>DamageReceiver.cs:95</c>），
        /// **发生在扣血之前**。
        ///
        /// <para>⚠ 因此这里必须用 <c>CurrentHealth - damageValue</c> **预测扣血后的血量**：
        /// 直接看当前血量会让分裂**整整晚一拍**（40%→10% 那一击不算，要等下一击），
        /// 而"从阈值以上一击打死"（40% 直接被带走）则**永远不再分裂**——
        /// 旧实现挂在**扣血之后**的静态事件上，连死后的那一击也会从尸体分裂出来。</para>
        /// </summary>
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_hasSplit) return;

            var health = character != null ? character.Health : null;
            if (health == null) return;
            if (health.MaxHealth <= 20) return;   // 基础限制，防止极弱小单位产生无限分裂

            // 用**原始伤害**（护甲/减伤换算要等 Health.Hurt 里才做）：宁可早触发，不可漏掉致命一击
            float incoming = Mathf.Max(0f, damageInfo.damageValue);
            if (health.CurrentHealth - incoming >= health.MaxHealth / 3) return;

            TrySplit(character);
        }

        /// <summary>保底通道：扣血**之后**（任意伤害来源）判断是否已掉进阈值。</summary>
        private void OnAnyHealthHurt(Health health, DamageInfo damageInfo)
        {
            if (_hasSplit || health == null) return;

            var character = _originalCharacter;
            if (character == null || character.Health != health) return;
            if (health.MaxHealth <= 20) return;
            if (health.CurrentHealth >= health.MaxHealth / 3) return;

            TrySplit(character);
        }

        /// <summary>
        /// ⚠ **只有真的发起成功才置位**。原先不管成败都先 `_hasSplit = true`，
        /// 于是「熔断 / 生成器未就绪」会把这只敌人的分裂机会**永久吃掉**——
        /// 即使下一帧帧率恢复，它也不会再分裂了，而外部只看得到一条 warning。
        ///
        /// <para>失败时留着重试是**免费**的：`TriggerSplit` 的熔断检查在所有重活之前
        /// （两次属性读 + 一次除法），失败即返回。唯一的成本是日志，所以那里的告警
        /// 改成每只只报一次——否则低帧率期间每次受击都写日志，而写日志本身要花时间，
        /// 会形成「掉帧→写日志→更掉帧」的正反馈。</para>
        /// </summary>
        private void TrySplit(CharacterMainControl character)
        {
            if (TriggerSplit(character)) _hasSplit = true;
        }

        /// <summary>攻击不参与本词条逻辑（接口要求实现）。</summary>
        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            Health.OnHurt -= OnAnyHealthHurt;
        }

        /// <returns>是否**成功发起**了分裂。失败（熔断 / 生成器未就绪）返回 false，调用方据此保留重试机会。</returns>
        private bool TriggerSplit(CharacterMainControl character)
        {
            int maxClones = EliteEnemyCore.Config.SplitAffixMaxCloneCount;
            float minFps = EliteEnemyCore.Config.SplitAffixMinFPSThreshold;
            
            // 1. 性能与数量熔断检查
            float currentFPS = 1.0f / Mathf.Max(Time.smoothDeltaTime, 0.001f);
            if (currentFPS < minFps || GlobalActiveSplitClones >= maxClones)
            {
                if (!_breakerWarned)
                {
                    _breakerWarned = true;
                    Debug.LogWarning($"[SplitBehavior] 熔断触发 (FPS:{currentFPS:F1}, Count:{GlobalActiveSplitClones})" +
                                     "——本次不分裂，但**机会保留**，帧率恢复后下次受击会再试");
                }
                return false;
            }

            var helper = EggSpawnHelper.Instance;
            if (helper == null || !helper.IsReady) return false;
            
            // 2. 捕获父级当前的 Buff 快照以便继承
            List<Buff> buffsToInherit = new List<Buff>();
            var activeBuffs = BuffInheritanceHelper.GetActiveBuffs(character);
            if (activeBuffs != null)
            {
                foreach (var b in activeBuffs)
                {
                    if (b != null) buffsToInherit.Add(b);
                }
            }
            
            Vector3 deathPosition = character.transform.position;
            int splitCount = Random.Range(MinSplitCount, MaxSplitCount + 1);
            
            // 修正生成数量不超过剩余额度
            int remainingQuota = maxClones - GlobalActiveSplitClones;
            if (splitCount > remainingQuota) splitCount = Mathf.Max(1, remainingQuota);

            // 获取原始显示的中文名（或本地化文本）
            // 官方口子：CharacterRandomPreset.DisplayName 就是 nameKey.ToPlainText()（CharacterRandomPreset.cs:247）
            string originalDisplayName = character.characterPreset.DisplayName;

            // 3. 批量生成分裂体
            helper.SpawnCloneCircle(
                originalEnemy: character,
                centerPosition: deathPosition,
                count: splitCount,
                radius: SplitRadius,
                healthMultiplier: SplitHealthRatio,
                damageMultiplier: SplitDamageRatio,
                speedMultiplier: SplitSpeedRatio,
                scaleMultiplier: 1f,
                preventElite: false, // 分裂体本身可以通过逻辑再次产生，但受 GlobalActiveSplitClones 熔断保护
                customKeySuffix: "EE_Split", 
                customDisplayName: originalDisplayName,
                onAllSpawned: (clones) => 
                {
                    // 一个都没生成出来 ⇒ 这次不算「分裂过了」，把机会还回去
                    // （否则「已经置位」就成了一次静默的消耗）。见 TriggerSplit 的契约。
                    if (clones == null || clones.Count == 0)
                    {
                        _hasSplit = false;
                        return;
                    }
                    foreach (var clone in clones)
                    {
                        if (clone != null)
                        {
                            // 挂载计数标记
                            clone.gameObject.AddComponent<SplitCloneMarker>();

                            // 继承父级 Buff
                            if (buffsToInherit.Count > 0)
                            {
                                BuffInheritanceHelper.ApplyBuffsTo(clone, buffsToInherit);
                            }
                        }
                    }
                });

            return true;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            Health.OnHurt -= OnAnyHealthHurt;   // 幂等：未订阅时 `-=` 是无害的

            _originalCharacter = null;
            _hasSplit = false;
        }
    }

    // 辅助工具：Buff 继承逻辑
    public static class BuffInheritanceHelper
    {
        public static List<Duckov.Buffs.Buff> GetActiveBuffs(CharacterMainControl target)
        {
            if (target == null) return null;

            // 官方取法：CharacterMainControl.GetBuffManager()（CharacterMainControl.cs:2647，
            // 内部直接返回 [SerializeField] 的私有字段）。原先这里用
            // GetComponent<CharacterBuffManager>()——只在"组件就挂在这个对象上"时才找得到，
            // 而游戏自己一律走 GetBuffManager()（BuffsDisplay.cs:60、BDSManager.cs:194）。
            var manager = target.GetBuffManager();
            if (manager == null) return null;

            // 再走官方只读口子 Buffs（CharacterBuffManager.cs:21，返回 ReadOnlyCollection<Buff>），
            // 不再依赖 Publicizer 把私有字段 buffs 公开——那是"改名即静默失效"的一类。
            var buffs = new List<Duckov.Buffs.Buff>();
            foreach (var buff in manager.Buffs)
            {
                if (buff != null) buffs.Add(buff);
            }
            return buffs;
        }

        public static void ApplyBuffsTo(CharacterMainControl target, List<Duckov.Buffs.Buff> buffsToCopy)
        {
            if (target == null || buffsToCopy == null || buffsToCopy.Count == 0) return;

            foreach (var originalBuff in buffsToCopy)
            {
                if (originalBuff == null) continue;
                var clonedBuff = Object.Instantiate(originalBuff);
                clonedBuff.name = originalBuff.name.Replace("(Clone)", "");
                
                try
                {
                    target.AddBuff(clonedBuff, null, 1);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[BuffInheritance] Failed to inherit {clonedBuff.name}: {e.Message}");
                    Object.Destroy(clonedBuff.gameObject);
                }
            }
        }
    }
}