
namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 持续回复词缀 - 每隔一段时间回复生命值
    /// </summary>
    public class RegenerationBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Regeneration";
        
        private static readonly float RegenInterval = 0.5f;     // 每0.5秒回复一次
        private static readonly float RegenAmount = 7f;      // 每次回复10点生命
        
        private float _regenTimer = 0f;        // 每个敌人独立的计时器

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (character == null) return;

            var health = character.Health;
            if (health == null || health.CurrentHealth >= health.MaxHealth)
            {
                return; // 生命已满，不需要回复
            }

            _regenTimer += deltaTime;

            if (_regenTimer >= RegenInterval)
            {
                _regenTimer = 0f; // 重置计时器（每个敌人独立）
                character.AddHealth(RegenAmount);
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character == null) return;
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (character == null) return;
        }
    }
}