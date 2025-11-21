using System;
using System.Collections;
using System.Collections.Generic;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【鸡哥】词缀 - 出生时召唤 4 只小鸡护卫
    /// </summary>
    public class ChickenBroBehavior : AffixBehaviorBase
    {
        public override string AffixName => "ChickenBro";
        
        private static readonly string ChickenPresetName = "Cname_Chick";
        private static readonly int ChickenCount = 2;
        private static readonly float SpawnRadius = 2f;
        private static readonly float ChickenHealthRatio = 0.8f;
        private static readonly float ChickenDamageRatio = 0.8f;
        private static readonly float ChickenSpeedRatio = 1.2f;
        
        private readonly Lazy<string> _chickenCustomName =
            new(() => LocalizationManager.GetText("Affix_ChickenBro_Summon_Name")
            );

        private string ChickenCustomName => _chickenCustomName.Value;

        private CharacterMainControl _boss;
        private List<CharacterMainControl> _chickens = new List<CharacterMainControl>();
        private bool _chickensSpawned = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _boss = character;
            _chickensSpawned = false;
            
            ModBehaviour.Instance?.StartCoroutine(SpawnChickensDelayed());
        }

        private IEnumerator SpawnChickensDelayed()
        {
            yield return new WaitForSeconds(0.5f);

            if (_boss == null || _chickensSpawned)
            {
                yield break;
            }

            SpawnChickens();
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
                    presetName: ChickenPresetName,
                    position: spawnPosition,
                    spawner: _boss,
                    healthMultiplier: ChickenHealthRatio,
                    damageMultiplier: ChickenDamageRatio,
                    speedMultiplier: ChickenSpeedRatio,
                    scaleMultiplier: 1f,
                    affixes: null,
                    preventElite: true,
                    customDisplayName: ChickenCustomName,  // 自定义名称
                    onSpawned: (chicken) => {
                        if (chicken != null) _chickens.Add(chicken);
                    });
            }
        }


        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            _boss = null;
            _chickens.Clear();
            _chickensSpawned = false;
        }
    }
}
