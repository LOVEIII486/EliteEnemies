namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 音乐家能吹的曲目。
    ///
    /// <para><b>记谱方式：半音 + 拍数，不需要任何乐理假设。</b>
    /// 卡祖笛的音高参数实测就是**半音**（量程 ±24、默认 0），所以一段旋律就是
    /// 一串「相对主音的半音数 + 时值」。</para>
    ///
    /// <para><b>怎么从简谱加一首新曲</b>（不懂乐理也能做）：简谱的
    /// <c>1 2 3 4 5 6 7</c> 对应半音 <c>0 2 4 5 7 9 11</c>，
    /// 数字后面加 <c>-</c> 表示延长一拍。照着抄成 <c>N(半音, 拍数)</c> 即可，
    /// 默认拍数是 1，所以 <c>N(0), N(0), N(7), N(7)</c> 就是简谱的 <c>1 1 5 5</c>。</para>
    ///
    /// <para>⚠ <b>曲目按"公有领域优先"收录</b>：传统曲调（民歌、童谣）随便用；
    /// 有版权的商业作品会明确标注出来，见 <see cref="Lemon"/> 的注释。</para>
    /// </summary>
    internal static class MusicianTunes
    {
        /// <summary>一个音符：相对主音的半音数 + 时值（拍）。</summary>
        internal readonly struct Note
        {
            /// <summary>相对主音的半音数。简谱 <c>1</c> 是 0，<c>2</c> 是 2，依此类推。</summary>
            public readonly int Semitone;

            /// <summary>时值，单位是拍。实际秒数 = 拍数 × 每拍秒数。</summary>
            public readonly float Beats;

            public Note(int semitone, float beats)
            {
                Semitone = semitone;
                Beats = beats;
            }
        }

        /// <summary>一首曲目：名字（只用于日志）+ 音符序列。</summary>
        internal sealed class Tune
        {
            public readonly string Name;
            public readonly Note[] Notes;

            public Tune(string name, Note[] notes)
            {
                Name = name;
                Notes = notes;
            }
        }

        /// <summary>简谱数字 → 半音：1=0 2=2 3=4 4=5 5=7 6=9 7=11。</summary>
        private static Note N(int semitone, float beats = 1f) => new Note(semitone, beats);

        // ═══════════════════════════════════════════════════════════════
        //  ⚠ 曲目**必须**声明在 All 之前。
        //     C# 的静态字段初始化器按**书写顺序**执行，All 若写在前面，
        //     它拿到的会是一串 null——而且是静默的，不会报错。
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 小星星（一闪一闪亮晶晶）。曲调出自法国民谣《Ah! vous dirai-je, maman》（1761），**公有领域**。
        /// <code>
        /// 1 1 5 5 6 6 5- | 4 4 3 3 2 2 1-
        /// 5 5 4 4 3 3 2- | 5 5 4 4 3 3 2-
        /// 1 1 5 5 6 6 5- | 4 4 3 3 2 2 1-
        /// </code>
        /// </summary>
        private static readonly Tune Twinkle = new Tune("小星星", new[]
        {
            N(0), N(0), N(7), N(7), N(9), N(9), N(7, 2f),
            N(5), N(5), N(4), N(4), N(2), N(2), N(0, 2f),
            N(7), N(7), N(5), N(5), N(4), N(4), N(2, 2f),
            N(7), N(7), N(5), N(5), N(4), N(4), N(2, 2f),
            N(0), N(0), N(7), N(7), N(9), N(9), N(7, 2f),
            N(5), N(5), N(4), N(4), N(2), N(2), N(0, 2f),
        });

        /// <summary>
        /// 两只老虎。曲调出自法国轮唱曲《Frère Jacques》，**公有领域**。
        /// <code>
        /// 1 2 3 1 | 1 2 3 1 | 3 4 5 | 3 4 5
        /// 5 6 5 4 3 1 | 5 6 5 4 3 1 | 1 5 1 | 1 5 1-
        /// </code>
        /// </summary>
        private static readonly Tune TwoTigers = new Tune("两只老虎", new[]
        {
            N(0), N(2), N(4), N(0),
            N(0), N(2), N(4), N(0),
            N(4), N(5), N(7),
            N(4), N(5), N(7),
            N(7), N(9), N(7), N(5), N(4), N(0),
            N(7), N(9), N(7), N(5), N(4), N(0),
            N(0), N(7), N(0),
            N(0), N(7), N(0, 2f),
        });

        /// <summary>
        /// 欢乐颂（贝多芬第九交响曲第四乐章主题，1824）。**公有领域**。
        /// <code>
        /// 3 3 4 5 | 5 4 3 2 | 1 1 2 3 | 3. 2 2-
        /// 3 3 4 5 | 5 4 3 2 | 1 1 2 3 | 2. 1 1-
        /// </code>
        /// </summary>
        private static readonly Tune OdeToJoy = new Tune("欢乐颂", new[]
        {
            N(4), N(4), N(5), N(7),
            N(7), N(5), N(4), N(2),
            N(0), N(0), N(2), N(4),
            N(4, 1.5f), N(2, 0.5f), N(2, 2f),
            N(4), N(4), N(5), N(7),
            N(7), N(5), N(4), N(2),
            N(0), N(0), N(2), N(4),
            N(2, 1.5f), N(0, 0.5f), N(0, 2f),
        });

        /// <summary>
        /// 铃儿响叮当（Jingle Bells，James Lord Pierpont，1857）。**公有领域**。
        /// <code>
        /// 3 3 3 | 3 3 3 | 3 5 1 2 3- | 4 4 4 4 4 3 3 | 3 3 2 2 1 2 5-
        /// </code>
        /// </summary>
        private static readonly Tune JingleBells = new Tune("铃儿响叮当", new[]
        {
            N(4), N(4), N(4),
            N(4), N(4), N(4),
            N(4), N(7), N(0), N(2), N(4, 2f),
            N(5), N(5), N(5), N(5), N(5), N(4), N(4),
            N(4), N(4), N(2), N(2), N(0), N(2), N(7, 2f),
        });

        /// <summary>
        /// 生日快乐。曲调出自 Patty &amp; Mildred Hill 的《Good Morning to All》（1893），**公有领域**
        /// （美国 2016 年判决定案；旋律本身 1893 年即已发表）。
        /// <code>
        /// 5 5 6 5 1̇ 7- | 5 5 6 5 2̇ 1̇- | 5 5 5̇ 3̇ 1̇ 7 6 | 4̇ 4̇ 3̇ 1̇ 2̇ 1̇-
        /// </code>
        /// <para>⚠ 第三、四句（<c>7 6</c> 与 <c>4̇ 4̇ 3̇ 1̇ 2̇ 1̇</c>）我把握不如前两句足。</para>
        /// </summary>
        private static readonly Tune HappyBirthday = new Tune("生日快乐", new[]
        {
            N(7), N(7), N(9), N(7), N(12), N(11, 2f),
            N(7), N(7), N(9), N(7), N(14), N(12, 2f),
            N(7), N(7), N(19), N(16), N(12), N(11), N(9),
            N(17), N(17), N(16), N(12), N(14), N(12, 2f),
        });

        /// <summary>
        /// 玛丽有只小羊羔（Mary Had a Little Lamb，1830 年代美国童谣）。**公有领域**。
        /// <code>
        /// 3 2 1 2 | 3 3 3- | 2 2 2- | 3 5 5-
        /// 3 2 1 2 | 3 3 3 3 | 2 2 3 2 | 1-
        /// </code>
        /// </summary>
        private static readonly Tune MaryLamb = new Tune("玛丽有只小羊羔", new[]
        {
            N(4), N(2), N(0), N(2),
            N(4), N(4), N(4, 2f),
            N(2), N(2), N(2, 2f),
            N(4), N(7), N(7, 2f),
            N(4), N(2), N(0), N(2),
            N(4), N(4), N(4), N(4),
            N(2), N(2), N(4), N(2),
            N(0, 4f),
        });

        /// <summary>
        /// 划船歌（Row, Row, Row Your Boat，1852 年美国歌曲）。**公有领域**。
        /// <code>
        /// 1 1 1 2 3 | 3 2 3 4 5 | 1̇ 1̇ 1̇ 5 5 5 | 3 3 3 1 1 1 | 5 4 3 2 1-
        /// </code>
        /// </summary>
        private static readonly Tune RowYourBoat = new Tune("划船歌", new[]
        {
            N(0), N(0), N(0), N(2), N(4),
            N(4), N(2), N(4), N(5), N(7),
            N(12), N(12), N(12), N(7), N(7), N(7),
            N(4), N(4), N(4), N(0), N(0), N(0),
            N(7), N(5), N(4), N(2), N(0, 2f),
        });

        /// <summary>
        /// 伦敦桥（London Bridge Is Falling Down，英国传统童谣）。**公有领域**。
        /// <code>
        /// 5 6 5 4 3 4 5 | 2 3 4 | 3 4 5 | 5 6 5 4 3 4 5 | 2 5 3 1-
        /// </code>
        /// </summary>
        private static readonly Tune LondonBridge = new Tune("伦敦桥", new[]
        {
            N(7), N(9), N(7), N(5), N(4), N(5), N(7),
            N(2), N(4), N(5),
            N(4), N(5), N(7),
            N(7), N(9), N(7), N(5), N(4), N(5), N(7),
            N(2), N(7), N(4), N(0, 2f),
        });

        // ⚠ Lemon（米津玄師，2018）——**有版权的商业作品，不是公有领域**。
        //
        //   模组作者已知悉并接受这个风险（2026-09-18 明确确认），故此处留位。
        //   但**没有填入**：不是版权顾虑，而是我无法凭记忆可靠还原它的音符——
        //   硬写一版出来很可能是一条"不像 Lemon 的旋律"顶着 Lemon 的名字，
        //   从听感上还很难判断是转错了还是音高参数没生效。
        //
        //   填入方式：拿到简谱后照上面的格式加一个 Tune 即可，例如
        //       private static readonly Tune Lemon = new Tune("Lemon", new[] { N(?, ?), ... });
        //   然后把它加进 All。
        //
        //   ⚠ 注意：本模组整体采用的许可证**不覆盖**这段旋律（它本来也不归模组授权）。

        /// <summary>可供随机挑选的曲目池。<b>⚠ 必须声明在所有曲目之后</b>，理由见上面的说明。</summary>
        public static readonly Tune[] All =
        {
            Twinkle, TwoTigers, OdeToJoy, JingleBells,
            HappyBirthday, MaryLamb, RowYourBoat, LondonBridge,
        };
    }
}
