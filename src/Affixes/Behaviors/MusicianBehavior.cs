using Duckov;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// ⚠ 刻意**不写** `using FMOD;`：那会把 FMOD.Debug 与 FMOD.RESULT 引进全局，
//   前者与 UnityEngine.Debug 撞名（CS0104），后者与 FMODUnity.STOP_MODE 一起
//   把 STOP_MODE 变成二义。RESULT 在下面按 `FMOD.RESULT` 全限定使用。

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 音乐家：玩家靠近时随机吹一段卡祖笛乐句，歇一会儿再吹下一段。
    ///
    /// <para><b>声音的事件路径来自游戏自己的 <c>ItemAgent_Kazoo</c></b>——它把"吹卡祖笛"
    /// 实现成「一个 FMOD 事件 + 两个 RTPC 参数」，跟谁在吹、有没有拿物品毫无关系。
    /// 所以敌人**不需要真的持有卡祖笛**，也不涉及任何反射。</para>
    ///
    /// <para><b>为什么是"乐句"而不是一个长音</b>：卡祖笛的演奏本体就是「连续哼鸣 + 音高跳变」，
    /// 所以让它像在吹曲子的关键是<b>离散的音符</b>，不是把音高平滑地扫来扫去。
    /// 每段乐句是一串 4~9 个音符，每个音符持续 0.16~0.38 秒，音高按级进随机游走；
    /// 乐句之间静默 1~3 秒。</para>
    ///
    /// <para>⚠ <b>参数名与值域是运行期探测出来的，不是猜的</b>，理由见
    /// <see cref="ProbeKazooParameters"/>。</para>
    /// </summary>
    public class MusicianBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Musician";

        // ═══════════════ 声音：事件路径照抄 ItemAgent_Kazoo.cs:9 ═══════════════

        /// <summary>卡祖笛的 FMOD 事件路径（不含 <c>event:/</c> 前缀）。</summary>
        private const string KazooEvent = "SFX/Special/Kazoo";

        /// <summary>
        /// 游戏自己的参数名写法（<c>AudioManager.cs:703</c> 会再拼上 <c>"parameter:/"</c>）。
        ///
        /// <para>⚠ 这个 <c>"parameter:/"</c> 前缀<b>不是 FMOD 的标准写法</b>，
        /// 所以它很可能一直是个静默失败——不报错，只是那个参数从未生效。
        /// 我们**优先用探测到的真实参数名**，只有探测失败时才退回这个字符串
        /// （退回时行为与游戏完全一致：一样不生效，但至少不会更糟）。</para>
        /// </summary>
        private const string FallbackPitchParameter = "parameter:/Kazoo/Pitch";

        /// <summary>同上的音量/强度参数。<b>必须给上再 Post</b>——默认是 0，声音被它门控。</summary>
        private const string FallbackIntensityParameter = "parameter:/Kazoo/Intensity";

        /// <summary>相对原版音量。走 <c>EventInstance.setVolume</c>（FMOD 按实例缩放），
        /// 不动 <c>bus:/Master/SFX</c>——那是全游戏音效的总线，碰它会连枪声脚步一起改小。</summary>
        private const float VolumeScale = 0.45f;

        // ═══════════════ 乐句 ═══════════════

        /// <summary>玩家进入这个距离才开始吹。</summary>
        private const float TriggerDistance = 20f;

        /// <summary>一段乐句里的音符数（随机区间，含两端）。</summary>
        private const int NotesPerPhraseMin = 4;
        private const int NotesPerPhraseMax = 9;

        /// <summary>单个音符的时长（随机区间，秒）。</summary>
        private const float NoteDurationMin = 0.16f;
        private const float NoteDurationMax = 0.38f;

        /// <summary>两次乐句之间的静默时长（随机区间，秒）。</summary>
        private const float CooldownMin = 1f;
        private const float CooldownMax = 3f;

        // ═══════════════ 旋律 ═══════════════

        /// <summary>音阶级数。5 级 ≈ 五声音阶，怎么随机都不会难听。</summary>
        private const int ScaleSteps = 5;

        /// <summary>
        /// 旋律占用参数量程的**比例**，以参数默认值为中心。
        ///
        /// <para>用它而不是写死音高数值，是因为这个参数的单位未知（可能是半音，也可能是 0~1）。
        /// 取量程的一个比例，无论哪种单位都能落在合理音域里；默认值则是"这把卡祖笛本来的音高"，
        /// 旋律绕着它走最自然。</para>
        /// </summary>
        private const float MelodySpanRatio = 0.3f;

        /// <summary>下一个音走级进的概率（其余为小跳），避免旋律变成乱跳。</summary>
        private const float StepwiseChance = 0.7f;

        // ═══════════════ 探测结果（进程内一次） ═══════════════

        private static bool _probed;
        private static string _pitchParameter = FallbackPitchParameter;
        private static string _intensityParameter = FallbackIntensityParameter;
        private static float _pitchLow;
        private static float _pitchHigh;

        private static bool _postFailedLogged;

        // ═══════════════ 运行状态 ═══════════════

        private EventInstance? _kazoo;
        private float _cooldownRemaining;

        /// <summary>当前音符还剩多久。</summary>
        private float _noteRemaining;
        /// <summary>本乐句还剩几个音符（含当前这个）。</summary>
        private int _notesLeft;
        /// <summary>当前音级，<c>0..ScaleSteps-1</c>。</summary>
        private int _noteIndex;

        // ═══════════════════════════════════════════════════════════════

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            CharacterMainControl player = CharacterMainControl.Main;
            if (player == null) return;

            float distSqr = (character.transform.position - player.transform.position).sqrMagnitude;
            if (distSqr > TriggerDistance * TriggerDistance)
            {
                // 玩家走远了：立刻收声，但**不重置冷却**——
                // 否则玩家在边界上反复横跳就能刷出连续演奏。
                StopKazoo();
                return;
            }

            if (IsPlaying())
            {
                TickPhrase(character, deltaTime);
                return;
            }

            _cooldownRemaining -= deltaTime;
            if (_cooldownRemaining > 0f) return;

            StartPhrase(character);
        }

        /// <summary>精英死亡时收声——不能在尸体旁边继续吹。</summary>
        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo)
        {
            StopKazoo();
        }

        /// <summary>清理（精英消失/组件销毁）。<b>必不可少</b>，理由见 <see cref="StopKazoo"/>。</summary>
        public override void OnCleanup(CharacterMainControl character)
        {
            StopKazoo();
        }

        // ═══════════════════════════════════════════════════════════════

        private void StartPhrase(CharacterMainControl character)
        {
            GameObject go = character.gameObject;

            // AudioManager 对未激活的对象会拒绝发声（只打一条 warning 然后返回 null）
            if (!go.activeInHierarchy)
            {
                _cooldownRemaining = CooldownMin;
                return;
            }

            if (!ProbeKazooParameters()) return;   // 探测没成功就不吹，下帧再来

            AudioObject audio = AudioObject.GetOrCreate(go);

            // 顺序有讲究：SetParameterByName 会把参数**缓存**在这个 AudioObject 上，
            // 随后 Post 的 ApplyParameters 会把它带给新事件。反过来的话，
            // 事件已经以 Intensity=0 起播了，那一下是哑的。
            audio.SetParameterByName(_intensityParameter, 1f);

            _noteIndex = Random.Range(0, ScaleSteps);
            _notesLeft = Random.Range(NotesPerPhraseMin, NotesPerPhraseMax + 1);
            audio.SetParameterByName(_pitchParameter, NoteValue(_noteIndex));

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
            _noteRemaining = Random.Range(NoteDurationMin, NoteDurationMax);
        }

        private void TickPhrase(CharacterMainControl character, float deltaTime)
        {
            _noteRemaining -= deltaTime;
            if (_noteRemaining > 0f) return;

            _notesLeft--;
            if (_notesLeft <= 0)
            {
                StopKazoo();
                _cooldownRemaining = Random.Range(CooldownMin, CooldownMax);
                return;
            }

            _noteIndex = NextNoteIndex(_noteIndex);
            AudioObject.GetOrCreate(character.gameObject)
                       .SetParameterByName(_pitchParameter, NoteValue(_noteIndex));
            _noteRemaining = Random.Range(NoteDurationMin, NoteDurationMax);
        }

        /// <summary>
        /// 下一个音级：以级进为主、偶尔小跳，撞到音阶边界就反弹。
        /// <b>绝不原地重复</b>——重复音会让旋律听起来像卡住了。
        /// </summary>
        private static int NextNoteIndex(int current)
        {
            bool stepwise = Random.value < StepwiseChance;
            int distance = stepwise ? 1 : 2;
            int direction = Random.value < 0.5f ? -1 : 1;
            int step = distance * direction;

            int next = current + step;
            if (next < 0 || next >= ScaleSteps)
            {
                next = current - step;   // 反弹，而不是夹取（夹取会连着两次同音）
            }
            return Mathf.Clamp(next, 0, ScaleSteps - 1);
        }

        /// <summary>把音级映射成参数值——落在探测到的音域内，见 <see cref="MelodySpanRatio"/>。</summary>
        private static float NoteValue(int index)
        {
            float t = ScaleSteps <= 1 ? 0.5f : (float)index / (ScaleSteps - 1);
            return Mathf.Lerp(_pitchLow, _pitchHigh, t);
        }

        /// <summary>停掉当前乐句。**幂等**——<c>OnCleanup</c> 可能被走到两次（基类约定）。</summary>
        /// <remarks>
        /// ⚠ <b>这一句不能省</b>：卡祖笛是个持续循环的事件，而游戏的 <c>AudioObject</c>
        /// <b>没有 <c>OnDestroy</c></b>——敌人对象被销毁时，挂在上面的 FMOD 事件不会被自动回收。
        /// 漏掉这里就是一段**永远停不下来的声音**。
        ///
        /// <para>⚠ <b>不要改用 <c>AudioManager.StopAll(go)</c></b>：那会把这个敌人身上
        /// <b>所有</b> FMOD 事件一起停掉，包括脚步声和语音。只停我们自己持有的这一个。</para>
        /// </remarks>
        private void StopKazoo()
        {
            if (_kazoo.HasValue && _kazoo.Value.isValid())
            {
                _kazoo.Value.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
            _kazoo = null;
            _noteRemaining = 0f;
            _notesLeft = 0;
        }

        private bool IsPlaying()
        {
            return _kazoo.HasValue && _kazoo.Value.isValid();
        }

        // ═══════════════ 参数探测 ═══════════════

        /// <summary>
        /// 向 FMOD 问出 <c>SFX/Special/Kazoo</c> 事件上<b>真实</b>的参数名与值域，进程内只成功一次。
        ///
        /// <para><b>为什么必须探测</b>：两件事从源码里都看不出来——
        /// ① 参数的真实名字（游戏传的 <c>"parameter:/Kazoo/Pitch"</c> 带了段不像 FMOD 写法的前缀）；
        /// ② 这个参数的<b>单位</b>（半音？0~1？），而不知道单位就没法生成像样的音阶。
        /// 与其猜，不如直接问 FMOD：<c>PARAMETER_DESCRIPTION</c> 带
        /// <c>name</c> / <c>minimum</c> / <c>maximum</c> / <c>defaultvalue</c>。</para>
        ///
        /// <para>探测结果打一条日志——它同时也回答"游戏自己的音高调用到底有没有生效"这个问题。</para>
        /// </summary>
        private static bool ProbeKazooParameters()
        {
            if (_probed) return true;

            // 关卡未就绪时 FMOD 还没初始化，这次不算失败，下次再试
            if (!RuntimeManager.IsInitialized) return false;

            _probed = true;
            try
            {
                if (RuntimeManager.StudioSystem.getEvent("event:/" + KazooEvent, out EventDescription desc) != FMOD.RESULT.OK)
                {
                    Debug.LogWarning($"[EliteEnemies.Musician] FMOD 里找不到事件 {KazooEvent}，" +
                                     $"将退回游戏自己的参数名写法（{FallbackPitchParameter}）");
                    ApplyFallbackRange();
                    return true;
                }

                desc.getParameterDescriptionCount(out int count);
                string pitchName = null, intensityName = null;
                float pitchMin = 0f, pitchMax = 0f, pitchDefault = 0f;

                for (int i = 0; i < count; i++)
                {
                    if (desc.getParameterDescriptionByIndex(i, out PARAMETER_DESCRIPTION p) != FMOD.RESULT.OK) continue;

                    string name = p.name.ToString();
                    Debug.Log($"[EliteEnemies.Musician] {KazooEvent} 参数：name='{name}' " +
                              $"min={p.minimum} max={p.maximum} default={p.defaultvalue} type={p.type}");

                    // 用 EndsWith 而不是等值比较：这样不管真实名字是 "Kazoo/Pitch"
                    // 还是游戏那套带前缀的写法，都能匹配上
                    if (name.EndsWith("Kazoo/Pitch"))
                    {
                        pitchName = name;
                        pitchMin = p.minimum;
                        pitchMax = p.maximum;
                        pitchDefault = p.defaultvalue;
                    }
                    else if (name.EndsWith("Kazoo/Intensity"))
                    {
                        intensityName = name;
                    }
                }

                if (pitchName == null || pitchMax <= pitchMin)
                {
                    Debug.LogWarning($"[EliteEnemies.Musician] {KazooEvent} 上没有可用的音高参数" +
                                     $"（找到 {count} 个参数），将退回游戏自己的参数名写法");
                    ApplyFallbackRange();
                    return true;
                }

                _pitchParameter = pitchName;
                SetMelodyRange(pitchMin, pitchMax, pitchDefault);
                if (intensityName != null) _intensityParameter = intensityName;

                Debug.Log($"[EliteEnemies.Musician] 卡祖笛音高参数='{_pitchParameter}'，" +
                          $"旋律音域 [{_pitchLow:F2}, {_pitchHigh:F2}]（共 {ScaleSteps} 级）");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EliteEnemies.Musician] 探测卡祖笛参数失败，将退回游戏自己的写法: {ex.Message}");
                ApplyFallbackRange();
                return true;
            }
        }

        /// <summary>
        /// 以参数默认值为中心、取量程的 <see cref="MelodySpanRatio"/> 作为旋律音域，
        /// 并夹回参数允许的范围内。
        /// </summary>
        private static void SetMelodyRange(float min, float max, float center)
        {
            float half = (max - min) * MelodySpanRatio * 0.5f;
            _pitchLow = Mathf.Clamp(center - half, min, max);
            _pitchHigh = Mathf.Clamp(center + half, min, max);
        }

        /// <summary>
        /// 探测走不通时的兜底音域。
        /// 取 <c>-16..16</c> 是按游戏那句 <c>点积 × 24 / maxScale(15)</c> 的量级估的
        /// （<c>ItemAgent_Kazoo.cs:90</c>）——反正是兜底，探测成功就用不到它。
        /// </summary>
        private static void ApplyFallbackRange()
        {
            _pitchLow = -16f;
            _pitchHigh = 16f;
        }
    }
}
