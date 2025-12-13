using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors;
using HarmonyLib;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.BehaviorPatches
{
    [HarmonyPatch(typeof(Projectile))]
    public static class ReflectAffixPatch
    {
        // -------------------------------------------------------------------------
        // Transpiler: 拦截 UpdateMoveAndCheck 方法中的 Hurt 调用
        // -------------------------------------------------------------------------
        [HarmonyPatch("UpdateMoveAndCheck")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var hurtMethod = AccessTools.Method(typeof(DamageReceiver), nameof(DamageReceiver.Hurt));
            var proxyMethod = AccessTools.Method(typeof(ReflectAffixPatch), nameof(ReflectOrHurt));

            foreach (var instruction in instructions)
            {
                if (instruction.Calls(hurtMethod))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load this (Projectile)
                    yield return new CodeInstruction(OpCodes.Call, proxyMethod); // Call static proxy
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        // -------------------------------------------------------------------------
        // 代理方法: 决定是反射还是造成伤害
        // -------------------------------------------------------------------------
        public static void ReflectOrHurt(DamageReceiver receiver, DamageInfo info, Projectile projectile)
        {
            if (receiver == null || receiver.health == null) 
            {
                return;
            }

            // --- 精英怪词条检测 ---
            // 确保你已经更新了 EliteBehaviorComponent 包含了 HasBehavior 方法
            var behaviorComponent = receiver.GetComponent<EliteBehaviorComponent>();
            
            // 注意：这里使用的是泛型 HasBehavior<ReflectBehavior>()
            // 请确保你的反射词条脚本类名确实是 ReflectBehavior
            bool hasReflect = behaviorComponent != null && behaviorComponent.HasBehavior<ReflectBehavior>();

            if (hasReflect)
            {
                DoReflection(projectile, receiver, info);
            }
            else
            {
                receiver.Hurt(info);
            }
        }

        // -------------------------------------------------------------------------
        // 反射的具体实现逻辑
        // -------------------------------------------------------------------------
        private static void DoReflection(Projectile projectile, DamageReceiver reflector, DamageInfo originalHitInfo)
        {
            // 1. 获取反射法线
            Vector3 normal = originalHitInfo.damageNormal;
            if (normal == Vector3.zero) normal = -projectile.transform.forward;

            // 2. 物理反射计算
            FieldInfo velocityField = AccessTools.Field(typeof(Projectile), "velocity");
            Vector3 currentVelocity = (Vector3)velocityField.GetValue(projectile);

            Vector3 reflectedVelocity = Vector3.Reflect(currentVelocity, normal);
            reflectedVelocity *= 1.2f; // 反弹加速

            velocityField.SetValue(projectile, reflectedVelocity);
            
            FieldInfo directionField = AccessTools.Field(typeof(Projectile), "direction");
            directionField.SetValue(projectile, reflectedVelocity.normalized);

            projectile.transform.forward = reflectedVelocity.normalized;

            // 3. 修改阵营 (Context)
            // ProjectileContext 是结构体，直接修改字段需要重新赋值回去，或者如果 context 是 field 的话可以直接修改
            // 但 Projectile 中 context 是 public field，所以可以直接修改
            
            // 修复：ProjectileContext 是 struct，不能与 null 比较
            // 修复：Teams 枚举通常是小写 player, badSide
            projectile.context.team = (projectile.context.team == Teams.player) ? Teams.all : Teams.player;
            
            projectile.context.fromCharacter = reflector.health.TryGetCharacter();
            
            FieldInfo traveledDistField = AccessTools.Field(typeof(Projectile), "traveledDistance");
            traveledDistField.SetValue(projectile, 0f); 
            
            // 增加穿透，抵消原版代码的递减
            projectile.context.penetrate += 1; 

            // 4. 防止立即再次击中反射者
            if (projectile.damagedObjects != null)
            {
                if (!projectile.damagedObjects.Contains(reflector.gameObject))
                {
                    projectile.damagedObjects.Add(reflector.gameObject);
                }
                // 清空已攻击列表，确保反弹回去能打中玩家
                projectile.damagedObjects.Clear();
                projectile.damagedObjects.Add(reflector.gameObject);
            }
        }
    }
}