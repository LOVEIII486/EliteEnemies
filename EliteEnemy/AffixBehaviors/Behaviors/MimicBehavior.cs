using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using ItemStatsSystem;
using NodeCanvas.Framework;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 【拟态】
    /// </summary>
    public class MimicBehavior : AffixBehaviorBase, IUpdateableAffixBehavior, ICombatAffixBehavior
    {
        public override string AffixName => "Mimic";

        private InteractableLootbox _trapBox;
        private AICharacterController _aiController;
        private CharacterSoundMaker _soundMaker;
        private NavMeshAgent _navAgent;
        private GraphOwner _brain;

        private List<Renderer> _cachedRenderers;
        private static FieldInfo _renderersField;

        private bool _hasTriggered = false;
        private bool _isTriggering = false;

        private readonly Vector3 _followOffset = Vector3.up * 0.15f;

        private const int BaitItemID = 445;
        private const int BaitItemCount = 10;
        private const float SyncThresholdSqr = 0.001f;

        private float _cachedSightDist, _cachedHearing, _cachedSightAngle, _cachedTraceDist;
        private bool _isSensorySuppressed = false;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _hasTriggered = false;
            _isTriggering = false;

            _aiController = character.GetComponentInChildren<AICharacterController>();
            if (_aiController == null) _aiController = character.GetComponentInParent<AICharacterController>();
            _soundMaker = character.GetComponent<CharacterSoundMaker>();
            _navAgent = character.GetComponent<NavMeshAgent>();

            if (_aiController != null)
            {
                _brain = _aiController.GetComponent<GraphOwner>();
                if (_brain == null) _brain = _aiController.GetComponentInParent<GraphOwner>();
            }

            InitRendererCache(character);

            SpawnBoxByLiftingEnemy(character);

            SetMimicState(character, true);

            ForceHideVisuals();
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_hasTriggered || character == null) return;

            character.Hide();
            ForceHideVisuals();

            // 持续压制 AI
            if (_aiController != null) SuppressAIHard(_aiController);
            if (_soundMaker != null && _soundMaker.enabled) _soundMaker.enabled = false;

            if (_trapBox != null)
            {
                Vector3 targetPos = _trapBox.transform.position + _followOffset;
                if (Vector3.SqrMagnitude(character.transform.position - targetPos) > SyncThresholdSqr)
                {
                    character.transform.position = targetPos;
                    character.transform.rotation = _trapBox.transform.rotation;
                }
            }
        }

        /// <summary>
        /// 被攻击时触发埋伏
        /// </summary>
        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (_hasTriggered) return;
    
            // 被打激活时，如果是被其他单位攻击，将攻击者设为突袭目标
            CharacterMainControl attacker = damageInfo.fromCharacter;
            TriggerAmbush(character, attacker);
        }

        private void SpawnBoxByLiftingEnemy(CharacterMainControl character)
        {
            if (character == null || character.deadLootBoxPrefab == null) return;

            Vector3 originalFloorPos = character.transform.position;
            character.transform.position += Vector3.up * 5.0f;

            _trapBox = UnityEngine.Object.Instantiate(character.deadLootBoxPrefab, originalFloorPos,
                character.transform.rotation);

            if (_trapBox != null)
            {
                Rigidbody boxRb = _trapBox.GetComponent<Rigidbody>();
                if (boxRb != null)
                {
                    boxRb.isKinematic = false;
                    boxRb.useGravity = true;
                    boxRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    boxRb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2f;
                    boxRb.WakeUp();
                }

                // 确保箱子不是触发器，以便它能撞击地面
                if (_trapBox.interactCollider != null) _trapBox.interactCollider.isTrigger = false;
                else
                {
                    var col = _trapBox.GetComponent<Collider>();
                    if (col != null) col.isTrigger = false;
                }

                _trapBox.Inventory.SetCapacity(BaitItemCount + 4);
                for (int i = 0; i < BaitItemCount; i++)
                {
                    Item newItem = ItemAssetsCollection.InstantiateSync(BaitItemID);
                    if (newItem != null) _trapBox.Inventory.AddItem(newItem);
                }

                // 绑定交互
                if (_trapBox.GetComponent<InteractableBase>() is var interactable && interactable != null)
                {
                    interactable.OnInteractStartEvent.AddListener((player, _) => OnPlayerOpenedBox(player, character));
                }

                // 移至当前活动场景
                try
                {
                    Duckov.Scenes.MultiSceneCore.MoveToActiveWithScene(_trapBox.gameObject,
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                }
                catch
                {
                }
            }
        }

        private void SuppressAIHard(AICharacterController ai)
        {
            ai.sightDistance = 0f;
            ai.sightAngle = 0f;
            ai.hearingAbility = 0f;
            ai.forceTracePlayerDistance = 0f;

            if (ai.searchedEnemy != null || ai.aimTarget != null)
            {
                ai.SetTarget(null);
                ai.searchedEnemy = null;
                ai.alert = false;
                ai.StopMove();
            }
        }

        private void TriggerAmbush(CharacterMainControl character, CharacterMainControl initialTarget = null)
        {
            if (_hasTriggered) return;
            _hasTriggered = true;

            if (_trapBox != null)
            {
                UnityEngine.Object.Destroy(_trapBox.gameObject);
                _trapBox = null;
            }
    
            SetMimicState(character, false);
            ForceShowVisuals(); 
            character.Show();

            if (initialTarget != null && _aiController != null)
            {
                FaceTarget(character, initialTarget);
                _aiController.SetTarget(initialTarget.transform);
                _aiController.searchedEnemy = initialTarget.mainDamageReceiver;
                _aiController.alert = true;
        
            }
        }

        private void SetMimicState(CharacterMainControl character, bool isMimic)
        {
            if (_aiController == null) return;

            if (isMimic)
            {
                // 暂停大脑逻辑
                if (_brain != null && _brain.isRunning) _brain.PauseBehaviour();
                SetSensorySuppression(_aiController, true);
                if (_soundMaker != null) _soundMaker.enabled = false;

                // 停止寻路系统，防止其在被强刷坐标时尝试回正位置导致抖动
                if (_navAgent != null && _navAgent.enabled) _navAgent.isStopped = true;
            }
            else
            {
                SetSensorySuppression(_aiController, false);
                if (_brain != null && _brain.isPaused) _brain.StartBehaviour();
                if (_soundMaker != null) _soundMaker.enabled = true;
                if (_navAgent != null && _navAgent.enabled) _navAgent.isStopped = false;
            }
        }

        private void SetSensorySuppression(AICharacterController ai, bool shouldSuppress)
        {
            if (ai == null) return;
            if (shouldSuppress)
            {
                if (_isSensorySuppressed) return;
                _cachedSightDist = ai.sightDistance;
                _cachedHearing = ai.hearingAbility;
                _cachedSightAngle = ai.sightAngle;
                _cachedTraceDist = ai.forceTracePlayerDistance;

                SuppressAIHard(ai);
                ai.PutBackWeapon();
                _isSensorySuppressed = true;
            }
            else
            {
                if (!_isSensorySuppressed) return;
                ai.sightDistance = _cachedSightDist;
                ai.sightAngle = _cachedSightAngle;
                ai.hearingAbility = _cachedHearing;
                ai.forceTracePlayerDistance = _cachedTraceDist;
                _isSensorySuppressed = false;
            }
        }

        #region 基础辅助逻辑

        private void InitRendererCache(CharacterMainControl character)
        {
            if (character.characterModel == null) return;
            try
            {
                if (_renderersField == null)
                    _renderersField =
                        typeof(CharacterModel).GetField("renderers", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_renderersField != null)
                    _cachedRenderers = _renderersField.GetValue(character.characterModel) as List<Renderer>;
            }
            catch
            {
                _cachedRenderers = null;
            }
        }

        private void ForceShowVisuals()
        {
            if (_cachedRenderers == null) return;
            foreach (var r in _cachedRenderers)
                if (r != null)
                    r.enabled = true;
        }

        private void ForceHideVisuals()
        {
            if (_cachedRenderers == null) return;
            foreach (var r in _cachedRenderers)
                if (r != null)
                    r.enabled = false;
        }

        private void OnPlayerOpenedBox(CharacterMainControl player, CharacterMainControl owner)
        {
            if (_hasTriggered || _isTriggering) return;
            _isTriggering = true;
    
            if (player.interactAction != null && player.interactAction.Running) 
                player.interactAction.StopAction();
        
            // 延迟突袭：将玩家传入作为初始目标
            owner.StartCoroutine(DelayedAmbushRoutine(owner, player));
        }

        private IEnumerator DelayedAmbushRoutine(CharacterMainControl owner, CharacterMainControl target)
        {
            yield return new WaitForSeconds(1.2f);
            if (owner != null) TriggerAmbush(owner, target);
        }
        
        private void FaceTarget(CharacterMainControl character, CharacterMainControl target)
        {
            if (character == null || target == null) return;
    
            Vector3 direction = (target.transform.position - character.transform.position);
            direction.y = 0f;
    
            if (direction.sqrMagnitude > 0.001f)
            {
                character.transform.rotation = Quaternion.LookRotation(direction);
            }
        }
        
        public override void OnCleanup(CharacterMainControl character)
        {
            SetMimicState(character, false);
            ForceShowVisuals();
            if (character != null) character.Show();
            _hasTriggered = false;
            _isTriggering = false;
            if (_trapBox != null) UnityEngine.Object.Destroy(_trapBox.gameObject);
        }

        public void OnAttack(CharacterMainControl c, DamageInfo d)
        {
        }

        public override void OnHitPlayer(CharacterMainControl c, DamageInfo d)
        {
        }

        public override void OnEliteDeath(CharacterMainControl c, DamageInfo d) => OnCleanup(c);

        #endregion
    }
}