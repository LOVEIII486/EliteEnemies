using System;
using System.Reflection;
using UnityEngine;
using Duckov.Buffs;
using Duckov.Utilities;

namespace EliteEnemies.AffixBehaviors
{
    // 震慑
    public class StunBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        private const string LogTag = "[EliteEnemies.StunBehavior]";
        public override string AffixName => "Stun";
        
        // 都是static函数 要么 const 要么 static readonly
        private static readonly string BuffName = "EliteBuff_Stun";
        private static readonly int BuffId = 99903;
        private static readonly bool BuffLimitedLifeTime = true;
        private static readonly float BuffDuration = 5f;
        
        // 共享Buff实例
        private static Buff _sharedBuff;
        private static bool _buffCreated = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            // 只创建一次共享的Buff
            if (!_buffCreated)
            {
                CreateSharedEliteBuff();
            }
        }

        private static void CreateSharedEliteBuff()
        {
            if (_buffCreated) return;

            try
            {
                Buff baseBuff = GameplayDataSettings.Buffs.BaseBuff;
                if (baseBuff == null)
                {
                    Debug.LogError($"{LogTag} 找不到BaseBuff");
                    return;
                }

                _sharedBuff = UnityEngine.Object.Instantiate(baseBuff);
                _sharedBuff.name = BuffName;
                
                UnityEngine.Object.DontDestroyOnLoad(_sharedBuff.gameObject);
                
                // 使用反射设置私有字段
                var buffType = typeof(Buff);
                
                var idField = buffType.GetField("id", BindingFlags.Instance | BindingFlags.NonPublic);
                if (idField != null)
                    idField.SetValue(_sharedBuff, BuffId);
                
                var limitedField = buffType.GetField("limitedLifeTime", BindingFlags.Instance | BindingFlags.NonPublic);
                if (limitedField != null)
                    limitedField.SetValue(_sharedBuff, BuffLimitedLifeTime);
                
                var durationField = buffType.GetField("totalLifeTime", BindingFlags.Instance | BindingFlags.NonPublic);
                if (durationField != null)
                    durationField.SetValue(_sharedBuff, BuffDuration);

                _buffCreated = true;
                Debug.Log($"{LogTag} {BuffName} 创建成功, ID:{BuffId}, 持续时间:{BuffDuration}秒");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} {BuffName} 创建Buff失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (!_buffCreated || _sharedBuff == null) return;
            if (!AffixBehaviorUtils.IsPlayerHitByAttacker(character)) return;

            var player = CharacterMainControl.Main;
            if (player == null) return;

            try
            {
                player.AddBuff(_sharedBuff, character, 0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} {_sharedBuff.name} 添加Buff失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnHitPlayer(CharacterMainControl attacker, DamageInfo damageInfo) { }
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}