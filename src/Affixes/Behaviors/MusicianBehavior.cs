using Duckov;
using FMOD.Studio;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 音乐家：玩家靠近时随机吹一段卡祖笛。
    ///
    /// <para><b>声音完全复刻游戏自己的 <c>ItemAgent_Kazoo</c></b>（见下方三个常量）——
    /// 它把"吹卡祖笛"实现成「一个 FMOD 事件 + 两个 RTPC 参数」，跟谁在吹、有没有拿物品
    /// 毫无关系。所以敌人**不需要真的持有卡祖笛**，我们照抄那三个调用即可，
    /// 也不涉及任何反射。</para>
    ///
    /// <para><b>为什么音高要自己驱动</b>：游戏里 <c>Kazoo/Pitch</c> 是玩家**晃鼠标**算出来的
    /// （<c>ItemAgent_Kazoo.cs:84-90</c>，相机右方向与瞄准方向的点积）。AI 没有鼠标，
    /// 不喂这个参数的话就是一成不变的死长音。这里用一条正弦摆动代替——听起来像在吹曲子。</para>
    /// </summary>
    public class MusicianBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Musician";

        // ═══════════ 声音：逐字照抄 ItemAgent_Kazoo ═══════════
        //
        // ⚠ 这三个字符串是**游戏自己代码里的常量**（ItemAgent_Kazoo.cs:9 / :91 / :92），
        //   不是我们猜的。改错了不会报错，只是没声音——所以照抄，别"顺手优化"。

        /// <summary>卡祖笛的 FMOD 事件路径。</summary>
        private const string KazooEvent = "SFX/Special/Kazoo";

        /// <summary>音高参数，值域与量级见 <see cref="PitchSwing"/>。</summary>
        private const string PitchParameter = "Kazoo/Pitch";

        /// <summary>音量/强度参数。<b>必须先给上再 Post</b>——默认是 0，声音被它门控。</summary>
        private const string IntensityParameter = "Kazoo/Intensity";

        /// <summary>
        /// 相对原版音量。
        ///
        /// <para>走 <c>EventInstance.setVolume</c>（FMOD 的**按实例**缩放），
        /// 不动 <c>bus:/Master/SFX</c>——那是全游戏音效的总线，碰它会连枪声脚步一起改小。</para>
        /// </summary>
        private const float VolumeScale = 0.45f;

        // ═══════════ 行为参数 ═══════════

        /// <summary>玩家进入这个距离才开始吹。</summary>
        private const float TriggerDistance = 20f;

        /// <summary>单次演奏时长（随机区间，秒）。</summary>
        private const float PlayMin = 1f;
        private const float PlayMax = 3f;

        /// <summary>两次演奏之间的间隔（随机区间，秒）。</summary>
        private const float CooldownMin = 1f;
        private const float CooldownMax = 3f;

        /// <summary>
        /// 音高摆动的幅度。
        ///
        /// <para>⚠ <b>这个数需要实机听感校准</b>：游戏的公式产出的是
        /// <c>点积(right, 瞄准点-自身) × 24 / maxScale(15)</c>，也就是
        /// <c>±(瞄准距离 × 1.6)</c> 量级——瞄准距离通常几米到十几米，故原版值大致在 ±10~±30。
        /// 但 FMOD 里这个参数的**实际值域**源码看不到（配在工程里），
        /// 所以这里取了个保守值。听着不对就改这一个数。</para>
        /// </summary>
        private const float PitchSwing = 8f;

        /// <summary>音高摆动的角速度（弧度/秒）。</summary>
        private const float PitchSwingSpeed = 2.5f;

        // ═══════════ 运行状态 ═══════════

        private EventInstance? _kazoo;
        private float _playRemaining;
        private float _cooldownRemaining;
        private float _pitchPhase;

        /// <summary>Post 失败只报一次——它多半是"关卡还没初始化完"，不该每次冷却都刷屏。</summary>
        private static bool _postFailedLogged;

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            CharacterMainControl player = CharacterMainControl.Main;
            if (player == null) return;

            float distSqr = (character.transform.position - player.transform.position).sqrMagnitude;
            if (distSqr > TriggerDistance * TriggerDistance)
            {
                // 玩家走远了：立刻收声，但**不重置冷却**——
                // 否则玩家在 20m 边界上反复横跳就能刷出连续演奏。
                StopKazoo();
                return;
            }

            if (IsPlaying())
            {
                TickPlaying(character, deltaTime);
                return;
            }

            _cooldownRemaining -= deltaTime;
            if (_cooldownRemaining > 0f) return;

            StartKazoo(character);
        }

        /// <summary>精英死亡时收声——不能在尸体旁边继续吹。</summary>
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            StopKazoo();
        }

        /// <summary>清理（精英消失/组件销毁）。<b>必不可少</b>：见 <see cref="StopKazoo"/> 的注释。</summary>
        public override void OnCleanup(CharacterMainControl character)
        {
            StopKazoo();
        }

        // ═══════════════════════════════════════════════════════════════

        private void StartKazoo(CharacterMainControl character)
        {
            GameObject go = character.gameObject;

            // AudioManager 对未激活的对象会拒绝发声（只打一条 warning 然后返回 null）
            if (!go.activeInHierarchy)
            {
                _cooldownRemaining = CooldownMin;
                return;
            }

            // 顺序有讲究：SetRTPC 会把参数**缓存**在该 GameObject 的 AudioObject 上
            // （AudioObject.SetParameterByName → parameters 字典），
            // 随后 Post 的 ApplyParameters 会把它带给新事件。反过来的话，
            // 事件已经以 Intensity=0 起播了，那一下是哑的。
            AudioManager.SetRTPC(IntensityParameter, 1f, go);
            _pitchPhase = 0f;
            AudioManager.SetRTPC(PitchParameter, 0f, go);

            _kazoo = AudioManager.Post(KazooEvent, go);

            if (!_kazoo.HasValue)
            {
                if (!_postFailedLogged)
                {
                    _postFailedLogged = true;
                    Debug.LogWarning($"[EliteEnemies.Musician] 播放 {KazooEvent} 失败（多半是关卡尚未初始化，" +
                                     "AudioManager.Initialized 为 false）——音乐家不会有声音");
                }
                _cooldownRemaining = CooldownMin;
                return;
            }

            _kazoo.Value.setVolume(VolumeScale);
            _playRemaining = Random.Range(PlayMin, PlayMax);
        }

        private void TickPlaying(CharacterMainControl character, float deltaTime)
        {
            _playRemaining -= deltaTime;
            if (_playRemaining <= 0f)
            {
                StopKazoo();
                _cooldownRemaining = Random.Range(CooldownMin, CooldownMax);
                return;
            }

            // 正弦摆动：让长音有起伏，像在吹曲子而不是拉警报
            _pitchPhase += deltaTime * PitchSwingSpeed;
            AudioManager.SetRTPC(PitchParameter, Mathf.Sin(_pitchPhase) * PitchSwing, character.gameObject);
        }

        /// <summary>
        /// 停掉当前演奏。**幂等**——<c>OnCleanup</c> 可能被走到两次（<c>OnEliteDeath</c> 一次、
        /// 组件销毁时又一次），基类的约定要求清理必须可重复调用。
        ///
        /// <para>⚠ <b>这一句不能省</b>：卡祖笛是个持续循环的事件，而游戏的
        /// <c>AudioObject</c> **没有 <c>OnDestroy</c>**——敌人对象被销毁时，
        /// 挂在上面的 FMOD 事件不会被自动回收。漏掉这里就是一段**永远停不下来的声音**。</para>
        ///
        /// <para>⚠ <b>不要改用 <c>AudioManager.StopAll(go)</c></b>：那会把这个敌人身上
        /// <b>所有</b> FMOD 事件一起停掉，包括脚步声和语音。只停我们自己持有的这一个。</para>
        /// </summary>
        private void StopKazoo()
        {
            if (_kazoo.HasValue && _kazoo.Value.isValid())
            {
                _kazoo.Value.stop(STOP_MODE.ALLOWFADEOUT);
            }
            _kazoo = null;
            _playRemaining = 0f;
        }

        private bool IsPlaying()
        {
            return _kazoo.HasValue && _kazoo.Value.isValid();
        }
    }
}
