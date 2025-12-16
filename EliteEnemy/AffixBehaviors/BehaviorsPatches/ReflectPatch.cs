using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors; 

namespace EliteEnemies.EliteEnemy.AffixBehaviors.BehaviorPatches
{
    [HarmonyPatch(typeof(Projectile))]
    public static class ReflectAffixPatch
    {
        private static readonly FieldInfo _velocityField = AccessTools.Field(typeof(Projectile), "velocity");
        private static readonly FieldInfo _traveledDistField = AccessTools.Field(typeof(Projectile), "traveledDistance");

        [HarmonyPatch("UpdateMoveAndCheck")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // 拦截 DamageReceiver.Hurt(DamageInfo) -> bool
            var hurtMethod = AccessTools.Method(typeof(DamageReceiver), nameof(DamageReceiver.Hurt));
            // 替换 ReflectAffixPatch.ReflectOrHurt(..., Projectile) -> bool
            var proxyMethod = AccessTools.Method(typeof(ReflectAffixPatch), nameof(ReflectOrHurt));

            foreach (var instruction in instructions)
            {
                if (instruction.Calls(hurtMethod))
                {
                    // 在调用 hurt 之前，把 Projectile 加载到堆栈上
                    // 原堆栈: [DamageReceiver] [DamageInfo]
                    // 新堆栈: [DamageReceiver] [DamageInfo] [Projectile]
                    yield return new CodeInstruction(OpCodes.Ldarg_0); 
                    yield return new CodeInstruction(OpCodes.Call, proxyMethod);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        // 返回bool
        public static bool ReflectOrHurt(DamageReceiver receiver, DamageInfo info, Projectile projectile)
        {
            var victim = receiver.GetComponentInParent<CharacterMainControl>();
            if (victim == null && receiver.health != null)
            {
                victim = receiver.health.GetComponent<CharacterMainControl>(); 
            }

            if (victim != null && ReflectBehavior.ActiveReflectors.Contains(victim.GetInstanceID()))
            {
                ref var ctx = ref projectile.context;

                ctx.team = victim.Team; 
                ctx.fromCharacter = victim;

                // 反向基础向量
                Vector3 baseReverseDir = -ctx.direction;

                // 随机偏转角度
                float deviationAngle = UnityEngine.Random.Range(-10f, 10f);
                Vector3 newDirection = Quaternion.Euler(0, deviationAngle, 0) * baseReverseDir;
                newDirection.y = 0; 
                newDirection.Normalize();

                ctx.direction = newDirection;
                projectile.transform.forward = newDirection; 
                
                if (_velocityField != null)
                {
                    _velocityField.SetValue(projectile, newDirection * ctx.speed);
                }

                // 重置命中判定
                projectile.damagedObjects.Clear();
                projectile.damagedObjects.Add(victim.gameObject); 

                _traveledDistField?.SetValue(projectile, 0f);

                ctx.penetrate += 1;
                return false;
            }

            return receiver.Hurt(info);
        }
    }
}