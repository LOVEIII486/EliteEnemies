using System;
using EliteEnemies.Core;
using EliteEnemies.Visuals;
using UnityEngine;
using UnityEngine.Events;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 客机侧的**精英视觉预警**：把两条"主机玩家看得见、客机玩家看不见"的预警
    /// 在复制体上重放出来。
    ///
    /// <para><b>起因</b>：客机的复制体是"哑"的——上面根本不挂 <c>EliteBehaviorComponent</c>，
    /// 词条行为一律不跑。于是缺了两条纯粹是表现层的预警：</para>
    /// <list type="bullet">
    /// <item>【自爆】<c>SelfDestructBehavior</c> 那条持续红色脉冲没有 ⇒
    /// <b>客机玩家零提示就被炸</b>，这是公平性问题，不只是观感；</item>
    /// <item>【报复】<c>RevengeBehavior</c> 的青色闪烁没有 ⇒ 看不到"它要还手了"；</item>
    /// <item>【反弹】<c>ReflectBehavior</c> 的金色护盾没有 ⇒
    /// <b>看不到"这会儿打它会被弹回来"</b>（见下面那条 ⚠）。</item>
    /// </list>
    ///
    /// <para>⚠ <b>【反弹】与前两条的驱动方式根本不同，别照着改</b>：
    /// 自爆/报复是**纯表现**，客机本地就能推出来、推错也无所谓；
    /// 而护盾背后是**玩法状态**——它必须与主机那个 <c>3s</c> 反射窗口**逐帧对齐**，
    /// 否则"看到护盾时打过去没被弹"或反过来会直接误导玩家。
    /// 所以它<b>不走本地推导，而是由主机广播的权威状态驱动</b>
    /// （<c>CoopEliteSync</c> 的 <c>s_clientReflecting</c>，协议 v9 起随精英条目过网）。
    /// 本组件在这里只是"把那份状态画出来"。</para>
    ///
    /// <para><b>为什么自爆/报复不需要新报文</b>：这两条客机**本地就能推出来**，所以
    /// 不动 <c>CoopWire</c>、不升 <c>ProtocolVersion</c>：
    /// <list type="bullet">
    /// <item>自爆 —— <see cref="EliteMarker.Affixes"/> 里有没有 <c>"Explosive"</c>（纯本地派生，标记本来就同步了）；</item>
    /// <item>报复 —— 订阅复制体自己的 <c>Health.OnHurtEvent</c>。联机模组对本机玩家造成的伤害是
    /// <c>return true</c>（<c>Patch_Health_Hurt_RemoteAnti</c>，见 <c>Docs\Coop\03-ai-sync.md</c> §5.1），
    /// 本地 <c>Hurt</c> 继续跑 ⇒ 事件会到。</item>
    /// </list></para>
    ///
    /// <para><b>⚠ 只做发光，刻意不做体型抖动</b>：主机那侧 <c>SelfDestructBehavior</c> 另外每帧写
    /// <c>transform.localScale</c>（脉冲膨胀），而客机的体型是由 <c>CoopEliteSync</c> 的
    /// <c>EliteVisual</c> 报文统一管的、写的是**绝对缩放**（<c>ApplyVisualTo</c>）。
    /// 自爆与巨大化/迷你**不互斥**（<c>AffixExclusivity</c> 里只有不死与自爆互斥）⇒
    /// 客机这边再写 <c>localScale</c> 会把巨大化/迷你的体型**冲掉**，而且是不报错的。
    /// 所以本轮只做发光脉冲。</para>
    ///
    /// <para><b>纯表现</b>：本组件绝不碰任何玩法状态（不改 <c>Health</c>、不碰
    /// <c>CharacterModifiers</c>、不影响伤害/掉落/选中）。它在客机上跑，一旦碰玩法就会与主机分叉。
    /// 单机玩家完全不受影响：整个联机模块只在 <see cref="CoopApi"/> 激活之后才安装，
    /// 而本组件只在 <c>CoopEliteSync</c> 的客户端路径上被挂载。</para>
    /// </summary>
    internal sealed class CoopEliteWarningVisual : MonoBehaviour
    {
        // ===== 词条名 =====
        //
        // ⚠ 这两个字符串是**运行时匹配依据**（比对的是 `EliteMarker.Affixes`，也就是主机报来的
        //    词条池键），必须与行为类里的 `AffixName` 逐字一致：
        //      "Revenge"   ← RevengeBehavior.AffixName（RevengeBehavior.cs:14）
        //      "Explosive" ← SelfDestructBehavior.AffixName（SelfDestructBehavior.cs:12）
        //    ⚠ 那两个是**实例属性返回的字符串、不是 const**，所以这里用不了 `nameof`——
        //      词条一旦改名，编译器不会报错，本组件会**静默失效**（本工程最忌的那一类）。
        //      改词条名时记得搜这里。`Affixes.Contains` 用的是 `List<string>.Contains`（序号比较），
        //      与 `GigantificationBehavior` 判断 "Slime" 的写法一致。
        private const string RevengeAffix = "Revenge";
        private const string ExplosiveAffix = "Explosive";

        // ===== 【报复】青色一次性闪烁的参数 =====
        //
        // ⚠ 这三个常量**必须与 RevengeBehavior 里的同名值逐字相同**（那是主机侧的权威表现）：
        //      RevengeGlowColor     ← RevengeBehavior.cs:40  GlowColor = new Color(0f, 1f, 1f)
        //      RevengeFlashDuration ← RevengeBehavior.cs:41  FlashDuration = 0.5f
        //      RevengeCooldown      ← RevengeBehavior.cs:18  ShootCooldown = 2f
        //                              （主机上"闪光"与"开火"共用这一个冷却）
        //    **改一处必须同时改另一处**，否则客机看到的预警与主机不一致。
        //    `TriggerFlash` 的第三个参数（初始强度）两处都用默认值 2f，不另外写。
        private static readonly Color RevengeGlowColor = new Color(0f, 1f, 1f);
        private const float RevengeFlashDuration = 0.5f;
        private const float RevengeCooldown = 2f;

        // ===== 【自爆】红色脉冲的参数 =====
        //
        // ⚠ 同样必须与 SelfDestructBehavior.UpdateInstabilityEffect（:119-127）保持一致：
        //      主机那一侧写的是  Color.red * Mathf.PingPong(Time.time * 5f, 1f) * 2f
        //    **同步常量，不要各自漂移。**
        private static readonly Color PulseColor = Color.red;
        private const float PulseSpeed = 5f;
        private const float PulseIntensity = 2f;

        /// <summary>
        /// 脉冲的采样/写入门控间隔（秒）。与 <c>SelfDestructBehavior.GlowWriteInterval</c>
        /// （<c>SelfDestructBehavior.cs:29</c>）取同一个值——那里为什么必须是 0.05 秒、
        /// 为什么"每帧写"是错的，注释都在那一边，见 <c>UpdateExplosivePulse</c>。
        /// </summary>
        private const float GlowWriteInterval = 0.05f;

        // ===== 【反弹】金色护盾的参数 =====
        //
        // ⚠ **必须与 ReflectBehavior.OnEliteInitialized 里 new SimpleShieldEffect 的实参逐字相同**
        // （那是主机侧的权威表现，写在行为类的构造调用里、不是命名常量）：
        //      ReflectBehavior.cs:33-37  new SimpleShieldEffect(character.transform,
        //                                                      new Color(1f, 0.84f, 0f, 0.35f), 1.3f)
        //    **改一处必须同时改另一处**，否则客机看到的护盾与主机不是同一个颜色/大小。
        private static readonly Color ReflectShieldColor = new Color(1f, 0.84f, 0f, 0.35f);
        private const float ReflectShieldSize = 1.3f;

        private CharacterMainControl _character;
        private Health _health;
        private UnityAction<DamageInfo> _hurtHandler;

        /// <summary>
        /// 报复的闪烁控制器。
        ///
        /// <para>⚠ <b>两条效果各用一份 <see cref="EliteGlowController"/>，与主机侧的结构一致</b>
        /// ——主机上那两个行为也是各自 <c>new</c> 一份（<c>RevengeBehavior.cs:57</c>、
        /// <c>SelfDestructBehavior.cs:38</c>），都往同一批渲染器上写。
        /// 合成一份的后果是 <c>SetEmissionColor</c> 里那句 <c>_isFlashing = false</c>
        /// 会在 50ms 内**掐掉报复的闪烁**（自爆的脉冲每 0.05 秒写一次）——
        /// 同一只精英既有 Revenge 又有 Explosive 时，客机就只剩红色脉冲了。</para>
        ///
        /// <para>代价是两条效果会在渲染器上互相覆盖（谁后写谁赢）。这与主机侧**完全一样**
        /// （主机那边谁后写取决于词条遍历顺序），不额外制造差异。</para>
        /// </summary>
        private EliteGlowController _flashGlow;

        /// <summary>
        /// 自爆的脉冲控制器。**只在确实带 <c>"Explosive"</c> 时才创建**——
        /// 它的构造函数要扫一遍角色的全部渲染器（<c>GetComponentsInChildren</c>），
        /// 不该给每只复制体都付这份成本。
        /// </summary>
        private EliteGlowController _pulseGlow;

        /// <summary>是否带自爆词条（每帧要读，所以缓存下来）。</summary>
        private bool _explosive;

        /// <summary>
        /// 【反弹】金色护盾。**懒创建**——与 <see cref="_pulseGlow"/> 同理：
        /// <see cref="SimpleShieldEffect"/> 要 <c>CreatePrimitive</c> 造一个胶囊体、
        /// <c>Shader.Find</c> 造一份运行时材质，不该给每只复制体都付这份成本
        /// （绝大多数精英不反弹）。
        ///
        /// <para>⚠ 它的那份材质**不归 GameObject 所有**（<c>new Material(shader)</c> 造的），
        /// <c>Destroy</c> 那个胶囊体不会连带销毁它 ⇒ 必须在 <see cref="OnDestroy"/> 里
        /// 显式 <c>_shield.Destroy()</c>，否则每次重建复制体都漏一份
        /// （<see cref="SimpleShieldEffect.Destroy"/> 的注释写了这件事）。</para>
        /// </summary>
        private SimpleShieldEffect _shield;

        /// <summary>
        /// 本机当前**显示出来**的反射状态。用来让 <see cref="ApplyReflect"/> 幂等——
        /// <c>CoopEliteSync</c> 会在"状态变化"与"复制体重建"两处都调它，
        /// 不记住上次的值就会重复 <c>Show()</c>/<c>Hide()</c>（无害但没必要），
        /// 更糟的是**重建后会以为当前是 false 而漏掉 Show**。
        /// </summary>
        private bool _reflecting;

        /// <summary>报复闪烁的剩余时间；<c>&lt;= 0</c> 表示当前没有闪烁在进行。</summary>
        private float _flashRemaining;

        /// <summary>上次触发报复闪烁的时刻。初值同主机侧（<c>RevengeBehavior.cs:43</c> 的 -999f）。</summary>
        private float _lastFlashTime = -999f;

        private float _nextGlowWriteTime;
        private Color _lastGlowColor;

        /// <summary>
        /// 幂等地把预警组件挂到复制体上（已经有了就只重绑状态）。
        ///
        /// <para><b>为什么必须幂等</b>：调用点 <c>CoopEliteSync.ApplyEliteMarker</c>
        /// 会被反复调到——<c>TryApply</c> 每次事件都去查"标记内容对不对"，
        /// 而复制体还**可能被销毁重建**（联机模组的 <c>Client_DestroyReplica</c>：
        /// 走远销毁、走回来再造一个）。</para>
        ///
        /// <para><b>状态一律挂在 GameObject 自己的生命周期上</b>（组件即状态），
        /// 不用静态字典存 Unity 对象——那种表会在复制体重建后指向已销毁的对象。</para>
        /// </summary>
        public static void EnsureOn(CharacterMainControl cmc)
        {
            if (cmc == null) return;

            try
            {
                var visual = cmc.GetComponent<CoopEliteWarningVisual>();
                if (visual == null) visual = cmc.gameObject.AddComponent<CoopEliteWarningVisual>();

                visual.Bind(cmc);
            }
            catch (Exception ex)
            {
                // 预警挂不上不该拦住"应用精英标记"这条链（调用点后面还有别的客户端逻辑）。
                // 但**必须出声**——静默 continue 正是本工程一路在清的东西。
                CoopLog.Warn($"[客机预警] 挂载失败（该复制体不会有报复/自爆的视觉预警）: {ex}");
            }
        }

        /// <summary>
        /// 把主机的**反射状态**套到这只复制体上。
        /// 由 <c>CoopEliteSync</c> 在"状态变了"与"复制体重建了"两处调用。
        ///
        /// <para>与 <see cref="EnsureOn"/> 的分工：那个管"组件在不在"，
        /// 这个管"当前该不该显示护盾"。**状态存在 <c>CoopEliteSync</c> 那边**
        /// （它是主机广播来的权威状态、不是本地推导，理由见类注释），本组件只负责画出来。</para>
        ///
        /// <para>组件还没挂上时**什么都不做**——那是正常时序：
        /// <c>CoopEliteSync</c> 在 <c>ApplyEliteMarker</c> 里 <c>EnsureOn</c> 之后
        /// 会立刻补一次当前值，所以漏不掉。</para>
        /// </summary>
        public static void SetReflecting(CharacterMainControl cmc, bool reflecting)
        {
            if (cmc == null) return;

            try
            {
                var visual = cmc.GetComponent<CoopEliteWarningVisual>();
                if (visual == null) return;

                visual.ApplyReflect(reflecting);
            }
            catch (Exception ex)
            {
                // 护盾画不出来不该打断调用方那条链（它后面还有别的客户端逻辑）。
                // 但**必须出声**——静默 continue 正是本工程一路在清的东西。
                CoopLog.Warn($"[客机预警] 套用反射状态失败（该复制体不会显示护盾）: {ex}");
            }
        }

        /// <summary>把反射状态画出来（幂等，可重复调用）。</summary>
        private void ApplyReflect(bool reflecting)
        {
            if (_reflecting == reflecting) return;
            _reflecting = reflecting;

            if (!reflecting)
            {
                _shield?.Hide();
                return;
            }

            if (_character == null)
            {
                // 理论上到不了（Bind 赋过值），但**不能靠"理论上"**：
                // 下面要拿它的 transform 当父节点，空引用会直接抛进调用链。
                // 回滚标志，让下次调用还能重试。
                CoopLog.Warn("[客机预警] 反射状态已到，但复制体引用为空，本次不显示护盾");
                _reflecting = false;
                return;
            }

            if (_shield == null)
            {
                _shield = new SimpleShieldEffect(_character.transform, ReflectShieldColor,
                                                 ReflectShieldSize);
            }

            _shield.Show();
        }

        /// <summary>把当前复制体的状态重新读一遍。可重复调用。</summary>
        private void Bind(CharacterMainControl cmc)
        {
            _character = cmc;
            if (_flashGlow == null) _flashGlow = new EliteGlowController(cmc);

            var marker = cmc.GetComponent<EliteMarker>();
            if (marker == null || marker.Affixes == null)
            {
                // 标记是这份预警的**唯一依据**，读不到就什么也推不出来。说出来。
                CoopLog.Warn($"[客机预警] 复制体 '{cmc.name}' 上没有可读的 EliteMarker——" +
                             "本机不会有报复/自爆的视觉预警（标记可能还没挂上）");
                _explosive = false;
                SyncHurtSubscription(false);
                return;
            }

            // 先建控制器、再置标志：反过来的话，若构造抛异常就会留下
            // `_explosive = true` 而 `_pulseGlow == null`，Update 里每帧空引用。
            bool explosive = marker.Affixes.Contains(ExplosiveAffix);
            if (explosive && _pulseGlow == null) _pulseGlow = new EliteGlowController(cmc);
            _explosive = explosive && _pulseGlow != null;

            SyncHurtSubscription(marker.Affixes.Contains(RevengeAffix));
        }

        /// <summary>
        /// 【报复】按当前词条表订阅/退订复制体的受伤事件。**两个方向都要做**。
        ///
        /// <para>⚠ 退订那一半同样必要：调用点会在"标记内容变了"时重跑
        /// （见 <c>CoopEliteSync.TryApply</c>），新的词条表里可能已经没有 Revenge ——
        /// 只订不退的话，这条预警会**无声地**挂在一只早就没有该词条的精英上。</para>
        ///
        /// <para>⚠ 订的是 <c>Health.OnHurtEvent</c> 而**不是** <c>DamageReceiver.OnHurtEvent</c>：
        /// 后者是"扣血之前"、且只覆盖"被打中"这一条路；<c>Health</c> 那条覆盖面更广
        /// （<c>Health.cs:453</c>，两条路都会走到它）。客机这边只需要"发生了伤害"这一个事实，
        /// 不需要主机侧那个"扣血前"的时机（那个时机是给不死/分裂这类改玩法用的）。
        /// 依据与取舍见 <c>EliteBehaviorComponent.RegisterCombatEvents</c> 的注释。</para>
        /// </summary>
        private void SyncHurtSubscription(bool revenge)
        {
            if (!revenge)
            {
                UnsubscribeHurt();
                return;
            }

            if (_hurtHandler != null) return;   // 已经订过（Bind 可能被重复调用）

            var health = _character.Health;
            if (health == null)
            {
                CoopLog.Warn($"[客机预警] 复制体 '{_character.name}' 上没有 Health——" +
                             "本机不会有【报复】的青色闪烁预警");
                return;
            }

            _health = health;
            _hurtHandler = OnReplicaHurt;
            _health.OnHurtEvent.AddListener(_hurtHandler);
        }

        /// <summary>退订受伤事件（没订过就什么都不做）。</summary>
        private void UnsubscribeHurt()
        {
            if (_hurtHandler == null) return;

            try
            {
                if (_health != null) _health.OnHurtEvent.RemoveListener(_hurtHandler);
            }
            catch (Exception ex)
            {
                CoopLog.Warn($"[客机预警] 退订受伤事件失败: {ex.Message}");
            }

            _hurtHandler = null;
            _health = null;
        }

        /// <summary>
        /// 复制体挨了一下。**判定条件与主机侧 <c>RevengeBehavior.OnDamaged</c>（<c>RevengeBehavior.cs:64-74</c>）
        /// 逐条对齐**：冷却内不算、角色不在场景里不算、没有攻击者不算、同队不算。
        ///
        /// <para>⚠ 本方法跑在游戏 <c>Health.Hurt</c> 的 <c>UnityEvent</c> 派发链里
        /// （反模式清单 §17 那族"在别人的回调里"），**必须整体兜住异常**——
        /// 抛出去会顺着事件链打断游戏自己的受伤流程。</para>
        /// </summary>
        private void OnReplicaHurt(DamageInfo damageInfo)
        {
            try
            {
                if (Time.time - _lastFlashTime < RevengeCooldown) return;
                if (_character == null || !_character.gameObject.activeInHierarchy) return;

                var attacker = damageInfo.fromCharacter;
                if (attacker == null || attacker.Team == _character.Team) return;

                _lastFlashTime = Time.time;
                _flashGlow.TriggerFlash(RevengeGlowColor, RevengeFlashDuration);
                _flashRemaining = RevengeFlashDuration;
            }
            catch (Exception ex)
            {
                CoopLog.Warn($"[客机预警] 处理复制体受伤事件失败（本次不出预警）: {ex.Message}");
            }
        }

        private void Update()
        {
            // 零开销早退：不在闪烁中、又不带自爆词条时，这里一次材质都不写。
            if (_character == null) return;

            float deltaTime = Time.deltaTime;   // 只取一次（03 篇 §5）

            if (_flashRemaining > 0f && _flashGlow != null) UpdateFlash(deltaTime);
            if (_explosive) UpdateExplosivePulse();

            // 护盾要每帧喂，但**没建过就不进**（`SimpleShieldEffect.Update` 自己在
            // 隐藏时也会早退，所以真正显示期间的成本只是淡入淡出那几步）。
            if (_shield != null) _shield.Update(deltaTime);
        }

        /// <summary>驱动报复的闪烁。衰减由 <c>EliteGlowController</c> 自己算，这里只喂时间与收尾。</summary>
        private void UpdateFlash(float deltaTime)
        {
            _flashGlow.Update(deltaTime);

            _flashRemaining -= deltaTime;
            if (_flashRemaining > 0f) return;

            // 收尾。`EliteGlowController.Update` 在强度归零时自己会 Reset，但它那边的衰减
            // 与这里的计时是两套浮点累加，可能我们先到点、它还差一点点 ⇒ 补一次 Reset，
            // 保证不在模型上留一层半亮的青色。
            _flashRemaining = 0f;
            _flashGlow.Reset();
        }

        /// <summary>
        /// 自爆的持续红色脉冲。**采样与写入门控逐行照抄**
        /// <c>SelfDestructBehavior.UpdateInstabilityEffect</c>（<c>SelfDestructBehavior.cs:119-127</c>）——
        /// 那段门控本身是 <c>docs\Visuals模块代码审查.md</c> V4 的修复项：`SetEmissionColor`
        /// 会遍历该角色的**全部**渲染器（含装备、武器、未激活部件）逐个
        /// <c>GetPropertyBlock</c> + <c>SetPropertyBlock</c>，而闪烁本身是
        /// <c>PingPong(Time.time * 5f, 1f)</c>——周期 0.4 秒，按 20Hz 采样已远超它，
        /// 视觉上无从分辨，写入次数却降到约 1/3。
        /// ⇒ <b>别在这里退回"每帧写材质"。</b>
        /// </summary>
        private void UpdateExplosivePulse()
        {
            if (Time.time < _nextGlowWriteTime) return;
            _nextGlowWriteTime = Time.time + GlowWriteInterval;

            float emissionStrength = Mathf.PingPong(Time.time * PulseSpeed, 1f);
            Color targetColor = PulseColor * emissionStrength * PulseIntensity;

            // 颜色没变就不写——闪烁在 PingPong 的两个极值附近会连续采样到同一个值。
            if (targetColor == _lastGlowColor) return;
            _lastGlowColor = targetColor;

            _pulseGlow.SetEmissionColor(targetColor);
        }

        /// <summary>
        /// 退订 + 清掉可能还亮着的发光 + 销毁护盾（那份运行时材质）。
        ///
        /// <para>⚠ <b>必须退订</b>：订阅的是**复制体自己**的 <c>Health</c>（不是静态事件），
        /// 但复制体被销毁时 <c>Health</c> 可能比本组件活得久一瞬，留着就是一个指向已销毁对象的委托。</para>
        /// </summary>
        private void OnDestroy()
        {
            // 清理路径不抛（03 篇 §4.3）；失败只记一条。
            UnsubscribeHurt();

            try
            {
                // 与主机侧 `RevengeBehavior.OnCleanup` / `SelfDestructBehavior.OnCleanup` 同样收尾：
                // 不清就会把最后一次写入的颜色留在渲染器上。
                _flashGlow?.Reset();
                _pulseGlow?.Reset();
            }
            catch (Exception ex)
            {
                CoopLog.Warn($"[客机预警] 收尾清光效失败（复制体正在销毁）: {ex.Message}");
            }

            // ⚠ **必须单独兜一层**：这一步要销毁一份**运行时材质**（`new Material(shader)` 造的，
            // 不归那个胶囊体所有）。混在上面那个 try 里的话，上面一抛异常这里就永远不执行
            // ⇒ 每次复制体重建都漏一份材质，而且是无声的。
            try
            {
                _shield?.Destroy();
                _shield = null;
            }
            catch (Exception ex)
            {
                CoopLog.Warn($"[客机预警] 销毁护盾失败（可能漏一份运行时材质）: {ex.Message}");
            }
        }
    }
}
