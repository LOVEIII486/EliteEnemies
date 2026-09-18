namespace EliteEnemies.Localization
{
    /// <summary>
    /// 一段**跟随语言切换**的本地化文本：存的是键，不是译文。
    ///
    /// <para><b>要解决什么</b>：把 <c>LocalizationManager.GetText</c> 的结果存进字段，
    /// 就等于把当时那门语言的译文**焊死**了——之后无论切多少次语言，读到的都是旧文本。
    /// 本工程原先在词条表、combo 表、行为类的 <c>Lazy&lt;string&gt;</c> 三处都有这个毛病。</para>
    ///
    /// <para><b>为什么不是「每次访问都查表」</b>：那确实能避开冻结，但代价是把一次字典查找
    /// 摊到每一次读取上。<see cref="Value"/> 的稳态开销是<b>一次 int 比较 + 一次字段读</b>——
    /// 没有分配、没有查表。只有语言<b>真的变过</b>之后的那第一次读取才会重查一次。</para>
    ///
    /// <para><b>为什么不做「换语言时回调刷新」</b>：那需要每个持有者都记得登记自己，
    /// 而漏登记是无声的（本工程已经栽过一次——<c>OnLanguageChanged</c> 就漏掉了词条表与血条）。
    /// 本类型**结构上自愈**：新增任何本地化字段都自动正确，不需要任何人记得做什么。</para>
    ///
    /// <para>⚠ 换代依据是 <see cref="LocalizationManager.LanguageVersion"/>。该值必须在
    /// **本地化就绪时**也自增一次，否则在就绪前被读过的实例会把兜底文本连同初始版本号
    /// 一起缓存住，变成另一种形式的冻结。</para>
    ///
    /// <para><b>什么时候<u>不</u>该用它</b>：本类型的价值在于「同一份数据被反复读」时
    /// 用一次 int 比较换掉一次查表。<b>读得越少，它越没有意义</b>——一个一次事件才读一次的
    /// 弹幕文本，缓存下来的字符串几乎不会被第二次用到，白占一个字段。</para>
    ///
    /// <para>判据是「这个字符串会不会被同一份数据多次读取」：</para>
    /// <list type="bullet">
    /// <item><b>会</b>（词条表、combo 表、血条前缀这类长期存活、被反复读的）→ 用本类型。</item>
    /// <item><b>不会</b>（弹幕文本、召唤命名：一次事件取一次就吐出去）→ 直接写
    /// <c>private string X =&gt; LocalizationManager.GetText(...)</c> 属性即可。
    /// 同样跟随语言切换，还少一个字段。本工程的 21 处行为类弹幕文本是这么写的。</item>
    /// </list>
    /// </summary>
    public struct LocalizedText
    {
        private readonly string _key;
        private readonly string _fallback;

        private string _cached;
        private int _cachedVersion;

        /// <param name="key">本地化键。</param>
        /// <param name="fallback">键不存在时返回什么；为 null 时 <c>GetText</c> 会返回键名本身。</param>
        public LocalizedText(string key, string fallback = null)
        {
            _key = key;
            _fallback = fallback;
            _cached = null;
            // 用一个**不可能等于**任何真实版本号的值开头，保证首次读取必定解析。
            // （LanguageVersion 从 1 起，见 LocalizationManager。）
            _cachedVersion = 0;
        }

        /// <summary>当前语言下的文本。稳态下零分配、零查表。</summary>
        public string Value
        {
            get
            {
                int version = LocalizationManager.LanguageVersion;
                if (_cachedVersion != version)
                {
                    _cached = LocalizationManager.GetText(_key, _fallback);
                    _cachedVersion = version;
                }
                return _cached;
            }
        }

        public override string ToString() => Value ?? string.Empty;
    }
}
