namespace EliteEnemies.AffixBehaviors
{
    public class BerserkBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Berserk";
        private bool _berserkTriggered = false;
    
        public override void OnEliteInitialized(CharacterMainControl character)
        {
            // AttributeModifier.Quick.ModifyHealth(character, 0.8f, healToFull: true);
        }
    
        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            // 血量低于50%触发狂暴
            if (character.Health.CurrentHealth < character.Health.MaxHealth * 0.5f && !_berserkTriggered)
            {
                _berserkTriggered = true;
                character.PopText("怒了！");
            
                // 战斗中立即修改，immediate = true
                StatModifier.Multiply(character, StatModifier.Attributes.GunDamageMultiplier, 1.3f);
                StatModifier.Multiply(character, StatModifier.Attributes.MeleeDamageMultiplier, 1.3f);
                StatModifier.Multiply(character, StatModifier.Attributes.WalkSpeed, 1.4f);
                
                AIFieldModifier.ModifyImmediate(character,AIFieldModifier.Fields.ShootCanMove,1f);
                AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.ShootDelay, 0.5f, multiply: true);
                AIFieldModifier.ModifyImmediate(character, AIFieldModifier.Fields.CanDash, 1f);
            }
        }
    }
}