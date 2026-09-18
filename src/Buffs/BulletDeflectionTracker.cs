using System.Collections.Generic;

namespace EliteEnemies.Buffs
{
    /// <summary>
    /// 记录当前哪些角色处于「子弹会被偏转」状态，供 <c>ProjectilePatches</c> 查询。
    ///
    /// <para>登记与注销分别挂在 <c>DistortionBuff</c> 的 <c>OnApplied</c> / <c>OnRemoved</c> 上，
    /// 而后者由 <c>CharacterBuffManager.onRemoveBuff</c> 驱动——**成对**，所以正常流程不会残留。</para>
    ///
    /// <para>⚠ 唯一的漏网情形是"角色整体被销毁而没走 RemoveBuff"（场景拆除）。
    /// 那时集合里会留下已销毁的引用，因此本类提供 <see cref="Clear"/>，
    /// 由 <c>ModBehaviour.OnSceneUnloaded</c> 调用——与 <c>EliteLootSystem</c> /
    /// <c>EliteGarbledLabel</c> 的缓存清理放在一起，清的是同一类跨场景残留状态。</para>
    /// </summary>
    public sealed class BulletDeflectionTracker
    {
        private static BulletDeflectionTracker _instance;

        public static BulletDeflectionTracker Instance => _instance ?? (_instance = new BulletDeflectionTracker());

        private readonly HashSet<CharacterMainControl> _distorted = new HashSet<CharacterMainControl>();

        public void Register(CharacterMainControl character)
        {
            if (character != null) _distorted.Add(character);
        }

        public void Unregister(CharacterMainControl character)
        {
            if (character != null) _distorted.Remove(character);
        }

        public bool IsDistorted(CharacterMainControl character)
        {
            return character != null && _distorted.Contains(character);
        }

        public void Clear()
        {
            _distorted.Clear();
        }
    }
}
