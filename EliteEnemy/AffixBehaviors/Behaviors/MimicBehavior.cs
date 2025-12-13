using System.Collections;
using System.Collections.Generic;
using System.Reflection; 
using UnityEngine;
using UnityEngine.AI;
using Duckov.Scenes;
using ItemStatsSystem;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【拟态】 - 静音版
    /// </summary>
    public class MimicBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Mimic";

        private InteractableLootbox _trapBox;
        private InteractableBase _trapInteractable;
        private AICharacterController _aiController;
        private Collider _col;
        
        private NavMeshAgent _navAgent;
        private Animator _animator;
        private Rigidbody _rb;
        private Movement _movement;
        private CharacterSoundMaker _soundMaker;

        private List<Renderer> _cachedRenderers;
        private static FieldInfo _renderersField;

        private bool _hasTriggered = false;
        private bool _isTriggering = false;
        private readonly Vector3 _followOffset = Vector3.up * 0.5f;

        private const int BaitItemID = 445; 
        private const int BaitItemCount = 10;
        
        private const float SyncThresholdSqr = 1.0f; 

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _hasTriggered = false;
            _isTriggering = false;
    
            _col = character.GetComponent<Collider>();
            _rb = character.GetComponent<Rigidbody>();
            _animator = character.GetComponentInChildren<Animator>();
            _navAgent = character.GetComponent<NavMeshAgent>();
            _aiController = character.GetComponentInChildren<AICharacterController>();
            if (_aiController == null) _aiController = character.GetComponentInParent<AICharacterController>();

            _movement = character.GetComponent<Movement>();
            _soundMaker = character.GetComponent<CharacterSoundMaker>();

            //初始化渲染器反射
            InitRendererCache(character);

            // 生成陷阱
            SpawnTrapBox(character);
            
            ToggleAI(false);
            if (_col != null) _col.enabled = false;
            
            SyncPositionToBox(character);
            ForceHideVisuals(); 
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_hasTriggered || character == null || character.characterModel == null) return;
            
            character.Hide(); // 必须hide防止血条显现
            ForceHideVisuals(); // 有时候hide无法关闭模型，不知道为啥
            
            // 1. 导航
            if (_navAgent != null && _navAgent.enabled) _navAgent.enabled = false;
            if (_animator != null && _animator.enabled) _animator.enabled = false;
            
            // 2. 静音
            if (_soundMaker != null && _soundMaker.enabled) _soundMaker.enabled = false;
            if (_movement != null && _movement.enabled) _movement.enabled = false;
            
            // 位置同步逻辑优化
            if (_trapBox != null)
            {
                Vector3 targetPos = _trapBox.transform.position + _followOffset;

                if (Vector3.SqrMagnitude(character.transform.position - targetPos) > SyncThresholdSqr)
                {
                    character.transform.position = targetPos;
                    character.transform.rotation = _trapBox.transform.rotation;
                    if (_rb != null && !_rb.isKinematic) 
                    {
                        _rb.velocity = Vector3.zero;
                        _rb.angularVelocity = Vector3.zero;
                    }
                }
            }
        }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (!_hasTriggered) TriggerAmbush(character);
        }

        private void TriggerAmbush(CharacterMainControl character)
        {
            if (_hasTriggered) return;
            
            // 触发埋伏瞬间，强制将敌人拉回箱子位置
            // if (_trapBox != null)
            // {
            //     character.transform.position = _trapBox.transform.position + _followOffset;
            //     character.transform.rotation = _trapBox.transform.rotation;
            // }

            _hasTriggered = true;

            if (_trapBox != null)
            {
                UnityEngine.Object.Destroy(_trapBox.gameObject);
                _trapBox = null;
            }
            
            // 1. 恢复所有能力 (AI、移动、声音)
            ToggleAI(true); 
            if (_col != null) _col.enabled = true;
            
            // 2. 强制显形
            ForceShowVisuals(); 
            character.Show();
        }

        private void ToggleAI(bool enable)
        {
            // 1. 声音与移动控制 (强类型操作)
            if (_soundMaker != null) _soundMaker.enabled = enable;
            if (_movement != null)
            {
                _movement.enabled = enable;
                // 如果是禁用移动，顺便把残余速度清零
                if (!enable && _movement.characterController != null) 
                {
                    // _movement.Velocity 是只读的，通常通过刚体控制
                }
            }

            // 2. 导航代理
            if (_navAgent != null)
            {
                if (!enable)
                {
                    if (_navAgent.isOnNavMesh) _navAgent.isStopped = true;
                    _navAgent.enabled = false;
                }
                else
                {
                    _navAgent.enabled = true;
                    if (_navAgent.isOnNavMesh) _navAgent.isStopped = false;
                }
            }

            // 3. 刚体
            if (_rb != null)
            {
                _rb.isKinematic = !enable; 
                if (!enable) _rb.velocity = Vector3.zero;
            }

            // 4. 动画
            if (_animator != null) _animator.enabled = enable;

            // 5. AI
            if (_aiController != null)
            {
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
        }

        #region 辅助函数

        private void ForceShowVisuals()
        {
            if (_cachedRenderers == null) return;
            for (int i = 0; i < _cachedRenderers.Count; i++)
            {
                if (_cachedRenderers[i] != null) _cachedRenderers[i].enabled = true;
            }
        }

        private void ForceHideVisuals()
        {
            if (_cachedRenderers == null) return;
            for (int i = 0; i < _cachedRenderers.Count; i++)
            {
                if (_cachedRenderers[i] != null && _cachedRenderers[i].enabled) _cachedRenderers[i].enabled = false;
            }
        }

        private void InitRendererCache(CharacterMainControl character)
        {
            if (character.characterModel == null) return;
            try
            {
                if (_renderersField == null)
                    _renderersField = typeof(CharacterModel).GetField("renderers", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_renderersField != null)
                    _cachedRenderers = _renderersField.GetValue(character.characterModel) as List<Renderer>;
            }
            catch { _cachedRenderers = null; }
        }

        private void SpawnTrapBox(CharacterMainControl owner)
        {
            if (owner.deadLootBoxPrefab == null) return;
            Vector3 spawnPos = owner.transform.position + Vector3.up * 1.5f;

            _trapBox = UnityEngine.Object.Instantiate(owner.deadLootBoxPrefab, spawnPos, owner.transform.rotation);
            if (_trapBox != null)
            {
                var boxRb = _trapBox.GetComponent<Rigidbody>();
                if (boxRb != null)
                {
                    boxRb.isKinematic = false; 
                    boxRb.useGravity = true;   
                    boxRb.collisionDetectionMode = CollisionDetectionMode.Continuous; 
                    boxRb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2f; 
                }

                if (_trapBox.interactCollider != null) _trapBox.interactCollider.isTrigger = false;
                else
                {
                    var col = _trapBox.GetComponent<Collider>();
                    if (col != null) col.isTrigger = false;
                }
                
                _trapBox.Inventory.SetCapacity(BaitItemCount + 4);
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
            if (player.interactAction != null && player.interactAction.Running) player.interactAction.StopAction();
            if (owner != null && owner.gameObject.activeInHierarchy) owner.StartCoroutine(DelayedAmbushRoutine(owner));
            else TriggerAmbush(owner);
        }
        
        private IEnumerator DelayedAmbushRoutine(CharacterMainControl owner)
        {
            yield return new WaitForSeconds(1.5f);
            if (owner != null) TriggerAmbush(owner);
        }

        #endregion

        public override void OnCleanup(CharacterMainControl character)
        {
            ForceShowVisuals();
            if (character != null) character.Show();
            if (_col != null) _col.enabled = true;
            ToggleAI(true);
            _hasTriggered = false;
            _isTriggering = false;
            _cachedRenderers = null; 
            if (_trapBox != null) UnityEngine.Object.Destroy(_trapBox.gameObject);
        }

        public void OnAttack(CharacterMainControl c, DamageInfo d) { }
        public override void OnHitPlayer(CharacterMainControl c, DamageInfo d) { }
        public override void OnEliteDeath(CharacterMainControl c, DamageInfo d) => OnCleanup(c);
    }
}