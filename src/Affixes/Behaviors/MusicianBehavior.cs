using Duckov;
using FMOD.Studio;
using EliteEnemies.Core;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 音乐家：玩家靠近时用卡祖笛吹一首曲子，吹完歇一会儿再吹下一首。
    ///
    /// <para><b>声音完全复刻游戏自己的 <c>ItemAgent_Kazoo</c></b>——它把"吹卡祖笛"实现成
    /// 「一个 FMOD 事件 + 两个 RTPC 参数」，跟谁在吹、有没有拿物品毫无关系。
    /// 所以敌人**不需要真的持有卡祖笛**，也不涉及任何反射。</para>
    ///
    /// <para>曲目数据在 <see cref="MusicianTunes"/>。</para>
    /// </summary>
    public class MusicianBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Musician";

        // ═══════════════ 声音 ═══════════════
        //
        // 下面三个名字**是实测确认过的**（2026-09-18，用 FMOD 的
        // EventDescription.getParameterDescriptionByIndex 逐条读出来的），不是猜的：
        //
        //     name='Pitch'      min=-24  max=24  default=0
        //     name='Intensity'  min=0    max=1   default=1
        //
        // ⚠ 事件路径照抄 ItemAgent_Kazoo.cs:9。改错了不会报错，只是没声音。
        //
        // ⚠ 参数必须经 AudioObject.SetParameterByName 直接设，**不能**用 AudioManager.SetRTPC：
        //   后者会给 key 拼一个 "parameter:/" 前缀（AudioManager.cs:703），
        //   而参数真名就是 "Pitch"——那个前缀让原版卡祖笛的音高一直静默失效。
        //   实测证据：用真名设参数返回 RESULT.OK，用带前缀的名字则设不进去。

        /// <summary>卡祖笛的 FMOD 事件路径（不含 <c>event:/</c> 前缀）。</summary>
        private const string KazooEvent = "SFX/Special/Kazoo";

        /// <summary>音高参数。<b>单位是半音</b>，量程 ±24。</summary>
        private const string PitchParameter = "Pitch";

        /// <summary>强度/音量参数，量程 0~1，默认 1（所以不设也有声）。</summary>
        private const string IntensityParameter = "Intensity";

        /// <summary>
        /// 相对原版音量。走 <c>EventInstance.setVolume</c>（按实例缩放），
        /// 不动 <c>bus:/Master/SFX</c>——那是全游戏音效的总线，碰它会连枪声脚步一起改小。
        ///
        /// <para><b>取值史：<c>0.45 → 1.0 → 2.5 → 2.0 → 1.0</c>，来回拉锯的根因是
        /// 3D 距离衰减的动态范围太宽。</b>同一次演奏，贴脸时和二十几米外的响度差得很多，
        /// 而 <c>setVolume</c> 只是一个**常量乘数**，必然顾此失彼：
        /// 推到远处听得见，近处就吵；压到近处不吵，远处又没了。
        /// 这四次调整里有三次都是在为前一次的补偿买单。</para>
        ///
        /// <para>现在取 <c>1.0</c>——即原版卡祖笛的音量，也就是玩家自己拿着吹时听到的响度。
        /// <b>如果之后又觉得远处听不见，正确的杠杆是 <see cref="TriggerDistance"/>
        /// （把触发距离收小），不是把这个数再推上去</b>：推上去只是把"远处勉强可闻"
        /// 换回"近处吵得难受"，同一个循环会再来一遍。</para>
        ///
        /// <para>⚠ 这个数是**按听感定的，不是算出来的**：<c>getVolume</c> 的
        /// <c>finalvolume</c> 实测**不反映 3D 距离衰减**（8.9m 与 26.0m 读到同一个值），
        /// 拿不到衰减曲线，所以只能以耳朵为准。</para>
        /// </summary>
        private const float VolumeScale = 1f;

        // ═══════════════ 乐句 ═══════════════

        /// <summary>
        /// 玩家进入这个距离才开始吹。
        ///
        /// <para><b>它和 <see cref="VolumeScale"/> 是一对，调一个必须想另一个。</b>
        /// 两者合起来决定"玩家实际听到什么"：触发距离决定**什么时候开始响**，
        /// 音量决定**响多大**。而 3D 距离衰减夹在中间，所以只顾一头必然反复：
        /// 触发距离拉大 → 远处听不清 → 推高音量 → 近处吵 → 压低音量 → 远处又没了。</para>
        ///
        /// <para><b>30m 是一次误判</b>：当时的理由是"远距离会衰减，所以早点开始吹"，
        /// 但实际效果是**从 30m 起就在吹、而那个距离根本听不清**——白白制造了
        /// "得把音量推上去"的压力，把问题推到了音量那一头。收到 20m 之后，
        /// 它只在玩家本来就听得见的范围内响，<c>VolumeScale = 1.0</c> 才站得住。</para>
        ///
        /// <para>再要调的话，**先动这个数、别先动音量**——这是本类里唯一
        /// 能"缩小需要覆盖的距离范围"的旋钮，而音量只能整体推高推低。</para>
        /// </summary>
        private const float TriggerDistance = 20f;

        /// <summary>
        /// 一段乐句吹整首还是只吹一段。
        ///
        /// <para><b>为什么是整首</b>：最初随机吹 8~14 个音，实机反馈"听不清"——
        /// 片段太短就只剩个动机。整首吹完才保证听得出是哪首（《小星星》约 14 秒，
        /// 其余 7~10 秒）。嫌长就设成 <c>false</c>，退回下面的随机区间。</para>
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

        /// <summary>
        /// 整首曲子整体升降的半音数。曲目按"简谱 1 = 0"记谱，卡祖笛的自然音高也是 0，故默认不移调。
        ///
        /// <para>⚠ 安全上限是 <c>5</c>：最高音是《生日快乐》的 <c>19</c> 半音，而参数量程是 ±24，
        /// 调到超出量程的音会被 FMOD 直接忽略，听起来就是"某个音丢了"。</para>
        /// </summary>
        private const int RootSemitone = 0;

        // ═══════════════ 运行状态 ═══════════════

        private EventInstance? _kazoo;

        /// <summary>本次乐句所在的 AudioObject，缓存下来免得每个音符都 GetComponent 一次。</summary>
        private AudioObject _audio;

        private float _cooldownRemaining;

        /// <summary>当前音符还剩多久。</summary>
        private float _noteRemaining;

        /// <summary>本乐句还剩几个音符（含当前这个）。</summary>
        private int _notesLeft;

        /// <summary>本次乐句所吹曲目的音符序列。</summary>
        private MusicianTunes.Note[] _phraseNotes;

        /// <summary>下一个要吹的音在 <see cref="_phraseNotes"/> 里的下标。</summary>
        private int _phraseCursor;

        /// <summary>播放失败只报一次——它多半是"关卡还没初始化完"，不该每次冷却都刷屏。</summary>
        private static bool _postFailedLogged;

        // ═══════════════════════════════════════════════════════════════

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            // ⚠ **时间被冻结时必须收声**，否则会留下一段停不下来的蜂鸣。
            //
            // 本类的音符计时是 `_noteRemaining -= deltaTime`，而这里传进来的 deltaTime 是
            // `Time.deltaTime`（EliteBehaviorComponent.cs:225）。时间一冻结它就是 0，
            // 于是音符永远减不完、`OnRefreshed` 也不再被走到——卡祖笛卡在**同一个音高**
            // 上一直吹。玩家听到的就是蜂鸣。
            //
            // 判据用 `Time.timeScale <= 0` 而不是某个具体开关：时间被压到 0 的来源有两处
            // （ESC 暂停 `GameManager.Paused`、拍照模式 `CameraMode.Active`，
            //  见 TimeScaleManager.cs:24-30），而这个条件**恰好就是**计时失效的充要条件——
            // 将来再多一种冻结来源也自动覆盖。
            //
            // 注意不能用 `Time.unscaledDeltaTime` 绕过去：那会让卡祖笛在暂停菜单里继续演奏。
            if (Time.timeScale <= 0f)
            {
                StopKazoo();
                return;
            }

            // ⚠ 用**最近玩家**（本机 + 远端），不是 `CharacterMainControl.Main`。
            //   后者是「本机玩家」——联机下判定在主机上跑，客机玩家是另一个角色对象，
            //   写死 Main 会让这个词条**只对主机玩家的靠近有反应**。
            var nearest = EliteEnemyCore.FindNearestPlayer(character.transform.position, out float nearestDist);
            if (nearest == null) return;

            float distSqr = nearestDist * nearestDist;
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

            // 随机挑一首，**从曲首**吹起：认得出是哪首歌全靠开头那几个音，随机起点会把它毁掉。
            // 想要更多变化，让起点在 [0, 长度-片段] 里随机即可。
            MusicianTunes.Tune tune = MusicianTunes.All[Random.Range(0, MusicianTunes.All.Length)];
            _phraseNotes = tune.Notes;
            _phraseCursor = 0;
            _notesLeft = PlayWholeTune
                ? _phraseNotes.Length
                : Mathf.Min(Random.Range(NotesPerPhraseMin, NotesPerPhraseMax + 1), _phraseNotes.Length);

            _audio = AudioObject.GetOrCreate(go);

            // 顺序有讲究：SetParameterByName 会把参数**缓存**在这个 AudioObject 上，
            // 随后 Post 的 ApplyParameters 会把它带给新事件。
            _audio.SetParameterByName(IntensityParameter, 1f);
            _audio.SetParameterByName(PitchParameter, NoteValue(_phraseNotes[0].Semitone));

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
            _noteRemaining = _phraseNotes[0].Beats * SecondsPerBeat;
        }

        private void TickPhrase(CharacterMainControl character, float deltaTime)
        {
            _noteRemaining -= deltaTime;
            if (_noteRemaining > 0f) return;

            _notesLeft--;
            _phraseCursor++;

            if (_notesLeft <= 0 || _phraseNotes == null || _phraseCursor >= _phraseNotes.Length)
            {
                StopKazoo();
                _cooldownRemaining = Random.Range(CooldownMin, CooldownMax);
                return;
            }

            MusicianTunes.Note note = _phraseNotes[_phraseCursor];
            _audio.SetParameterByName(PitchParameter, NoteValue(note.Semitone));
            _noteRemaining = note.Beats * SecondsPerBeat;
        }

        /// <summary>把音符的半音数换算成参数值。</summary>
        private static float NoteValue(int semitone) => RootSemitone + semitone;

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
            _audio = null;
            _noteRemaining = 0f;
            _notesLeft = 0;
            _phraseNotes = null;
            _phraseCursor = 0;
        }

        private bool IsPlaying() => _kazoo.HasValue && _kazoo.Value.isValid();
    }
}
