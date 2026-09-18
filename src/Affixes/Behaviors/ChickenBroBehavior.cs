using System;
using System.Collections;
using System.Collections.Generic;
using EliteEnemies.Core;
using EliteEnemies.Localization;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【鸡哥】词缀 - 出生时召唤 2 只小鸡护卫
    /// </summary>
    public class ChickenBroBehavior : AffixBehaviorBase
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

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _boss = character;
            _chickensSpawned = false;
            
            // 用框架托管的协程（原先挂全局单例 ModBehaviour）：
            // 宿主变成精英自己的行为组件 ⇒ 主人先死时协程会随之停止，不会从尸体上再刷出一窝小鸡。
            StartManagedCoroutine(SpawnChickensDelayed());
        }

        private IEnumerator SpawnChickensDelayed()
        {
            yield return new WaitForSeconds(0.5f);

            if (_boss == null || _chickensSpawned)
            {
                yield break;
            }

            SpawnChickens();
            _chickensSpawned = true; // 标记已生成
        }

        private void SpawnChickens()
        {
            var helper = EggSpawnHelper.Instance;
            if (helper == null || !helper.IsReady) return;

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
        }
    }
}