using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    public static class AffixBehaviorRegistration
    {
        public static void RegisterAllBehaviors()
        {
            AffixBehaviorManager.RegisterBehavior<TestTalkativeBehavior>();
            AffixBehaviorManager.RegisterBehavior<RegenerationBehavior>();
            AffixBehaviorManager.RegisterBehavior<InvisibilityBehavior>();
            AffixBehaviorManager.RegisterBehavior<SplitBehavior>();
            AffixBehaviorManager.RegisterBehavior<GigantificationBehavior>();
            AffixBehaviorManager.RegisterBehavior<MiniaturationBehavior>();
            AffixBehaviorManager.RegisterBehavior<SelfDestructBehavior>();
            AffixBehaviorManager.RegisterBehavior<UndyingBehavior>();
            AffixBehaviorManager.RegisterBehavior<StickyBehavior>();
            AffixBehaviorManager.RegisterBehavior<MimicTearBehavior>();
            AffixBehaviorManager.RegisterBehavior<TimeStopBehavior>();
            AffixBehaviorManager.RegisterBehavior<MagazineCurseBehavior>();
            AffixBehaviorManager.RegisterBehavior<KnockbackBehavior>();
            AffixBehaviorManager.RegisterBehavior<ChaosOnHitBehavior>();
            AffixBehaviorManager.RegisterBehavior<VampirismBehavior>();
            AffixBehaviorManager.RegisterBehavior<SlimeBehavior>();
            AffixBehaviorManager.RegisterBehavior<BlindnessBehavior>();
            AffixBehaviorManager.RegisterBehavior<SlowBehavior>();
            AffixBehaviorManager.RegisterBehavior<StunBehavior>();
            AffixBehaviorManager.RegisterBehavior<DungEaterBehavior>();

            Debug.Log($"[EliteEnemies.AffixBehavior] 注册完成，共 {AffixBehaviorManager.Count} 个词缀行为类型");
        }
    }
}