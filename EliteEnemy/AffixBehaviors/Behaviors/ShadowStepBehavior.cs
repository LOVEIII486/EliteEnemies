// ShadowStepBehavior.cs
using UnityEngine;
using EliteEnemies.AffixBehaviors;

namespace EliteEnemies.AffixBehaviors
{
    /// <summary>
    /// 【影步】—— 被击中后，敌人会立即进行一次“战术闪移”：
    ///  - 立刻 Dash 一次（若可用），并在短时间内强制侧移/后撤
    ///  - CD：默认 4s；闪移持续：0.6s；强制速度：8m/s
    ///  - 不改变数值伤害/移速，只是行为改变，避免与其他词条耦合
    /// </summary>
    public class ShadowStepBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "影步";

        private const float _cooldown = 4.0f;
        private const float _evadeDuration = 0.6f;
        private const float _evadeSpeed = 8.0f;

        private bool _isActive = false;
        private bool _isEvading = false;
        private float _cdTimer = 0f;
        private float _evadeTimer = 0f;

        private CharacterMainControl _ch;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            _ch = character;
            if (_ch != null && _ch.Health != null)
            {
                _isActive = true;
                _cdTimer = 0f;
                _evadeTimer = 0f;
                _isEvading = false;

                // ✅ 通过类名订阅静态事件
                Health.OnHurt += OnHurtShadowStep;
            }
            else
            {
                _isActive = false;
                Debug.LogWarning("[ShadowStep] 初始化失败：缺少 Health 组件。");
            }
        }




        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            if (!_isActive) return;
            var h = character?.Health;
            if (h == null) { _isActive = false; return; }

            // 冷却计时
            if (_cdTimer > 0f) _cdTimer = Mathf.Max(0f, _cdTimer - deltaTime);

            // 闪移驱动：在持续时间内保持强制侧移/后撤速度
            if (_isEvading)
            {
                _evadeTimer -= deltaTime;
                if (_evadeTimer <= 0f || h.IsDead)
                {
                    _isEvading = false;
                    // 结束时把强制速度清 0，交还AI控制权
                    character.SetForceMoveVelocity(Vector3.zero); // 文档接口。:contentReference[oaicite:4]{index=4}
                }
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            CleanupInternal();
        }

        public override void OnCleanup(CharacterMainControl character)
        {
            CleanupInternal();
        }

        private void CleanupInternal()
        {
            if (_ch != null && _ch.Health != null)
            {
                // ✅ 用类名取消订阅
                Health.OnHurt -= OnHurtShadowStep;
            }

            if (_isEvading && _ch != null)
                _ch.SetForceMoveVelocity(Vector3.zero);

            _isActive = false;
            _isEvading = false;
            _cdTimer = 0f;
            _evadeTimer = 0f;
            _ch = null;
        }

        private void OnHurtShadowStep(Health health, DamageInfo dmg)
        {
            if (!_isActive || _isEvading || _cdTimer > 0f || _ch == null)
                return;

            // ✅ 只响应自己角色受伤事件
            if (health != _ch.Health)
                return;

            if (health.IsDead)
                return;

            // === 以下保持原闪移逻辑 ===
            Vector3 dir = Vector3.zero;

            var player = CharacterMainControl.Main;
            if (player != null)
            {
                Vector3 away = (_ch.transform.position - player.transform.position);
                away.y = 0f;
                if (away.sqrMagnitude > 0.01f)
                {
                    if (Random.value < 0.8f)
                        dir = away.normalized;
                    else
                        dir = Vector3.Cross(away.normalized, Vector3.up);
                }
            }

            if (dir == Vector3.zero)
                dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;

            try { _ch.Dash(); } catch { }
            _ch.SetForceMoveVelocity(dir * _evadeSpeed);

            _isEvading = true;
            _evadeTimer = _evadeDuration;
            _cdTimer = _cooldown;

            // 可选调试输出
            // Debug.Log($"[ShadowStep] {_ch.name} 闪移触发！");
        }

    }
}
