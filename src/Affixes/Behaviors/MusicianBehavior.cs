using System.Collections.Generic;
using Duckov;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// ⚠ 刻意**不写** `using System;`：那会把 System.Random 引进来，
//   与 UnityEngine.Random 撞名（CS0104）。下面按 System.StringComparer 全限定使用。

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
        /// 探测失败时（FMOD 未就绪、事件找不到）用的音高参数名。
        ///
        /// <para><b>实测真名就是 <c>Pitch</c></b>（2026-09-18 的 Player.log，
        /// <c>PARAMETER_DESCRIPTION.name</c> 直接给出来的）——<b>没有</b> <c>Kazoo/</c> 这一层。</para>
        ///
        /// <para>⚠ 而游戏自己传的是 <c>"parameter:/Kazoo/Pitch"</c>
        /// （<c>AudioManager.cs:703</c> 给 key 拼了个 <c>"parameter:/"</c> 前缀）。
        /// 那个字符串**不是 FMOD 的标准写法**，所以原版卡祖笛的音高多半一直静默失效——
        /// 玩家晃鼠标时音高其实并不跟着走。我们不再沿用它。</para>
        /// </summary>
        private const string FallbackPitchParameter = "Pitch";

        /// <summary>同上的强度参数。真名就是 <c>Intensity</c>（量程 0~1、默认 1）。</summary>
        /// <remarks>
        /// 默认值是 <b>1</b> 而不是 0——所以早先注释里"默认 0、声音被它门控"的说法是错的，
        /// 不设它也有声。仍然显式设成 1，只为不受别处改动影响。
        /// </remarks>
        private const string FallbackIntensityParameter = "Intensity";

        /// <summary>
        /// 相对原版音量。走 <c>EventInstance.setVolume</c>（FMOD 按实例缩放），
        /// 不动 <c>bus:/Master/SFX</c>——那是全游戏音效的总线，碰它会连枪声脚步一起改小。
        ///
        /// <para><b>为什么是 2.5</b>：原版卡祖笛是玩家**拿在手里**吹的（几乎无距离衰减），
        /// 而这个事件的内建音量只有 0.316（≈ -10dB，实测），搬到十几二十米外的敌人身上，
        /// 实机反馈"敌人在屏幕边缘就听不到了"。2.5 约合 +8dB，用来补偿那段距离。</para>
        ///
        /// <para>⚠ <b>这个数是按听感定的，不是算出来的</b>：<c>getVolume</c> 的
        /// <c>finalvolume</c> 实测**不反映 3D 距离衰减**（8.9m 与 26.0m 读到同一个值），
        /// 所以拿不到衰减曲线，只能以耳朵为准。嫌吵就调小、嫌轻就调大，一处常量。</para>
        /// </summary>
        private const float VolumeScale = 2.5f;

        // ═══════════════ 乐句 ═══════════════

        /// <summary>玩家进入这个距离才开始吹。</summary>
        private const float TriggerDistance = 30f;

        /// <summary>
        /// 一段乐句吹整首还是只吹一段。
        ///
        /// <para><b>为什么改成整首</b>：最初是随机吹 8~14 个音，实机反馈"听不清"——
        /// 片段太短就只剩个动机。现在一首吹完（《小星星》42 个音 ≈ 14 秒，
        /// 其余 7~10 秒），保证听得出是哪首。</para>
        ///
        /// <para>嫌长就把它设成 <c>false</c>，会退回"随机吹若干音"的行为；
        /// 或者改下面那个区间。</para>
        /// </summary>
        private const bool PlayWholeTune = true;

        /// <summary><see cref="PlayWholeTune"/> 为 <c>false</c> 时，每次吹多少个音（随机区间，含两端）。</summary>
        private const int NotesPerPhraseMin = 8;
        private const int NotesPerPhraseMax = 14;

        /// <summary>每拍多少秒。曲目里的时值都以"拍"为单位，实际秒数 = 拍数 × 本值。</summary>
        private const float SecondsPerBeat = 0.3f;

        /// <summary>两次乐句之间的静默时长（随机区间，秒）。</summary>
        private const float CooldownMin = 1f;
        private const float CooldownMax = 3f;

        // ═══════════════ 旋律 ═══════════════

        /// <summary>
        /// 整首曲子整体升降的半音数。
        ///
        /// <para>曲目按"简谱 1 = 0"记谱，卡祖笛的自然音高也正好是 0，所以默认不移调。</para>
        ///
        /// <para>⚠ <b>不要随手调大</b>：参数量程是 ±24，而曲目里音最高的
        /// 《生日快乐》到 <c>19</c> 半音（高八度的 5）。<see cref="NoteValue"/> 会把超出的音符
        /// 夹回量程，夹完那个音就**不对了**——听起来像跑调，而不会有任何报错。
        /// 想整体升高，安全上限是 <c>24 - 19 = 5</c>。</para>
        /// </summary>
        private const int RootSemitone = 0;

        /// <summary>
        /// 判定"这个参数的单位是半音"的阈值：量程跨度小于一个八度就认为不是半音。
        /// 用于参数探测里按量程认音高参数，见 <see cref="ProbeKazooParameters"/>。
        /// </summary>
        private const float SemitoneRangeThreshold = 12f;

        // ═══════════════ 探测结果（进程内一次） ═══════════════

        private static bool _probed;
        private static string _pitchParameter = FallbackPitchParameter;
        private static string _intensityParameter = FallbackIntensityParameter;

        /// <summary>探测到的音高参数量程，用来把音符夹进合法范围（正常情况用不到）。</summary>
        private static float _pitchMin = -24f;
        private static float _pitchMax = 24f;

        /// <summary>音高验证是否已做过（见 <see cref="VerifyPitchOnce"/>），只做一次。</summary>
        private static bool _pitchVerified;

        private static bool _postFailedLogged;

        /// <summary>已经打过"正在吹哪首"日志的曲名（每首只报一次，不刷屏）。</summary>
        private static readonly HashSet<string> LoggedTunes = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>
        /// 音量诊断还能打几条。每段乐句消耗 1 条（只在句末取，见 <see cref="LogVolume"/>）。
        /// </summary>
        private static int _volumeLogsLeft = 3;

        // ═══════════════ 运行状态 ═══════════════

        private EventInstance? _kazoo;
        private float _cooldownRemaining;

        /// <summary>当前音符还剩多久。</summary>
        private float _noteRemaining;
        /// <summary>本乐句还剩几个音符（含当前这个）。</summary>
        private int _notesLeft;
        /// <summary>本次乐句所吹曲目的音符序列（从头开始截取）。</summary>
        private MusicianTunes.Note[] _phraseNotes;
        /// <summary>下一个要吹的音在 <see cref="_phraseNotes"/> 里的下标。</summary>
        private int _phraseCursor;

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

            // 随机挑一首，**从曲首**吹起。
            // 从曲首是因为"认得出是哪首歌"全靠开头那几个音；随机起点会把它毁掉。
            // 想要更多变化的话，改这里让起点在 [0, 长度-片段] 里随机即可。
            MusicianTunes.Tune tune = MusicianTunes.All[Random.Range(0, MusicianTunes.All.Length)];
            _phraseNotes = tune.Notes;
            _phraseCursor = 0;
            _notesLeft = PlayWholeTune
                ? _phraseNotes.Length
                : Mathf.Min(Random.Range(NotesPerPhraseMin, NotesPerPhraseMax + 1), _phraseNotes.Length);

            audio.SetParameterByName(_pitchParameter, NoteValue(_phraseNotes[0].Semitone));

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
            VerifyPitchOnce(NoteValue(_phraseNotes[0].Semitone));
            LogTuneOnce(tune, _notesLeft);
            _noteRemaining = _phraseNotes[0].Beats * SecondsPerBeat;
        }

        /// <summary>把音符的半音数换算成参数值，并夹进探测到的合法量程。</summary>
        private static float NoteValue(int semitone)
            => Mathf.Clamp(RootSemitone + semitone, _pitchMin, _pitchMax);

        /// <summary>每首曲子只报一次"正在吹哪首"——这是核对曲目有没有按预期被选中的唯一线索。</summary>
        private static void LogTuneOnce(MusicianTunes.Tune tune, int noteCount)
        {
            if (!LoggedTunes.Add(tune.Name)) return;
            Debug.Log($"[EliteEnemies.Musician] 开始吹奏《{tune.Name}》（本次取前 {noteCount} 个音，" +
                      $"全曲 {tune.Notes.Length} 个音）");
        }

        /// <summary>
        /// 第一次设音高时，直接在实例上再设一遍并核对 <c>RESULT</c>。
        ///
        /// <para><b>为什么要这一下</b>：<c>AudioObject.SetParameterByName</c> 是 <c>void</c>，
        /// 返回值被丢掉——参数名写错时**不报错、不留日志**，只是音高不变，从听感上很难和
        /// "音高变了但幅度小"区分开。本工程已经在这条路上栽过两次（名字取不出、名字规则写错），
        /// 所以把"设进去了没有"变成日志里看得见的一行。只核对一次，不刷屏。</para>
        /// </summary>
        private void VerifyPitchOnce(float pitchValue)
        {
            if (_pitchVerified || !_kazoo.HasValue) return;
            _pitchVerified = true;

            FMOD.RESULT result = _kazoo.Value.setParameterByName(_pitchParameter, pitchValue);
            Debug.Log(result == FMOD.RESULT.OK
                ? $"[EliteEnemies.Musician] 音高参数 '{_pitchParameter}' 设置成功（RESULT.OK），旋律会变调"
                : $"[EliteEnemies.Musician] ⚠ 音高参数 '{_pitchParameter}' 设置失败：{result}——" +
                  "旋律**不会**变调，参数名仍然不对");
        }

        /// <summary>
        /// 打一条音量诊断，只为记录这个事件的**内建音量**。
        ///
        /// <para>⚠ <b>它测不出距离衰减，别再指望它</b>（2026-09-18 实测）：</para>
        /// <list type="bullet">
        /// <item><b>句首取样恒为 <c>1.000</c></b>——事件刚 <c>start()</c>、还没经过一次 FMOD 的
        /// <c>system.update()</c>，衰减根本没算上。第一版只取句首，于是得出了
        /// "完全没有距离衰减"的错误结论。</item>
        /// <item><b>句末取样恒为 <c>0.316</c></b>（= 事件内建音量，≈ -10dB），
        /// 8.9m 与 26.0m 读到的是同一个值。也就是说 <c>finalvolume</c>
        /// <b>不反映 3D 距离衰减</b>。</item>
        /// </list>
        /// <para>结论：距离衰减只能**靠耳朵**判断，拿不到曲线。<see cref="VolumeScale"/> 就是据此定的。
        /// 留着这条只为记下内建音量，好在换事件时有个对照。</para>
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
                      $"距离={dist:F1}m（⚠ 实际值不含 3D 距离衰减，见本方法注释）");
        }

        private void TickPhrase(CharacterMainControl character, float deltaTime)
        {
            _noteRemaining -= deltaTime;
            if (_noteRemaining > 0f) return;

            _notesLeft--;
            _phraseCursor++;

            if (_notesLeft <= 0 || _phraseNotes == null || _phraseCursor >= _phraseNotes.Length)
            {
                LogVolume(character);   // 只在句末取——句首那个位置读到的是假象，见方法注释
                StopKazoo();
                _cooldownRemaining = Random.Range(CooldownMin, CooldownMax);
                return;
            }

            MusicianTunes.Note note = _phraseNotes[_phraseCursor];
            AudioObject.GetOrCreate(character.gameObject)
                       .SetParameterByName(_pitchParameter, NoteValue(note.Semitone));
            _noteRemaining = note.Beats * SecondsPerBeat;
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
            _phraseNotes = null;
            _phraseCursor = 0;
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
                    SetPitchRange(-24f, 24f);
                    return true;
                }

                desc.getParameterDescriptionCount(out int count);
                string pitchName = null, intensityName = null;
                float pitchMin = 0f, pitchMax = 0f, pitchDefault = 0f;

                // 名字匹配不上时的兜底：本事件上"量程最宽的那个参数"就是音高。
                // 实测 2026-09-18：两个参数分别是 ±24（音高）与 0~1（强度），量程差得很开，
                // 这个判据不会认错。留着它是因为**名字匹配这条路实测失败过两次**
                // （先是 StringWrapper 取不出名字，后是名字规则写错）——
                // 更重要的是：判定出"哪个是音高"之后**必须把它的真名带出来**，
                // 否则设参数时又得退回那个错的兜底名。
                float widestSpan = -1f, widestMin = 0f, widestMax = 0f, widestDefault = 0f;
                string widestName = null;

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

                    // ⚠ 匹配规则实测纠正过：真实名字是 **'Pitch' / 'Intensity'**，
                    //   **没有** "Kazoo/" 这一层（那个前缀只存在于游戏自己拼的字符串里）。
                    //   所以按名字结尾匹配，Path 与 "Kazoo/Pitch" 两种形态都能覆盖。
                    if (name.EndsWith("Pitch"))
                    {
                        pitchName = name;
                        pitchMin = p.minimum;
                        pitchMax = p.maximum;
                        pitchDefault = p.defaultvalue;
                    }
                    else if (name.EndsWith("Intensity"))
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
                        widestName = name;
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
                    SetPitchRange(-24f, 24f);
                    return true;
                }

                // ★ 一定要把**真名**带出去：走"按量程认定"这条路时 pitchName 是 null，
                //   若不接上 widestName，设参数就会退回兜底名——那样即使认对了量程，
                //   参数名仍然是错的，音高照样不会变。
                if (pitchName != null) _pitchParameter = pitchName;
                else if (widestName != null) _pitchParameter = widestName;

                if (intensityName != null) _intensityParameter = intensityName;

                SetPitchRange(pitchMin, pitchMax);

                Debug.Log($"[EliteEnemies.Musician] 卡祖笛音高参数='{_pitchParameter}'" +
                          $"（{(byRange ? "按量程认定，名字没匹配上" : "按名字匹配")}），" +
                          $"参数量程 [{pitchMin}, {pitchMax}] 默认 {pitchDefault}，" +
                          $"曲目整体移调 {RootSemitone} 半音");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EliteEnemies.Musician] 探测卡祖笛参数失败，将退回游戏自己的写法: {ex.Message}");
                SetPitchRange(-24f, 24f);
                return true;
            }
        }

        /// <summary>
        /// 记下音高参数的合法量程，用来把曲目里的音符夹进去。
        ///
        /// <para>曲目按"简谱 1 = 0"记谱，音域大致 0~11 半音，离实测的 ±24 还有余量，
        /// 正常情况下夹取不会生效——它只是防止 <see cref="RootSemitone"/> 被调得过大时
        /// 把音符设到量程外（那样 FMOD 会直接忽略，听起来就是"某个音丢了"）。</para>
        /// </summary>
        private static void SetPitchRange(float min, float max)
        {
            _pitchMin = min;
            _pitchMax = max;
        }

    }
}
