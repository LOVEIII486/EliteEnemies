using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Duckov.Scenes;
using ItemStatsSystem;
using System.Collections.Generic;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【拟态】 - 垃圾诱饵版 (无清理逻辑)
    /// </summary>
    public class MimicBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Mimic";

        private InteractableLootbox _trapBox;
        private InteractableBase _trapInteractable;
        private AICharacterController _aiController; 
        private Collider _col;
        
        private bool _hasTriggered = false; 
        private readonly Vector3 _followOffset = Vector3.up * 0.5f; 
        
        private const int BaitItemID = 445; 
        private const int BaitItemCount = 10;
        private bool _isTriggering = false; 

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _hasTriggered = false;
            _isTriggering = false;
            _col = character.GetComponent<Collider>();
            _aiController = character.GetComponentInChildren<AICharacterController>();
            if (_aiController == null) _aiController = character.GetComponentInParent<AICharacterController>();

            SpawnTrapBox(character);
            ToggleAI(false);
            
            if (_col != null) _col.enabled = false;
            SyncPositionToBox(character);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_hasTriggered || character == null || character.characterModel == null) return;
            
            character.Hide();
            
            if (_trapBox != null)
            {
                Vector3 targetPos = _trapBox.transform.position + _followOffset;
                if (Vector3.SqrMagnitude(character.transform.position - targetPos) > 0.0025f)
                {
                    SyncPositionToBox(character);
                }
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (!_hasTriggered) TriggerAmbush(character);
        }

        private void SpawnTrapBox(CharacterMainControl owner)
        {
            if (owner.deadLootBoxPrefab == null) return;
            Vector3 spawnPos = owner.transform.position + Vector3.up * 1.5f;

            _trapBox = UnityEngine.Object.Instantiate(owner.deadLootBoxPrefab, spawnPos, owner.transform.rotation);
            if (_trapBox != null)
            {
                var rb = _trapBox.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false; 
                    rb.useGravity = true;   
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous; 
                    rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2f; 
                }

                if (_trapBox.interactCollider != null) _trapBox.interactCollider.isTrigger = false;
                else
                {
                    var col = _trapBox.GetComponent<Collider>();
                    if (col != null) col.isTrigger = false;
                }
                
                _trapBox.Inventory.SetCapacity(BaitItemCount+4);

                GenerateBaitItems();

                _trapInteractable = _trapBox.GetComponent<InteractableBase>();
                if (_trapInteractable != null)
                {
                    _trapInteractable.OnInteractStartEvent.AddListener((player, interactable) =>
                    {
                        OnPlayerOpenedBox(player, owner);
                    });
                }
                
                try { MultiSceneCore.MoveToActiveWithScene(_trapBox.gameObject, UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); } catch { }
            }
        }

        private void GenerateBaitItems()
        {
            if (_trapBox == null || _trapBox.Inventory == null) return;
            
            for (int i = 0; i < BaitItemCount; i++)
            {
                Item newItem = ItemAssetsCollection.InstantiateSync(BaitItemID);
                if (newItem != null)
                {
                    newItem.FromInfoKey = "MimicBait"; 
                    _trapBox.Inventory.AddItem(newItem);
                }
            }
        }

        private void ToggleAI(bool enable)
        {
            if (_aiController == null) return;

            if (enable)
            {
                _aiController.gameObject.SetActive(true);
                _aiController.enabled = true;
            }
            else
            {
                _aiController.StopMove();
                _aiController.alert = false;
                _aiController.searchedEnemy = null;
                _aiController.SetTarget(null);
                _aiController.PutBackWeapon();
                _aiController.gameObject.SetActive(false);
            }
        }
        
        private void SyncPositionToBox(CharacterMainControl character)
        {
            if (_trapBox != null)
            {
                character.transform.position = _trapBox.transform.position + _followOffset;
                character.transform.rotation = _trapBox.transform.rotation;
            }
        }

        private void OnPlayerOpenedBox(CharacterMainControl player, CharacterMainControl owner)
        {
            if (_hasTriggered || _isTriggering) return;
            _isTriggering = true;

            if (player.interactAction != null && player.interactAction.Running)
                player.interactAction.StopAction();

            if (owner != null && owner.gameObject.activeInHierarchy)
            {
                owner.StartCoroutine(DelayedAmbushRoutine(owner));
            }
            else
            {
                TriggerAmbush(owner);
            }
        }
        
        private IEnumerator DelayedAmbushRoutine(CharacterMainControl owner)
        {
            yield return new WaitForSeconds(1.5f);

            if (owner != null)
            {
                TriggerAmbush(owner);
            }
        }

        private void TriggerAmbush(CharacterMainControl character)
        {
            if (_hasTriggered) return;
            _hasTriggered = true;
            

            if (_trapBox != null)
            {
                UnityEngine.Object.Destroy(_trapBox.gameObject);
                _trapBox = null;
            }
            
            ToggleAI(true); 
            if (_col != null) _col.enabled = true;
            character.Show();
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            if (character != null) character.Show();
            if (_col != null) _col.enabled = true;
            
            ToggleAI(true);
            _hasTriggered = false;
            _isTriggering = false;
            
            if (_trapBox != null) UnityEngine.Object.Destroy(_trapBox.gameObject);
        }

        public void OnAttack(CharacterMainControl c, DamageInfo d) { }
        public override void OnHitPlayer(CharacterMainControl c, DamageInfo d) { }
        public override void OnEliteDeath(CharacterMainControl c, DamageInfo d) => OnCleanup(c);
    }
}