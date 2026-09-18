using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EliteEnemies.Localization
{
    /// <summary>
    /// 模组本地化：加载自己的 CSV，**并把全部文本推入游戏自带的本地化器**。
    ///
    /// <para><b>为什么必须推</b>：游戏的 <c>SodaCraft.Localizations.LocalizationManager</c>
    /// 只装它自己的文本；模组的 CSV 由本类的 <c>CSVFileLocalizor</c> 读，
    /// **两者互不相通**——游戏既没有给模组注册文本源的 API，我们的 provider 也不会被它看见。
    /// 于是任何**由游戏侧显示**的键（Buff 的 <c>displayName</c>/<c>description</c>、
    /// 物品的 <c>FromInfoKey</c>、预设的 <c>nameKey</c>…）都会显示成 <c>*键名*</c>。
    ///
    /// <para><b>推入之后</b>：游戏自己的 <c>GetPlainText</c> 就能解析我们的全部键，
    /// 于是「谁能解析这个键」只有一个答案——<b>不需要再去改游戏显示文本的函数</b>。</para>
    ///
    /// <para>这是第三方框架 Feather 走的同一条路（<c>FastModLib/I18n.cs:114</c> 逐键
    /// <c>SetOverrideText</c>）。差别只在清理策略：它推之前什么都不清，于是**新语言里缺的键**
    /// 会残留旧语言的文本。本类的 <see cref="PushToGame"/> 会摘掉这**一类**键（差集），
    /// 而不是无条件全清——两种写法的取舍见该方法的注释。</para>
    ///
    /// <para><b>职责边界</b>：本类只管「加载 / 建索引 / 推给游戏 / 从游戏读 / 跟随语言」。
    /// <b>值与键的校验都不归它</b>——键的静态校验在 <c>tools/check-localization.sh</c>，
    /// 模块级自检在各模块自己（如 <c>EliteBuffRegistry.ValidateLocalization</c>），
    /// ModSetting 的界面重建在 <c>SettingsUIRegistration.Reregister</c>。</para>
    /// </summary>
    public static class LocalizationManager
    {
        private const string LogTag = "[EliteEnemies.Localization]";
        private const string LocalizationFolderName = "Localization";

        /// <summary>一个语言的文件与它的键集合。键集合直接取自 provider 内部那个字典（见 <see cref="LoadAndSetLanguage"/>）。</summary>
        private sealed class LanguageData
        {
            public CSVFileLocalizor Provider;
            public HashSet<string> Keys;
        }

        // 缓存已加载的语言数据，避免重复IO
        private static readonly Dictionary<SystemLanguage, LanguageData> LoadedProviders
            = new Dictionary<SystemLanguage, LanguageData>();

        /// <summary>
        /// 本模组推给游戏本地化器的 **键 → 我们最后推的那份文本**。
        /// **重推前必须按它清理**，否则会残留旧语言。
        ///
        /// <para><b>为什么存文本，而不只是存键</b>：撞车告警要靠它判「现有文本是不是我们自己写的」。
        /// 换语言时 <c>overrideTexts</c> 里装的**正是我们上一轮推的文本**（差集清理只摘掉
        /// 新语言里没有的键，两种语言都有的键原样留着），所以只按「这个键我推过」判断，
        /// 就会把上一门语言的**自己**当成别的模组——实测切一次语言刷 188 行假告警。
        /// 存下文本才能分开这两种情况：</para>
        /// <list type="bullet">
        /// <item>现有文本 == 我们推的那份 → 上一门语言的自己，**不是撞车**</item>
        /// <item>现有文本 ≠ 我们推的那份（或这个键我们从没推过）→ **真被别的来源占了**</item>
        /// </list>
        ///
        /// <para>只用「这个键在不在集合里」也能消掉假告警，但那会连
        /// 「别的模组事后覆盖了我们的键」一起漏掉——那恰是这个告警本来要抓的东西。</para>
        /// </summary>
        private static readonly Dictionary<string, string> PushedTexts = new Dictionary<string, string>(StringComparer.Ordinal);

        private static SystemLanguage _currentLanguage;
        private static LanguageData _current;
        private static string _modDirectory; // 缓存模组路径供Refresh使用
        private static bool _isInitialized = false;

        /// <summary>
        /// 语言换代计数。每次「当前语言确实变了」自增一次，供 <see cref="LocalizedText"/>
        /// 判断自己缓存的那份文本是否已经过期。
        ///
        /// <para><b>为什么用计数器而不是回调</b>：回调要求每个持有本地化文本的地方都记得
        /// 登记自己，而漏登记是无声的——本工程已经栽过一次（<c>OnLanguageChanged</c> 漏掉了
        /// 词条表与血条两层）。计数器把判断挪到读取方：谁读谁比一下，不需要任何人记得做什么。</para>
        ///
        /// <para>⚠ <b>初值是 1，不是 0</b>：<see cref="LocalizedText"/> 用 0 当「从未解析过」的哨兵值，
        /// 所以这里必须从 1 起，否则首次读取会被误判成「已是最新」而返回 null。</para>
        ///
        /// <para>⚠ <b>初始化成功时必须也自增一次</b>：本地化就绪前读过的实例会把兜底文本
        /// 缓存住，若不换代，那份兜底就永远不会被真实译文替换——即 AGENT.md §3.5 那颗雷
        /// 换了个形式复发。</para>
        /// </summary>
        public static int LanguageVersion { get; private set; } = 1;

        // ==================== 初始化 ====================

        public static void Initialize(string modDirectory)
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"{LogTag} 本地化系统已初始化，跳过重复初始化");
                return;
            }

            _modDirectory = modDirectory;

            try
            {
                DetermineCurrentLanguage();

                // 尝试加载当前语言，如果失败则执行后备逻辑
                if (!LoadAndSetLanguage(_currentLanguage))
                {
                    Debug.LogWarning($"{LogTag} 无法加载语言 {_currentLanguage}，尝试后备语言...");

                    // 优先使用英文作为后备
                    if (!LoadAndSetLanguage(SystemLanguage.English))
                    {
                        // 英文也失败了，尝试中文作为最后的保底
                        if (!LoadAndSetLanguage(SystemLanguage.ChineseSimplified) &&
                            !LoadAndSetLanguage(SystemLanguage.Chinese))
                        {
                            Debug.LogError($"{LogTag} 严重错误：无法加载任何语言文件（English/Chinese）！");
                        }
                    }
                }

                _isInitialized = true;

                // 本地化就绪 → **必须换代**：就绪前被读过的 LocalizedText 缓存的是兜底文本，
                // 不换代的话那份兜底永远不会被真实译文替换。
                LanguageVersion++;

                PushToGame();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 初始化失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 刷新本地化数据。语言真的变了才会重新加载，并**重推给游戏本地化器**。
        /// </summary>
        public static void Refresh()
        {
            if (!_isInitialized) return;

            try
            {
                var oldLanguage = _currentLanguage;
                DetermineCurrentLanguage();

                // 只有语言确实改变了，或者当前没有Provider时才重新加载
                if (_currentLanguage != oldLanguage || _current == null)
                {
                    if (LoadAndSetLanguage(_currentLanguage))
                    {
                        Debug.Log($"{LogTag} 语言已刷新: {_currentLanguage}");
                    }
                    else
                    {
                        // 如果新语言加载失败，保持旧的Provider或尝试后备
                        Debug.LogWarning($"{LogTag} 切换到 {_currentLanguage} 失败，尝试使用后备语言");
                        if (_current == null)
                        {
                            LoadAndSetLanguage(SystemLanguage.English);
                        }
                    }
                }

                // 语言**确实变了**才换代。没变就不动——否则每次 Refresh 都会让全工程的
                // LocalizedText 白重查一遍（虽然无害，但那是没有理由的开销）。
                if (_currentLanguage != oldLanguage) LanguageVersion++;

                // ★ 重推：游戏的 override 不会随语言自动更新
                //
                // 这里**不**再调 provider.BuildDictionary()：本次加载要么刚 new 过
                // （构造器已解析），要么命中缓存（字典早已建好）——两种情况文件都没变，
                // 重解析纯属多余的第三次读取。
                // 代价：会话中途手改 CSV 不会热重载（那是开发期的便利，不是运行期需求）。
                PushToGame();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 刷新失败: {ex.Message}");
            }
        }

        public static void Cleanup()
        {
            ClearGameOverrides();
            LoadedProviders.Clear();
            _current = null;
            _modDirectory = null;
            _isInitialized = false;

            // 换代：清理后 _current 已空，任何 LocalizedText 若继续供旧译文就是脏读。
            // 下次 Initialize 还会再自增一次，两次之间读到的只会是兜底值。
            LanguageVersion++;
        }

        // ==================== 公共接口 ====================

        /// <summary>
        /// 取文本。**值从游戏本地化器读**，本模组的表只当"这个键存不存在"的索引。
        ///
        /// <para><b>为什么不在自己的表里读值</b>：那样就有两条独立的值路径——
        /// 我们显示一份、游戏显示另一份，两边只在"都成功"时才一致。
        /// 统一从游戏读之后，<b>"我们显示的"与"游戏显示的"永远是同一次推送的内容</b>，
        /// 整体逻辑只剩一条路径（ModSetting 是唯一例外，它不解析 key，见下面的注释）。</para>
        /// </summary>
        public static string GetText(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;

            // 表里没有 = 这个键不在我们的 CSV 里 → 交给调用方的兜底
            if (!HasKey(key)) return fallback ?? key;

            var text = SodaCraft.Localizations.LocalizationManager.GetPlainText(key);

            // 游戏解析不到时返回的兜底是 **"*" + key + "*"**（LocalizationManager.cs:122）。
            // 精确按这个形状比对，而不是"首尾是星号"——否则译文本身写成 `*注意*` 会被误判。
            // 表里有、游戏里却没有 —— 只可能是推送漏掉了它。**必须报出来**，
            // 否则表现就是界面上凭空出现 `*EliteEnemies_Affix_X*` 而没人知道为什么。
            if (IsGameFallbackFor(text, key))
            {
                Debug.LogError($"{LogTag} 键 '{key}' 在表里存在，但游戏本地化器解析不到——" +
                               "推送漏了它（PushToGame 失败？）");
                return fallback ?? key;
            }

            return text;
        }

        /// <summary>
        /// 判断 <paramref name="text"/> 是不是游戏「解析不到这个键」时给的兜底形状
        /// （<c>"*" + key + "*"</c>，见游戏 <c>LocalizationManager.cs:122</c>）。
        ///
        /// <para>逐字符比较而**不是** <c>text == "*" + key + "*"</c>：后者每次调用都要
        /// 拼接并分配一个新字符串，而这里是<b>全部文本读取的公共路径</b>。
        /// 弹幕文本、血条前缀、设置界面描述都从这里过，没有理由为一次判定产生垃圾。</para>
        /// </summary>
        private static bool IsGameFallbackFor(string text, string key)
        {
            if (text == null || text.Length != key.Length + 2) return false;
            if (text[0] != '*' || text[text.Length - 1] != '*') return false;
            return string.CompareOrdinal(text, 1, key, 0, key.Length) == 0;
        }

        /// <summary>当前语言里有没有这个键。供各模块做启动期自检。</summary>
        public static bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key) || _current?.Keys == null) return false;
            return _current.Keys.Contains(key);
        }

        // ==================== 推入游戏本地化器 ====================

        /// <summary>
        /// 把当前语言的全部文本推入游戏本地化器（<c>overrideTexts</c>）。
        ///
        /// <para><b>清理策略：只清「上一轮推过、这次不再有」的键（差集）</b>——
        /// 既不是"全清再推"，也不是"什么都不清"。三种写法各有一个洞：</para>
        /// <list type="bullet">
        /// <item><b>什么都不清</b>（Feather 的写法）：跨语言键集合不一致时
        /// （A 语言有、B 语言没有），切到 B 后那些键**残留 A 语言的文本**。
        /// 机制上确实存在，但**只在"文件不完整 + 运行时切语言"两个条件同时成立时才显现**——
        /// 文件齐的时候直接覆盖完全等价。</item>
        /// <item><b>全清再推</b>：能防上面那条，但**推送中途失败时我们的文本会整片消失**
        /// （已清掉的键没人补回来，界面全变 <c>*键名*</c>）。这个洞**任何一次异常都会触发**，
        /// 比它要防的那个大。</item>
        /// <item><b>只清差集（本实现）</b>：要推的键全程留在字典里，不存在"文本消失窗口"；
        /// 而上一轮有、这次没有的键会被摘掉。两个洞都没有。</item>
        /// </list>
        ///
        /// <para><b>撞键会告警</b>：<c>overrideTexts</c> 是全局字典，覆盖是静默的。
        /// 要推的键若已被别的来源占着，说明键名可能与别的模组撞车——**报出来**，
        /// 而不是悄悄覆盖。</para>
        ///
        /// <para>⚠ <b>「别的来源」必须排除我们自己</b>。换语言时 <c>overrideTexts</c> 里装的
        /// 正是我们上一轮推的文本，只按「这个键存在」判断的话，切一次语言就会把
        /// 上一门语言的自己报成 188 次「与别的模组撞车」——**告警一旦长期是噪音，
        /// 真撞车就没人看了**。判据见 <see cref="PushedTexts"/>。</para>
        /// </summary>
        public static void PushToGame()
        {
            var provider = _current?.Provider;
            var keys = _current?.Keys;
            if (provider == null || keys == null || keys.Count == 0)
            {
                Debug.LogError($"{LogTag} 没有可推送的本地化数据（provider 或键集合为空）——" +
                               "游戏侧将无法解析本模组的键，界面会显示 *键名*");
                return;
            }

            // ── ① 先摘掉"上一轮推过、这一轮不再有"的键（差集） ──
            //    见上面类型注释里对三种写法的取舍。正常情况下这一步是空的。
            List<string> stale = null;
            foreach (var key in PushedTexts.Keys)
            {
                if (keys.Contains(key)) continue;
                (stale ?? (stale = new List<string>())).Add(key);
            }
            if (stale != null)
            {
                foreach (var key in stale)
                {
                    SodaCraft.Localizations.LocalizationManager.RemoveOverrideText(key);
                    PushedTexts.Remove(key);
                }
                Debug.LogWarning($"{LogTag} 有 {stale.Count} 个键在本语言里不存在（多半是漏译），" +
                                 "已撤掉它们的上一轮文本，避免残留旧语言");
            }

            // ── ② 再推当前语言的全部键 ──
            int pushed = 0;
            int conflicts = 0;

            foreach (var key in keys)
            {
                string text;
                try
                {
                    text = provider.Get(key);
                }
                catch
                {
                    continue;
                }
                if (string.IsNullOrEmpty(text)) continue;

                // 「已占用」的判据必须排除**我们自己**上一轮推的文本。
                // 游戏侧 TryGetOverrideText 就是 overrideTexts.TryGetValue
                // （游戏 LocalizationManager.cs:46-48），键不存在时返回 false。
                PushedTexts.TryGetValue(key, out string oursLastPushed);
                bool occupiedByOther =
                    SodaCraft.Localizations.LocalizationManager.TryGetOverrideText(key, out var existing)
                    && existing != text          // 文本一样就无所谓覆盖，不算撞车
                    && existing != oursLastPushed; // 是我们自己上一门语言的文本 → 不是撞车

                if (occupiedByOther)
                {
                    conflicts++;
                    Debug.LogWarning($"{LogTag} 键 '{key}' 已被别的来源占用（现有文本：'{existing}'），" +
                                     "本次会覆盖它——键名疑似与其他模组撞车");
                }

                SodaCraft.Localizations.LocalizationManager.SetOverrideText(key, text);
                PushedTexts[key] = text;   // 记下我们推了什么，供下一轮区分自己与别人
                pushed++;
            }

            Debug.Log($"{LogTag} 已向游戏本地化器推送 {pushed} 条文本（语言 {_currentLanguage}）" +
                      (conflicts > 0 ? $"；⚠ {conflicts} 条与其他来源撞车（见上方告警）" : string.Empty));
        }

        /// <summary>撤掉本模组推给游戏本地化器的全部文本。</summary>
        public static void ClearGameOverrides()
        {
            if (PushedTexts.Count == 0) return;

            foreach (var key in PushedTexts.Keys)
            {
                SodaCraft.Localizations.LocalizationManager.RemoveOverrideText(key);
            }
            PushedTexts.Clear();
        }

        // ==================== 内部逻辑 ====================

        private static void DetermineCurrentLanguage()
        {
            _currentLanguage = SodaCraft.Localizations.LocalizationManager.Initialized
                ? SodaCraft.Localizations.LocalizationManager.CurrentLanguage
                : Application.systemLanguage;
        }

        /// <summary>
        /// 尝试加载并设置指定的语言
        /// </summary>
        /// <returns>是否成功设置了Provider</returns>
        private static bool LoadAndSetLanguage(SystemLanguage language)
        {
            // 1. 检查缓存
            if (LoadedProviders.TryGetValue(language, out var cached))
            {
                _current = cached;
                return true;
            }

            // 2. 检查文件是否存在
            string fileName = GetLanguageFileName(language);
            string filePath = Path.Combine(_modDirectory, LocalizationFolderName, fileName);

            if (!File.Exists(filePath))
            {
                // 针对中文的特殊处理：如果请求的是Chinese但没找到，尝试ChineseSimplified
                if (language == SystemLanguage.Chinese)
                {
                    string simPath = Path.Combine(_modDirectory, LocalizationFolderName, "ChineseSimplified.csv");
                    if (File.Exists(simPath))
                    {
                        filePath = simPath;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            // 3. 加载文件
            try
            {
                var provider = new CSVFileLocalizor(filePath);

                var data = new LanguageData
                {
                    Provider = provider,
                    // ★ 键集合直接取自 provider 内部那个字典，**不再自己扫一遍 CSV**。
                    //   CSVFileLocalizor 只暴露 Get/GetEntry/HasKey、没有枚举 API，
                    //   所以本工程把 TeamSoda.MiniLocalizor 加进了 Publicize 名单
                    //   （见 csproj 的 Publicize 段），换来"同一个文件只读一遍"。
                    Keys = new HashSet<string>(provider.dic.Keys, StringComparer.Ordinal)
                };
                LoadedProviders[language] = data;
                _current = data;

                Debug.Log($"{LogTag} 已加载并切换语言: {fileName}（{data.Keys.Count} 条）");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 加载文件失败 {fileName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将系统语言映射到文件名
        /// </summary>
        private static string GetLanguageFileName(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    return "ChineseSimplified.csv";
                case SystemLanguage.ChineseTraditional:
                    return "ChineseTraditional.csv";
                case SystemLanguage.English:
                    return "English.csv";
                case SystemLanguage.Japanese:
                    return "Japanese.csv";
                case SystemLanguage.Korean:
                    return "Korean.csv";
                case SystemLanguage.Russian:
                    return "Russian.csv";
                default:
                    return $"{language}.csv";
            }
        }
    }
}
