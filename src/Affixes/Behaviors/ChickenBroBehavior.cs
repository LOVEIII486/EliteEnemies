using System;
using System.Collections.Generic;
using EliteEnemies.Core;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【鸡哥】词缀 - 出生时召唤 2 只小鸡护卫
    /// </summary>
    public class ChickenBroBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "ChickenBro";
        
        // 原本是 nameKey "Cname_Chick"，现对应资源名为 "SpawnPreset_Animal_Jinitaimei"
        private static readonly string ChickenPresetName = "SpawnPreset_Animal_Jinitaimei";
        
        private static readonly int ChickenCount = 2;
        private static readonly float SpawnRadius = 2f;
        private static readonly float ChickenHealthRatio = 0.8f;
        private static readonly float ChickenDamageRatio = 0.8f;
        private static readonly float ChickenSpeedRatio = 1.2f;
        
        private string ChickenCustomName =>
            LocalizationManager.GetText("EliteEnemies_Affix_ChickenBro_Summon_Name");

        private CharacterMainControl _boss;
        private List<CharacterMainControl> _chickens = new List<CharacterMainControl>();
        private bool _chickensSpawned = false;

        /// <summary>出生后延迟这么久再召唤（原来是一个 <c>WaitForSeconds(0.5f)</c> 的协程）。</summary>
        private const float SpawnDelay = 0.5f;

        private float _spawnDueTime = -1f;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _boss = character;
            _chickensSpawned = false;

            // ⚠️ **刻意不用协程**（与拟态同一条教训，别再改回去）。
            //
            //   本工程一共两处用框架托管协程：一处是拟态的开箱伏击，实机日志显示
            //   "协程体确实跑了、宿主 active 且 enabled、没有任何异常，但它**再也不恢复**"；
            //   另一处就是这里。而第三处（时停）在联机下正好被黑名单禁掉
            //   ⇒ **这条模式在本工程没有任何一个"在跑的"成功例子**。
            //
            //   改成"记一个到点时间戳、在 OnUpdate 里比较"，整类问题直接消失；
            //   宿主生命周期仍然绑在行为组件上（角色一销毁就不再调 OnUpdate），
            //   所以"主人先死不会再从尸体上刷出一窝小鸡"这条原语义不变。
            _spawnDueTime = Time.time + SpawnDelay;
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_chickensSpawned || _spawnDueTime < 0f) return;
            if (Time.time < _spawnDueTime) return;

            _spawnDueTime = -1f;
            SpawnChickens();
            _chickensSpawned = true; // 标记已生成（无论成功与否，与原协程版一致：不重试）
        }

        private void SpawnChickens()
        {
            var helper = EggSpawnHelper.Instance;
            if (helper == null || !helper.IsReady)
            {
                // ⚠ 原先这里是**静默 return**——症状就是"鸡哥不召唤，日志里一个字都没有"，
                //   排查时无从下手。生成助手没就绪是**异常情况**（它在关卡初始化时就该就绪），
                //   所以这里按错误报出来，而不是吞掉。
                Debug.LogError($"[EliteEnemies.ChickenBro] 召唤小鸡失败：生成助手未就绪" +
                               $"（Instance={(helper != null)}，IsReady={(helper != null && helper.IsReady)}）" +
                               "——本次不再重试");
                return;
            }

            Vector3 bossPosition = _boss.transform.position;
    
            for (int i = 0; i < ChickenCount; i++)
            {
                float angle = (360f / ChickenCount) * i;
                Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * SpawnRadius;
                Vector3 spawnPosition = bossPosition + offset;
                spawnPosition.y = bossPosition.y;
                
                helper.SpawnByPresetName(
                    resourceName: ChickenPresetName,
                    position: spawnPosition,
                    spawner: _boss,
                    healthMultiplier: ChickenHealthRatio,
                    damageMultiplier: ChickenDamageRatio,
                    speedMultiplier: ChickenSpeedRatio,
                    scaleMultiplier: 1f,
                    affixes: null,
                    preventElite: true,
                    customKeySuffix: "EE_Chick_NonElite",
                    customDisplayName: ChickenCustomName,
                    onSpawned: (chicken) => {
                        if (chicken != null) _chickens.Add(chicken);
                    });
            }
        }



        public override void OnCleanup(CharacterMainControl character)
        {
            _boss = null;
            _chickens.Clear();
            _chickensSpawned = false;
            _spawnDueTime = -1f;
        }
    }
}