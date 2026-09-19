using EliteEnemies.Modifiers;
using EliteEnemies.Localization;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 狂暴
    /// </summary>
    public class BerserkBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Berserk";
        
        private bool _berserkTriggered = false;
        public override void OnEliteInitialized(CharacterMainControl character) 
        {
            _berserkTriggered = false;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (character == null || character.Health == null) return;

            // 血量低于 70% 触发狂暴
            if (character.Health.CurrentHealth < character.Health.MaxHealth * 0.7f && !_berserkTriggered)
            {
                TriggerBerserk(character);
            }
        }

        private void TriggerBerserk(CharacterMainControl character)
        {
            _berserkTriggered = true;
            PlayerEffectRelay.PopTextOnElite(character, "EliteEnemies_Affix_Berserk_PopText_1", null);

            // 1. 伤害增加 30%
            // 复用现成的助手：它做的就是"枪 + 近战都乘同一倍率"（CharacterModifiers.cs:50）
            CharacterModifiers.Quick.ModifyDamage(character, 1.3f, AffixName);
            
            // 2. 移速增加 30%
            //    必须走 ModifySpeed —— MoveSpeed 不是游戏的属性 key，
            //    该方法是同时写 WalkSpeed 与 RunSpeed 的那一个（见 StatKeys 处的注释）
            CharacterModifiers.Quick.ModifySpeed(character, 1.3f, AffixName);

            // 3. AI 逻辑增强
            ModifyAI(character, AIFields.ShootCanMove, true);
            ModifyAI(character, AIFields.CanDash, true);
            ModifyAI(character, AIFields.BaseReactionTime, 0.5f);
        }
        

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);
            _berserkTriggered = false;
        }
    }
}