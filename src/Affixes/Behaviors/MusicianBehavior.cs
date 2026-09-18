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

        /// <summary>同上的音量/强度参数。</summary>
        /// <remarks>
        /// 实测（2026-09-18 的 Player.log）：这个参数的默认值是 <b>1</b>（量程 0~1），
        /// 所以之前注释里"默认 0、声音被它门控"的说法是错的——不设它也有声。
        /// 仍然显式设成 1，只为不受别处改动影响。
        /// </remarks>
        private const string FallbackIntensityParameter = "parameter:/Kazoo/Intensity";

        /// <summary>
        /// 相对原版音量。走 <c>EventInstance.setVolume</c>（FMOD 按实例缩放），
        /// 不动 <c>bus:/Master/SFX</c>——那是全游戏音效的总线，碰它会连枪声脚步一起改小。
        ///
        /// <para><b>为什么是 1.0</b>：这里原先是 0.45（用户要求"稍低一点"），实机反馈偏小，
        /// 先回到原版音量做基准。⚠ 敌人通常离玩家 10~20m，会再吃一层 3D 距离衰减，
        /// 而原版卡祖笛是玩家自己拿在手上吹的（几乎无衰减）——所以"回到 1.0"未必就够响。
        /// 真要补偿距离衰减，可以设成大于 1 的值（FMOD 允许），见 <see cref="LogVolume"/> 打出的实测比值。</para>
        /// </summary>
        private const float VolumeScale = 1f;

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

        /// <summary>
        /// 音阶：小调五声的半音偏移。
        ///
        /// <para>实测（<c>Player.log</c>）音高参数量程是 <c>-24 ~ 24</c>、默认 <c>0</c>，
        /// 这就是**半音**——所以音阶可以直接按半音写，不必再按量程比例摊开。
        /// 小调五声 (<c>0 3 5 7 10</c>) 怎么随机都不会难听，跨度也只有一个八度不到。</para>
        /// </summary>
        private static readonly int[] PentatonicSemitones = { 0, 3, 5, 7, 10 };

        /// <summary>
        /// 判定"这个参数的单位是半音"的阈值：量程跨度小于一个八度就认为不是半音
        /// （例如归一化的 0~1），改用按比例摊开的音阶。
        /// </summary>
        private const float SemitoneRangeThreshold = 12f;

        /// <summary>非半音参数时，旋律占用量程的比例（以参数默认值为中心）。</summary>
        private const float MelodySpanRatio = 0.3f;

        /// <summary>下一个音走级进的概率（其余为小跳），避免旋律变成乱跳。</summary>
        private const float StepwiseChance = 0.7f;

        // ═══════════════ 探测结果（进程内一次） ═══════════════

        private static bool _probed;
        private static string _pitchParameter = FallbackPitchParameter;
        private static string _intensityParameter = FallbackIntensityParameter;

        /// <summary>音阶每一级对应的参数值。由探测结果构建，见 <see cref="BuildScale"/>。</summary>
        private static float[] _noteValues = { 0f, 3f, 5f, 7f, 10f };

        private static bool _postFailedLogged;
        private static int _volumeLogsLeft = 3;

        // ═══════════════ 运行状态 ═══════════════

        private EventInstance? _kazoo;
        private float _cooldownRemaining;

        /// <summary>当前音符还剩多久。</summary>
        private float _noteRemaining;
        /// <summary>本乐句还剩几个音符（含当前这个）。</summary>
        private int _notesLeft;
        /// <summary>当前音级，<c>0.._noteValues.Length-1</c>。</summary>
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
            // 随后 Post 的 ApplyParameters 会把它带给新事件。
            audio.SetParameterByName(_intensityParameter, 1f);

            _noteIndex = Random.Range(0, _noteValues.Length);
            _notesLeft = Random.Range(NotesPerPhraseMin, NotesPerPhraseMax + 1);
            audio.SetParameterByName(_pitchParameter, _noteValues[_noteIndex]);

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
            LogVolume(character);
            _noteRemaining = Random.Range(NoteDurationMin, NoteDurationMax);
        }

        /// <summary>
        /// 打几条音量诊断（全程最多 <c>3</c> 条），用来判断"3D 距离衰减吃掉了多少"。
        ///
        /// <para><c>getVolume(out volume, out finalvolume)</c> 的第二个值是**算进所有衰减之后**的
        /// 实际音量。两者一比就知道该把 <see cref="VolumeScale"/> 补偿到多少——
        /// 与其反复调参，不如把它测出来。</para>
        /// </summary>
        private void LogVolume(CharacterMainControl character)
        {
            if (_volumeLogsLeft <= 0 || !_kazoo.HasValue) return;
            _volumeLogsLeft--;

            if (_kazoo.Value.getVolume(out float setVolume, out float finalVolume) != FMOD.RESULT.OK) return;

            CharacterMainControl player = CharacterMainControl.Main;
            float dist = player != null
                ? Vector3.Distance(character.transform.position, player.transform.position)
                : -1f;

            Debug.Log($"[EliteEnemies.Musician] 音量诊断：设定={setVolume:F3} 实际={finalVolume:F3} " +
                      $"距离={dist:F1}m（实际/设定 = 距离衰减倍数）");
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
                       .SetParameterByName(_pitchParameter, _noteValues[_noteIndex]);
            _noteRemaining = Random.Range(NoteDurationMin, NoteDurationMax);
        }

        /// <summary>
        /// 下一个音级：以级进为主、偶尔小跳，撞到音阶边界就反弹。
        /// <b>绝不原地重复</b>——重复音会让旋律听起来像卡住了。
        /// </summary>
        private static int NextNoteIndex(int current)
        {
            int steps = _noteValues.Length;
            if (steps <= 1) return 0;

            bool stepwise = Random.value < StepwiseChance;
            int distance = stepwise ? 1 : 2;
            int direction = Random.value < 0.5f ? -1 : 1;
            int step = distance * direction;

            int next = current + step;
            if (next < 0 || next >= steps)
            {
                next = current - step;   // 反弹，而不是夹取（夹取会连着两次同音）
            }
            return Mathf.Clamp(next, 0, steps - 1);
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
                    BuildScale(-24f, 24f, 0f);
                    return true;
                }

                desc.getParameterDescriptionCount(out int count);
                string pitchName = null, intensityName = null;
                float pitchMin = 0f, pitchMax = 0f, pitchDefault = 0f;

                // 名字匹配不上时的兜底：本事件上"量程最宽的那个参数"就是音高。
                // 实测 2026-09-18：两个参数分别是 ±24（音高）与 0~1（强度），量程差得很开，
                // 这个判据不会认错。留着它是因为**名字匹配这条路实测失败过**——
                // 见下面关于 StringWrapper 的注释。
                float widestSpan = -1f, widestMin = 0f, widestMax = 0f, widestDefault = 0f;

                for (int i = 0; i < count; i++)
                {
                    if (desc.getParameterDescriptionByIndex(i, out PARAMETER_DESCRIPTION p) != FMOD.RESULT.OK) continue;

                    // ⚠ 必须用**隐式转换**取名字，不能写 p.name.ToString()：
                    //   FMOD.StringWrapper 有 implicit operator string，但**没有重写 ToString()**，
                    //   所以 ToString() 返回的是类型名 "FMOD.StringWrapper"。
                    //   这个坑实测踩过——第一版就是这么写的，于是名字永远匹配不上，
                    //   静默退回了游戏那套本来就坏的写法，旋律一直没响。
                    string name = p.name;
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

                    float span = p.maximum - p.minimum;
                    if (span > widestSpan)
                    {
                        widestSpan = span;
                        widestMin = p.minimum;
                        widestMax = p.maximum;
                        widestDefault = p.defaultvalue;
                    }
                }

                bool byRange = false;
                if ((pitchName == null || pitchMax <= pitchMin) && widestSpan >= SemitoneRangeThreshold)
                {
                    // 名字没匹配上，但有个量程明显是半音的参数 → 按量程认它
                    byRange = true;
                    pitchMin = widestMin;
                    pitchMax = widestMax;
                    pitchDefault = widestDefault;
                }

                if (pitchMax <= pitchMin)
                {
                    Debug.LogWarning($"[EliteEnemies.Musician] {KazooEvent} 上认不出音高参数" +
                                     $"（找到 {count} 个参数，最宽量程 {widestSpan}），" +
                                     $"将退回游戏自己的参数名写法（音高不会变化）");
                    BuildScale(-24f, 24f, 0f);
                    return true;
                }

                if (pitchName != null) _pitchParameter = pitchName;
                if (intensityName != null) _intensityParameter = intensityName;

                BuildScale(pitchMin, pitchMax, pitchDefault);

                Debug.Log($"[EliteEnemies.Musician] 卡祖笛音高参数='{_pitchParameter}'" +
                          $"（{(byRange ? "按量程认定，名字没匹配上" : "按名字匹配")}），" +
                          $"参数量程 [{pitchMin}, {pitchMax}] 默认 {pitchDefault}；" +
                          $"旋律音阶 [{string.Join(", ", System.Array.ConvertAll(_noteValues, v => v.ToString("F1")))}]");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EliteEnemies.Musician] 探测卡祖笛参数失败，将退回游戏自己的写法: {ex.Message}");
                BuildScale(-24f, 24f, 0f);
                return true;
            }
        }

        /// <summary>
        /// 由参数量程构建音阶。
        ///
        /// <para>量程跨度 ≥ 一个八度 → 认定单位是**半音**，直接用小调五声（实测正是这种情况）。
        /// 否则单位未知（例如归一化的 0~1），只能退而求其次：以默认值为中心、按量程比例摊开。</para>
        /// </summary>
        private static void BuildScale(float min, float max, float center)
        {
            int steps = PentatonicSemitones.Length;

            if (max - min >= SemitoneRangeThreshold)
            {
                _noteValues = new float[steps];
                for (int i = 0; i < steps; i++)
                {
                    _noteValues[i] = Mathf.Clamp(center + PentatonicSemitones[i], min, max);
                }
                return;
            }

            float half = (max - min) * MelodySpanRatio * 0.5f;
            float low = Mathf.Clamp(center - half, min, max);
            float high = Mathf.Clamp(center + half, min, max);

            _noteValues = new float[steps];
            for (int i = 0; i < steps; i++)
            {
                _noteValues[i] = Mathf.Lerp(low, high, (float)i / (steps - 1));
            }
        }

    }
}
