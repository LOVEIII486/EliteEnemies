using System;
using System.Reflection;
using ECM2;
using UnityEngine;
using ItemStatsSystem;
using ItemStatsSystem.Stats;
using EliteEnemies.AffixBehaviors;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【史莱姆】词缀 - 初始巨大但虚弱,血量降低时体型缩小且伤害增强,并会周期性跳跃
    /// </summary>
    public class SlimeBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Slime";

        // 体型和属性
        private static readonly float InitialScale = 3f;          // 初始体型倍率
        private static readonly float MinScale = 0.5f;            // 最小体型倍率
        private static readonly float InitialHealthMult = 3f;    // 初始血量倍率
        private static readonly float InitialDamageMult = 0.4f;  // 初始伤害倍率
        private static readonly float MaxDamageMult = 1.2f;      // 最大伤害倍率
        private static readonly float HealthThreshold = 0.05f;    // 5%血量变化才更新

        // 跳跃
        private static readonly float JumpIntervalMin = 0.5f;      // 最小跳跃间隔
        private static readonly float JumpIntervalMax = 1.5f;      // 最大跳跃间隔
        private static readonly float JumpForce = 5f;             // 跳跃力度
        private static readonly float GroundPause = 0.3f;         // 地面约束暂停时间
        private static readonly float MaxJumpHeightCheck = 5f;  // 跳跃前检测上方空间
        
        private Vector3 _lastValidPosition;  // 记录最后一个有效位置
        private Vector3 _lastGroundedPosition;
        private bool _hasGroundedPosition;
        
        private CharacterMainControl _character;
        private Health _health;
        private CharacterMovement _movement;
        private Vector3 _originalScale;
        
        private float _lastHealthPercent;
        private float _nextJumpTime;
        private float _currentDamageMultiplier;
        
        // 缓存反射相关对象
        private object _itemCache;
        private System.Reflection.MethodInfo _getStatMethod;
        private object _gunDamageStat;
        private object _meleeDamageStat;
        private System.Reflection.MethodInfo _removeModifierMethod;
        private System.Reflection.MethodInfo _addModifierMethod;
        
        private Modifier _currentGunModifier;
        private Modifier _currentMeleeModifier;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _character = character;
            _health = character.Health;
            
            if (_health == null)
            {
                Debug.LogError("[SlimeBehavior] 找不到Health组件");
                return;
            }

            // 获取CharacterMovement组件(用于跳跃)
            if (character.movementControl != null)
            {
                _movement = character.movementControl.GetComponent<CharacterMovement>();
            }

            // 保存原始缩放
            _originalScale = character.transform.localScale;
            
            // 应用初始体型
            character.transform.localScale = _originalScale * InitialScale;

            // 应用属性修改（血量和初始伤害）
            ApplyInitialStats(character);

            // 初始化状态
            _lastHealthPercent = 1f;
            _currentDamageMultiplier = InitialDamageMult;
            _nextJumpTime = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
            
            _lastGroundedPosition = character.transform.position;
            _hasGroundedPosition = true;
            //Debug.Log($"[SlimeBehavior] {character.name} 史莱姆初始化完成 - 体型:{INITIAL_SCALE}x, 伤害:{INITIAL_DAMAGE_MULT}x");
        }

        private void ApplyInitialStats(CharacterMainControl character)
        {
            try
            {
                if (_health == null) return;

                // 通过反射获取item并缓存
                var itemField = typeof(Health).GetField("item",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    
                _itemCache = itemField?.GetValue(_health);
                if (_itemCache == null)
                {
                    Debug.LogWarning($"[SlimeBehavior] {character.name} 没有有效的item");
                    return;
                }

                _getStatMethod = _itemCache.GetType().GetMethod("GetStat", new[] { typeof(string) });
                if (_getStatMethod == null)
                {
                    Debug.LogError("[SlimeBehavior] 找不到GetStat方法");
                    return;
                }

                // 获取Stat对象并缓存反射方法
                _gunDamageStat = _getStatMethod.Invoke(_itemCache, new object[] { "GunDamageMultiplier" });
                _meleeDamageStat = _getStatMethod.Invoke(_itemCache, new object[] { "MeleeDamageMultiplier" });
                
                if (_gunDamageStat != null)
                {
                    var statType = _gunDamageStat.GetType();
                    _removeModifierMethod = statType.GetMethod("RemoveModifier", new[] { typeof(Modifier) });
                    _addModifierMethod = statType.GetMethod("AddModifier", new[] { typeof(Modifier) });
                }

                // 添加血量倍率（这个只需要加一次）
                AddModifierToStat("MaxHealth", InitialHealthMult);

                // 添加初始伤害倍率
                UpdateDamageModifiers(InitialDamageMult);
                
                // 恢复满血
                _health.SetHealth(_health.MaxHealth);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlimeBehavior] 应用初始属性失败: {ex.Message}");
            }
        }

        private void AddModifierToStat(string statName, float multiplier)
        {
            try
            {
                var statObj = _getStatMethod.Invoke(_itemCache, new object[] { statName });
                if (statObj == null) return;

                var addMod = statObj.GetType().GetMethod("AddModifier", new[] { typeof(Modifier) });
                if (addMod == null) return;

                float delta = multiplier - 1f;
                if (Mathf.Approximately(delta, 0f)) return;

                addMod.Invoke(statObj, new object[] { 
                    new Modifier(ModifierType.PercentageMultiply, delta, _character) 
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlimeBehavior] 添加Modifier失败({statName}): {ex.Message}");
            }
        }

        private void UpdateDamageModifiers(float newMultiplier)
        {
            try
            {
                float delta = newMultiplier - 1f;

                // 移除旧的Modifier并添加新的（枪械伤害）
                if (_gunDamageStat != null && _addModifierMethod != null && _removeModifierMethod != null)
                {
                    if (_currentGunModifier != null)
                    {
                        _removeModifierMethod.Invoke(_gunDamageStat, new object[] { _currentGunModifier });
                    }
                    
                    _currentGunModifier = new Modifier(ModifierType.PercentageMultiply, delta, _character);
                    _addModifierMethod.Invoke(_gunDamageStat, new object[] { _currentGunModifier });
                }

                // 移除旧的Modifier并添加新的（近战伤害）
                if (_meleeDamageStat != null && _addModifierMethod != null && _removeModifierMethod != null)
                {
                    if (_currentMeleeModifier != null)
                    {
                        _removeModifierMethod.Invoke(_meleeDamageStat, new object[] { _currentMeleeModifier });
                    }
                    
                    _currentMeleeModifier = new Modifier(ModifierType.PercentageMultiply, delta, _character);
                    _addModifierMethod.Invoke(_meleeDamageStat, new object[] { _currentMeleeModifier });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlimeBehavior] 更新伤害Modifier失败: {ex.Message}");
            }
        }
        
        // 在 IsGrounded() 后面添加新方法
        private bool CanSafelyJump()
        {
            if (_movement == null || _character == null) return false;
    
            // 检查是否在地面上
            if (!IsGrounded()) return false;
    
            // ⭐ 检查上方是否有足够空间（避免卡天花板）
            Vector3 rayStart = _character.transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(rayStart, Vector3.up, MaxJumpHeightCheck, LayerMask.GetMask("Default", "Terrain")))
            {
                // 上方有障碍物，不跳跃
                return false;
            }
    
            // ⭐ 检查附近是否有墙壁（避免跳出地图）
            float checkRadius = 2f;
            Collider[] nearbyColliders = Physics.OverlapSphere(_character.transform.position, checkRadius, LayerMask.GetMask("Default", "Terrain"));
    
            // 如果周围障碍物太多，可能在狭窄空间，不跳跃
            if (nearbyColliders.Length > 5)
            {
                return false;
            }
    
            return true;
        }

        // 5. 在Update中添加边界检查
        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_health == null || _health.IsDead || character == null) return;

            // 边界检查
            Vector3 currentPos = character.transform.position;
            if (currentPos.y < -50f || currentPos.y > 100f)
            {
                Debug.LogWarning($"[SlimeBehavior] {character.name} 超出边界，传送回安全位置");
                character.movementControl.ForceSetPosition(_lastValidPosition);
                return;
            }
    
            // 更新有效位置
            if (IsGrounded())
            {
                _lastValidPosition = currentPos;
            }

            // 血量和体型更新
            float currentHealthPercent = _health.CurrentHealth / _health.MaxHealth;
            if (Mathf.Abs(currentHealthPercent - _lastHealthPercent) >= HealthThreshold)
            {
                UpdateScaleAndDamage(character, currentHealthPercent);
                _lastHealthPercent = currentHealthPercent;
            }

            // 跳跃
            if (Time.time >= _nextJumpTime && CanSafelyJump())
            {
                _lastGroundedPosition = character.transform.position;
                _hasGroundedPosition = true;
                PerformJump();
                _nextJumpTime = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
            }
        }
        
        /// <summary>
        /// 检测是否超出地图边界
        /// </summary>
        private bool IsOutOfBounds(Vector3 position)
        {
            // 检查Y轴是否异常（掉落或飞太高）
            if (position.y < -50f || position.y > 100f)
                return true;
    
            // 检查是否离最后有效位置太远（可能穿墙了）
            float maxDistance = 20f;
            if (Vector3.Distance(position, _lastValidPosition) > maxDistance)
                return true;
    
            return false;
        }

        private void UpdateScaleAndDamage(CharacterMainControl character, float healthPercent)
        {
            // 血量从100%到0%: 体型从3倍到0.5倍, 伤害从0.2倍到1.5倍
            float t = 1f - healthPercent;  // 血量越低t越大(0→1)
            
            float newScale = Mathf.Lerp(InitialScale, MinScale, t);
            _currentDamageMultiplier = Mathf.Lerp(InitialDamageMult, MaxDamageMult, t);

            // 应用新体型
            character.transform.localScale = _originalScale * newScale;

            // 动态更新伤害倍率
            UpdateDamageModifiers(_currentDamageMultiplier);

            // Debug.Log($"[SlimeBehavior] {character.name} 血量:{healthPercent:P0}, 体型:{newScale:F2}x, 伤害:{_currentDamageMultiplier:F2}x");
        }

        private bool IsGrounded()
        {
            if (_movement == null) return false;
            
            // 检查垂直速度是否接近0(表示在地面)
            return Mathf.Abs(_movement.velocity.y) < 0.1f;
        }

        private void PerformJump()
        {
            if (_movement == null || _character == null) return;

            try
            {
                // 暂停地面约束
                _movement.PauseGroundConstraint(GroundPause);

                // 设置向上的速度
                Vector3 velocity = _movement.velocity;
                velocity.y = JumpForce;
                _movement.velocity = velocity;

                //Debug.Log($"[SlimeBehavior] {_character.name} 跳跃!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlimeBehavior] 跳跃失败: {ex.Message}");
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            if (character == null) return;

            try
            {
                Vector3 pos = character.transform.position;
                RaycastHit hit;

                // 1）优先用射线往下找到地面（避免最后一次落地点刚好在空中平台边缘）
                if (Physics.Raycast(pos + Vector3.up, Vector3.down, out hit, 50f, LayerMask.GetMask("Default", "Terrain")))
                {
                    character.transform.position = hit.point;
                }
                // 2）找不到地面时，退回到最后一次记录的落地点
                else if (_hasGroundedPosition)
                {
                    character.transform.position = _lastGroundedPosition;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlimeBehavior] OnEliteDeath 复位到地面失败: {ex.Message}");
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            // 清理Modifier
            try
            {
                if (_gunDamageStat != null && _removeModifierMethod != null && _currentGunModifier != null)
                {
                    _removeModifierMethod.Invoke(_gunDamageStat, new object[] { _currentGunModifier });
                }
                
                if (_meleeDamageStat != null && _removeModifierMethod != null && _currentMeleeModifier != null)
                {
                    _removeModifierMethod.Invoke(_meleeDamageStat, new object[] { _currentMeleeModifier });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlimeBehavior] 清理Modifier失败: {ex.Message}");
            }
            
            if (character != null && _originalScale != Vector3.zero)
            {
                character.transform.localScale = _originalScale;
            }
            
            _character = null;
            _health = null;
            _movement = null;
            _itemCache = null;
            _getStatMethod = null;
            _gunDamageStat = null;
            _meleeDamageStat = null;
            _removeModifierMethod = null;
            _addModifierMethod = null;
            _currentGunModifier = null;
            _currentMeleeModifier = null;
            _lastHealthPercent = 0f;
            _currentDamageMultiplier = InitialDamageMult;
        }
    }
}