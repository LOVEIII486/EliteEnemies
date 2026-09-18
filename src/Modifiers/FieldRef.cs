namespace EliteEnemies.Modifiers
{
    /// <summary>
    /// 字段引用：拿到目标的**字段本身**（而不是它的值），可读可写。
    ///
    /// <para>用途是把「访问哪个字段」从运行时字符串变成**编译期绑定**：
    /// 字段改名会直接编译报错，而不是像反射那样静默跳过
    /// （反射版遇到找不到的字段只会 <c>if (fInfo == null) return;</c> 悄悄什么都不做）。</para>
    ///
    /// <para>用法：<c>field(ai) = 1f;</c> 写，<c>var v = field(ai);</c> 读。
    /// 零反射、零装箱（委托是引用类型，作为字典 key 不装箱）。</para>
    /// </summary>
    public delegate ref TField FieldRef<in TObj, TField>(TObj target);
}
