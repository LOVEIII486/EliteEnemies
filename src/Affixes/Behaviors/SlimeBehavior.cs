using ECM2;
using UnityEngine;
using EliteEnemies.Modifiers;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 【史莱姆】词缀 - 体型随血量缩小，伤害随之提升，并不断跳跃。
    ///
    /// <para><b>关于"死在空中"</b>：史莱姆每 0.5~1.5 秒跳一次，所以经常在空中被打死。
    /// 箱子与尸体分别处理：</para>
    /// <list type="bullet">
    /// <item><b>箱子</b>由本模组自己的补丁 <c>Patches\SlimeLootboxPhysics</c> 负责——它按
    /// <c>Slime</c> 词条把箱子改成**物理掉落**（<c>isKinematic = false</c> + <c>useGravity</c>
    /// + 连续碰撞检测 + 取消 trigger），箱子会自己落到地面。</item>
    /// <item><b>尸体</b>在死亡那一刻向下打**一条射线**落到地面（<see cref="SnapCorpseToGround"/>）。
    /// 这一步不是纯观感：死亡钩子跑在战利品箱创建**之前**，它决定箱子的出生位置。
    /// 原先的做法是**每帧**记录"最后安全落地高度"、死亡时瞬移过去——史莱姆一直在跳，
    /// 那个高度可能是很久以前的；一次射线更简单也更准，而且不再有逐帧记账。</item>
    /// </list>
    /// </summary>
    public class SlimeBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Slime";

        private static readonly float InitialScale = 3.5f;
        private static readonly float MinScale = 0.5f;
        private static readonly float InitialHealthMult = 3.5f;
        private static readonly float InitialDamageMult = 0.65f;
        private static readonly float MaxDamageMult = 1.5f;
        private static readonly float HealthThreshold = 0.05f;

        private static readonly float JumpForce = 5f;
        private static readonly float JumpIntervalMin = 0.5f;
        private static readonly float JumpIntervalMax = 1.5f;
        private static readonly float GroundPause = 0.3f;
        private static readonly float MaxJumpHeightCheck = 5f;
        private static readonly float NoJumpHealthThreshold = 0.2f;

        private CharacterMainControl _character;
        private Health _health;

        /// <summary>游戏自己的移动接口（<c>CharacterMainControl.movementControl</c>）：
        /// <c>IsOnGround</c>（<c>Movement.cs:85</c>）/ <c>Velocity</c>（<c>:109</c>）/
        /// <c>SetYVelocity</c>（<c>:148</c>）——优先用它，别直接摸 ECM2。</summary>
        private Movement _movementControl;

        /// <summary>ECM2 的角色移动组件。只为一件事留着：跳跃前挂起贴地约束
        /// （<c>PauseGroundConstraint</c>，游戏没有包装它，自己在 <c>Movement.cs:245</c> 也直接调 ECM2）。</summary>
        private CharacterMovement _ecm2Movement;

        private Rigidbody _body;

        private Vector3 _originalScale;
        private float _lastHealthPercent = 1f;
        private float _nextJumpTime;
        private int _obstacleMask;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            if (character == null) return;

            _character = character;
            // 来自框架上下文（每个敌人只解析一次），不再自己 GetComponent
            _health = Ctx.Health;
            _movementControl = Ctx.Movement;
            _ecm2Movement = Ctx.Ecm2Movement;
            _body = Ctx.Body;

            _obstacleMask = LayerMask.GetMask("Default", "Ground", "Wall", "HalfObsticle", "Door");

            _originalScale = character.transform.localScale;

            // 初始生命加成并补满血量
            CharacterModifiers.Quick.ModifyHealth(character, InitialHealthMult, this.AffixName, true);

            // 初始体型变化与伤害降低
            UpdateScaleAndDamage(character, 1.0f);

            _lastHealthPercent = 1f;
            _nextJumpTime = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (_health == null || _health.IsDead) return;

            // 1. 动态体型和伤害更新
            float currentHealthPercent = _health.CurrentHealth / _health.MaxHealth;
            if (Mathf.Abs(currentHealthPercent - _lastHealthPercent) >= HealthThreshold)
            {
                UpdateScaleAndDamage(character, currentHealthPercent);
                _lastHealthPercent = currentHealthPercent;
            }

            // 2. 跳跃逻辑
            if (currentHealthPercent >= NoJumpHealthThreshold && Time.time >= _nextJumpTime && CanSafelyJump())
            {
                PerformJump();
                _nextJumpTime = Time.time + UnityEngine.Random.Range(JumpIntervalMin, JumpIntervalMax);
            }
        }

        private void UpdateScaleAndDamage(CharacterMainControl character, float healthPercent)
        {
            float t = 1f - healthPercent;

            float newScaleFactor = Mathf.Lerp(InitialScale, MinScale, t);
            character.transform.localScale = _originalScale * newScaleFactor;

            float newDamageMultiplier = Mathf.Lerp(InitialDamageMult, MaxDamageMult, t);

            CharacterModifiers.Modify(character, StatKeys.GunDamageMultiplier, newDamageMultiplier, true, this.AffixName);
            CharacterModifiers.Modify(character, StatKeys.MeleeDamageMultiplier, newDamageMultiplier, true, this.AffixName);
        }

        /// <summary>
        /// 是否稳定站在地面上。用游戏自己的公开包装（<c>Movement.IsOnGround</c> 内部就是
        /// <c>characterMovement.isOnGround</c>，`Movement.cs:85`；速度同 `:109`）。
        ///
        /// <para>⚠ <b>【未核实】</b> ECM2 本体不在 `..\DuckovSource-ILSpy\` 里，
        /// 所以无法从源码确认它的 <c>isOnGround</c> 与原先直接读的 <c>isGrounded</c>
        /// 是否同一语义。改用游戏自己的包装是更稳妥的方向（那是游戏判定落地时用的那个），
        /// 但"两者等价"这句**没有出处支撑**——若实机发现跳跃时机变了，先怀疑这里。</para>
        /// </summary>
        private bool IsGrounded()
            => _movementControl != null && _movementControl.IsOnGround
               && Mathf.Abs(_movementControl.Velocity.y) < 0.1f;

        private bool CanSafelyJump() => IsGrounded() && !Physics.Raycast(_character.transform.position + Vector3.up, Vector3.up, MaxJumpHeightCheck, _obstacleMask);

        private void PerformJump()
        {
            if (_movementControl == null) return;

            // 先挂起贴地约束，否则向上的速度会被它按回去（ECM2 的原始接口，游戏没有包装）
            _ecm2Movement?.PauseGroundConstraint(GroundPause);

            // 官方包装：characterMovement.velocity.y = y（Movement.cs:148）
            _movementControl.SetYVelocity(JumpForce);
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            // 死在半空时把**尸体**落到正下方的地面。
            //
            // 为什么保留这一步（而不是全靠箱子物理）：死亡钩子**跑在战利品箱创建之前**
            // （`BeforeCharacterSpawnLootOnDead` 在 `CharacterMainControl.OnDead:1810` 触发，
            // 箱子在 `:1813` 才 `CreateFromItem`），所以这一步决定了箱子的**出生位置**——
            // 让它在实地上出现、再由 `SlimeLootboxPhysics` 的物理兜底，比让它先出现在半空稳。
            //
            // 做法与原先不同：原先**每帧**记录"最后安全落地高度"再瞬移过去，而史莱姆一直在跳，
            // 那个高度可能是很久以前的；现在只在死亡这一刻**向下打一条射线**取地面——更简单也更准。
            if (character != null && !IsGrounded()) SnapCorpseToGround(character);

            // 冻结尸体
            if (_body != null) _body.isKinematic = true;
            if (_ecm2Movement != null) _ecm2Movement.enabled = false;
        }

        /// <summary>把尸体落到正下方地面（只打一条射线，不做逐帧记账）。</summary>
        private void SnapCorpseToGround(CharacterMainControl character)
        {
            const float MaxDropDistance = 50f;

            Vector3 pos = character.transform.position;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, MaxDropDistance, _obstacleMask))
            {
                character.transform.position = new Vector3(pos.x, hit.point.y + 0.1f, pos.z);
            }
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            ClearBaseModifiers(character);

            if (character != null && _originalScale != Vector3.zero)
            {
                character.transform.localScale = _originalScale;
            }

            if (_ecm2Movement != null) _ecm2Movement.enabled = true;
            if (_body != null) _body.isKinematic = false;

            _character = null;
            _health = null;
            _movementControl = null;
            _ecm2Movement = null;
            _body = null;
        }
    }
}
