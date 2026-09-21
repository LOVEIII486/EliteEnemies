using System.Collections;
using Duckov.Buffs;
using ECM2;
using ItemStatsSystem;
using UnityEngine;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 「作用于玩家」的效果体。**抽出来放这里，供两条路径共用**：
    /// ① 本机直接执行（单机、以及联机时主机自己的玩家）；
    /// ② 联机下由**受害者那台机器**执行（效果经 <c>CoopPlayerEffect</c> 转交过去）。
    ///
    /// <para><b>为什么必须共用</b>：联机下这些效果<b>不能</b>由主机替远端玩家做——
    /// 主机上那个只是<b>复制体</b>，改它到不了真人，下一帧还会被客机的同步覆盖回去。
    /// 于是同一件事有了**两个执行地点**，而两份实现必然漂移。
    /// 所以效果体只写一遍，两条路径都调它。</para>
    ///
    /// <para>放 <c>Affixes</c> 而不是 <c>Coop</c>：它们是**词条的效果**，与联机无关；
    /// 联机只是"换个地方执行"。依赖方向也因此是 <c>Coop → Affixes</c>（允许的方向）。</para>
    /// </summary>
    internal static class PlayerEffectActions
    {
        // ===== 击退 =====

        private const float KnockbackUpForce = 7f;      // 垂直力（原 KnockbackBehavior.KnockbackForce）
        private const float KnockbackHorizontal = 15f;  // 横向力（原 KnockbackBehavior.HorizontalForce）
        private const float KnockbackGroundPause = 0.3f;

        /// <summary>
        /// 把玩家朝 <paramref name="scaledHorizontalDir"/> 方向击飞。
        ///
        /// <para>⚠ <b>传进来的方向必须是"已乘过距离倍率"的</b>——那个倍率依赖
        /// 精英到玩家的距离，只有在主机上算得出来。在客户端用基准力即可，
        /// 倍率已经含在参数里了。</para>
        /// </summary>
        public static void Knockback(CharacterMainControl player, Vector3 scaledHorizontalDir)
        {
            if (player == null) return;

            var movement = player.movementControl;
            if (movement == null) return;

            var component = movement.GetComponent<CharacterMovement>();
            if (component == null) return;

            component.PauseGroundConstraint(KnockbackGroundPause);

            Vector3 velocity = component.velocity;
            velocity.x = scaledHorizontalDir.x * KnockbackHorizontal;
            velocity.z = scaledHorizontalDir.z * KnockbackHorizontal;
            velocity.y = KnockbackUpForce;
            component.velocity = velocity;
        }

        // ===== 换位（平滑） =====

        /// <summary>
        /// 把玩家平滑移动到 <paramref name="target"/>。**要在玩家的 MonoBehaviour 上起协程**：
        /// <c>player.StartCoroutine(PlayerEffectActions.SmoothMoveTo(player, pos, dur))</c>。
        /// </summary>
        public static IEnumerator SmoothMoveTo(CharacterMainControl player, Vector3 target, float duration)
        {
            if (player == null) yield break;

            Vector3 start = player.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (player == null) yield break;   // 中途被销毁

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                player.transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));

                yield return null;
            }

            if (player != null) player.transform.position = target;
        }

        // ===== 咬枪（消耗弹匣里的子弹） =====

        /// <summary>
        /// 从玩家当前枪械的弹匣里扣 <paramref name="count"/> 发。返回**实际扣掉**的发数。
        ///
        /// <para>两道门都不能省，理由见 <c>AmmoEaterBehavior</c> 里的长注释
        /// （换弹中途扣弹会让实扣数与缓存/UI 对不上）。</para>
        /// </summary>
        public static int ConsumeBullets(CharacterMainControl player, int count)
        {
            if (player == null || count <= 0) return 0;

            ItemAgent_Gun gun = player.GetGun();
            if (gun == null) return 0;

            ItemSetting_Gun setting = gun.GunItemSetting;
            if (setting == null) return 0;

            if (setting.LoadingBullets || gun.IsReloading()) return 0;
            if (setting.BulletCount <= 0) return 0;
            if (gun.BulletEmpty) return 0;

            int consumed = 0;
            for (int i = 0; i < count; i++)
            {
                if (setting.LoadingBullets || gun.IsReloading() || gun.BulletEmpty) break;
                setting.UseABullet();
                consumed++;
            }

            return consumed;
        }

        // ===== 强制换弹 =====

        /// <summary>强制玩家换弹。返回是否真的发起了换弹（没枪 / 换不了时返回 false）。</summary>
        public static bool ForceReload(CharacterMainControl player)
        {
            if (player == null) return false;

            var gun = player.GetGun();
            if (gun == null) return false;

            return player.TryToReload();
        }

        // ===== 掉落当前武器 =====

        /// <summary>
        /// 让玩家丢掉正在拿的武器（只丢"武器"类，别的物品不动）。
        /// 返回 true = 确实丢了一件；false = 手上不是武器（**调用方据此决定是否消耗这次机会**）。
        /// </summary>
        public static bool DropCurrentWeapon(CharacterMainControl player)
        {
            if (player == null) return false;

            var heldAgent = player.CurrentHoldItemAgent;
            var heldItem = heldAgent ? heldAgent.Item : null;

            bool hasWeapon = heldItem != null && heldItem.Tags != null && heldItem.Tags.Contains("Weapon");
            if (!hasWeapon) return false;

            var dropPos = player.transform.position + Vector3.up * 0.1f;
            heldItem.Drop(dropPos, true, Vector3.forward, 360f);

            if (player.agentHolder != null && player.CurrentHoldItemAgent != null)
            {
                player.agentHolder.ChangeHoldItem(null);
            }

            return true;
        }

        // ===== 偷窃（客户端侧：从自己背包里移除指定物品） =====

        /// <summary>
        /// 从玩家背包里移除**一件指定类型**的物品，成功则返回它的 typeId，否则 <c>-1</c>。
        ///
        /// <para>这是偷窃在**受害者那台机器**上的那一半：物品必须从真人的背包里出去。
        /// 另一半（把东西加到精英身上）在主机做——主机拿不到客机的完整背包，
        /// 所以只能由这边挑、再回报偷了什么。</para>
        /// </summary>
        public static int RemoveOneItemOfType(CharacterMainControl player, int typeId)
        {
            if (player == null) return -1;

            Item bag = player.CharacterItem;
            if (bag == null || bag.Inventory == null) return -1;

            var inventory = bag.Inventory;
            for (int i = 0; i < inventory.Content.Count; i++)
            {
                var item = inventory.Content[i];
                if (item == null || !item) continue;
                if (typeId >= 0 && item.TypeID != typeId) continue;

                int removedTypeId = item.TypeID;
                item.Detach();
                return removedTypeId;
            }

            return -1;
        }

        // ===== 撤销【混沌】施加的那批 debuff =====

        /// <summary>
        /// 把 <paramref name="debuffs"/> 里被 <paramref name="mask"/> 选中的那几个从玩家身上撤掉。
        ///
        /// <para><b>为什么是位掩码而不是 id 列表</b>：两端手上是**同一份数组**
        /// （<c>ChaosOnHitBehavior.NegativeDebuffs</c>），所以"撤哪几个"只要几个 bit。
        /// 而联机那条「玩家效果」报文**只带一个 <c>int</c> 参数**——正好装得下，
        /// 于是不必新开报文体、**也不必升协议版本**（<c>effect</c> 字段本来就是 byte）。</para>
        ///
        /// <para>⚠ <b>代价：数组的顺序即协议里的位序。</b>重排那个数组会让客机撤错 buff，
        /// 而且**不报错、不留日志**——所以那边的注释里写了"不许重排"。</para>
        /// </summary>
        public static void RemoveDebuffs(CharacterMainControl player, Buff[] debuffs, int mask)
        {
            if (player == null || debuffs == null || mask == 0) return;

            for (int i = 0; i < debuffs.Length; i++)
            {
                if ((mask & (1 << i)) == 0) continue;

                var prefab = debuffs[i];
                if (prefab == null) continue;

                player.RemoveBuff(prefab.ID, false);
            }
        }
    }
}
