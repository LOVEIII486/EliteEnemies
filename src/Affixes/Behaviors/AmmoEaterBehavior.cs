using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 噬弹：命中玩家时，从玩家**当前武器的弹匣里吃掉子弹**。
    ///
    /// <para>与既有的「弹匣诅咒」不是一回事：那个是强制玩家换弹（子弹还在），
    /// 这个是**真把子弹扣掉**。</para>
    /// </summary>
    public class AmmoEaterBehavior : AffixBehaviorBase, ICombatAffixBehavior
    {
        public override string AffixName => "AmmoEater";

        /// <summary>每次啃掉几发。</summary>
        private const int BulletsPerBite = 1;

        /// <summary>两次啃之间至少隔多久。不加节流的话，霰弹枪一轮多颗弹丸能瞬间清空弹匣。</summary>
        private const float BiteInterval = 0.5f;

        private float _lastBiteTime = -999f;

        public void OnAttack(CharacterMainControl character, DamageInfo damageInfo) { }

        public void OnDamaged(CharacterMainControl character, DamageInfo damageInfo) { }

        public override void OnHitPlayer(CharacterMainControl attacker, CharacterMainControl victim, DamageInfo damageInfo)
        {
            if (Time.time < _lastBiteTime + BiteInterval) return;

            CharacterMainControl player = CharacterMainControl.Main;
            if (player == null) return;

            // 玩家没拿枪（跑图/近战）时不生效——这是正常的空窗，不是 bug
            ItemAgent_Gun gun = player.GetGun();
            if (gun == null) return;

            ItemSetting_Gun setting = gun.GunItemSetting;
            if (setting == null) return;

            // ⚠ 两道门都必须有，而且**不能省**：
            //   · LoadingBullets —— UseABullet 自己**不检查换弹状态**。换弹流程
            //     （ItemSetting_Gun.LoadBulletsFromInventory，async）里 needCount 是按进入时的
            //     弹数算好的，中途插进去扣弹，会让实扣发数与缓存/UI 对不上。
            //   · IsReloading() —— 状态机层面的同一件事，两个一起判更稳。
            if (setting.LoadingBullets || gun.IsReloading()) return;

            // 顺手读一次公开的 BulletCount：它的 getter 会在缓存为负时重算，
            // 保证下面 UseABullet 的 `bulletCount--` 落在一个**已初始化**的值上。
            // 同时这也是一道"空仓就别啃"的门。
            if (setting.BulletCount <= 0) return;
            if (gun.BulletEmpty) return;

            for (int i = 0; i < BulletsPerBite; i++)
            {
                if (setting.LoadingBullets || gun.IsReloading() || gun.BulletEmpty) break;
                setting.UseABullet();
            }

            _lastBiteTime = Time.time;
        }
    }
}
