using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Duckov.Scenes;
using EliteEnemies.DebugTools;
using UnityEngine;
using SodaCraft.Localizations;

namespace EliteEnemies.Core
{
    /// <summary>
    /// 敌人生成辅助工具。
    ///
    /// <para><b>它原先经 <c>Egg</c> 生成，现在直接调
    /// <see cref="CharacterRandomPreset.CreateCharacterAsync"/>。</b> 两者走的是同一条路——
    /// <c>Egg.Spawn()</c> 内部就是 <c>preset.CreateCharacterAsync(...)</c>
    /// （<c>Egg.cs:53</c>），而游戏文档确认 <c>CreateCharacterAsync</c> 是
    /// **所有 preset 路径（普通刷怪 / pet / mate / Egg / 部署道具）的唯一汇聚点**。
    /// 所以这次改的不是"路径"，而是**拿不拿得到生成结果**：</para>
    ///
    /// <list type="bullet">
    /// <item><c>Egg.Spawn()</c> 是 <c>async UniTaskVoid</c>，**不返回角色**。于是原先只能
    /// <c>WaitForSeconds(delay + 0.3f)</c> 之后用 <c>FindObjectsByType</c> + 距离阈值
    /// 去**猜**刚生成的是谁——而那个猜测不区分队伍，旁边有别的敌人就会**认错对象**，
    /// 把缩放与词条加到无关的敌人身上。**不报错、不抛异常。**</item>
    /// <item><c>CreateCharacterAsync</c> 的签名是 <c>UniTask&lt;CharacterMainControl&gt;</c>，
    /// **直接返回角色**，失败给 <c>null</c>。竞态与魔法延迟一并消失。</item>
    /// </list>
    ///
    /// <para>顺带去掉的还有：<c>Egg</c> 预制体依赖（原先靠
    /// <c>Resources.FindObjectsOfTypeAll&lt;Egg&gt;</c> 全局搜索取一个）与
    /// <c>DefaultEggSpawnDelay</c>。</para>
    ///
    /// <para>⚠ <b>Egg 顺手做的两件事这里照做，以保持行为不变</b>：给生成体继承召唤者的队伍，
    /// 并把它设成召唤者的跟随者（<c>ai.leader</c> / <c>PetAI.SetMaster</c>）。
    /// 「分裂分身是否也该跟随原体」是个玩法问题，**本轮不改**，见
    /// <see cref="ApplySpawnerRelation"/> 的注释。</para>
    /// </summary>
    public class EggSpawnHelper : MonoBehaviour
    {
        private const string LogTag = "[EliteEnemies.EggSpawnHelper]";

        /// <summary>
        /// 生成位置相对传入坐标的下移量。**照抄 <c>Egg</c> 的值**
        /// （<c>Egg.cs</c> 用 <c>transform.position + Vector3.down * 0.25f</c>）——
        /// 蛋是靠自身碰撞体落在地面上的，去掉蛋之后这个偏移要自己补，
        /// 否则生成体会浮在地面上方。
        /// </summary>
        private const float SpawnGroundOffset = 0.25f;

        private static EggSpawnHelper _instance;
        public static EggSpawnHelper Instance => _instance;

        private bool _isReady = false;
        private CharacterMainControl _player;

        public bool IsReady => _isReady;

        // ========== 副本账本 ==========

        /// <summary>
        /// 待释放的副本：<c>CreateModifiedPreset</c> 造的副本必须**活到生成体死亡**，
        /// 所以记账是「副本 + 它的生成体」，释放时机由生成体说了算。
        /// </summary>
        private struct PendingPreset
        {
            public CharacterRandomPreset Preset;
            public CharacterMainControl Owner;
        }

        private readonly List<PendingPreset> _pendingPresets = new List<PendingPreset>();

        // ========== 客机侧：召唤体的显示名 ==========

        /// <summary>
        /// 客机侧记的一笔"给召唤体复制体套过的显示名"，**只为换语言时重推**。
        ///
        /// <para>为什么要重推：名字是走 <c>SetOverrideText</c> 推给游戏本地化器的
        /// <b>一份文本</b>，而那份文本是按"当时那门语言"拼出来的（后缀与前缀都取自语言）
        /// ⇒ 不重推的话，玩家切一次语言，客机头顶的召唤体名字会**停在旧语言**上。
        /// 换语言是低频事件，所以这里不做缓存失效那套，只在版本号变过之后整表重推一遍。</para>
        /// </summary>
        private struct LocalDisplayName
        {
            public CharacterRandomPreset Preset;
            public string NameKey;
            public string PrefixPresetKey;
        }

        private readonly List<LocalDisplayName> _localDisplayNames = new List<LocalDisplayName>();

        /// <summary>上次重推时 <c>LanguageVersion</c> 的值。初值 -1 ⇒ 第一帧就对齐，不会误重推。</summary>
        private int _localNameLanguageVersion = -1;

        // 账本四件套。存在的意义是**自检**：DumpPresetLedger 断言
        // 「创建 = 随生成体释放 + 立即释放 + 异常孤儿 + 跟踪中」。
        // 没有它的话，四处记账里漏一处，表现只是"少释放了几个副本"，
        // 不报错、不影响玩法，不会有任何人发现。
        private int _presetCreated;
        private int _releasedWithOwner;
        private int _releasedImmediately;
        private int _abandoned;

        // ========== 初始化 ==========

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Start()
        {
            StartCoroutine(WaitForInitialization());
        }

        private IEnumerator WaitForInitialization()
        {
            while (CharacterMainControl.Main == null)
            {
                yield return new WaitForSeconds(0.5f);
            }

            _player = CharacterMainControl.Main;
            _isReady = true;
            Debug.Log($"{LogTag} 初始化完成");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;

            // 注意：_pendingPresets 里还没释放的副本就此交给场景卸载时的
            // Resources.UnloadUnusedAssets 回收。走到这里只可能是模组被停用
            // （本对象是 DontDestroyOnLoad），而那时对应的召唤体可能还活着——
            // **不能**顺手把它们脚下的预设销毁掉。这是已知且刻意的残留。
        }

        // ========== 副本释放 ==========

        /// <summary>
        /// 每帧检查待释放的副本。**常态开销是一次 <c>Count</c> 比较**（没有待释放项就直接返回）。
        ///
        /// <para>⚠ 为什么是「每帧扫描」而不是「给生成体挂一个组件，在它的 <c>OnDestroy</c> 里释放」：
        /// 后者依赖**同一 GameObject 上各组件 <c>OnDestroy</c> 的执行顺序**，而 Unity 不保证那个顺序。
        /// 本工程核过当前所有读者——游戏侧的 <c>CharacterMainControl</c>、<c>AICharacterController</c>、
        /// <c>Health</c> 的 <c>OnDestroy</c>，以及本模组各词条的 <c>OnCleanup</c>——**都不读
        /// <c>characterPreset</c>**，所以那样写今天也不会出事；但那是**对别人未来代码的假设**。
        /// 扫描完全不依赖顺序：<c>Owner != null</c> 为假意味着生成体**已经销毁完毕**，
        /// 它身上所有 <c>OnDestroy</c> 都跑完了。</para>
        /// </summary>
        private void Update()
        {
            // 换语言就重推一遍召唤体的名字。稳态开销 = 一次 int 比较，与
            // `LocalizedText` 的失效判据同一取舍。放在早退之前：`_pendingPresets`
            // 空着的时候也可能有显示名要重推。
            int langVersion = EliteEnemies.Localization.LocalizationManager.LanguageVersion;
            if (langVersion != _localNameLanguageVersion)
            {
                _localNameLanguageVersion = langVersion;
                RepushLocalDisplayNames();
            }

            if (_pendingPresets.Count == 0) return;
            ReleaseDeadOwners();
        }

        private void ReleaseDeadOwners()
        {
            for (int i = _pendingPresets.Count - 1; i >= 0; i--)
            {
                PendingPreset entry = _pendingPresets[i];
                // Unity 的假空判定：生成体被销毁之后 `Owner != null` 才为假。
                // 销毁是**帧末**发生的，所以这里看到假空时，那个角色的一生已经结束。
                if (entry.Owner != null) continue;

                _pendingPresets.RemoveAt(i);
                ReleasePreset(entry.Preset, "生成体已销毁", followedOwner: true);
            }
        }

        /// <summary>
        /// 把副本与生成体绑定。副本的寿命从此由生成体决定，见 <see cref="CreateModifiedPreset"/>。
        /// </summary>
        private void TrackPreset(CharacterRandomPreset preset, CharacterMainControl owner)
        {
            if (preset == null || owner == null) return;
            _pendingPresets.Add(new PendingPreset { Preset = preset, Owner = owner });
        }

        /// <summary>
        /// 释放一个副本预设。**步骤顺序有讲究，不要重排**：名称与实例 ID 都得在 <c>Destroy</c>
        /// **之前**取——对已销毁对象读 <c>.name</c> 或调 <c>GetInstanceID()</c> 都会抛。
        /// </summary>
        private void ReleasePreset(CharacterRandomPreset preset, string reason, bool followedOwner)
        {
            if (preset == null) return;

            // 客机侧那份"名字表"也要跟着副本一起退场，否则它会随每只召唤体增长、
            // 永远不退（而那张表在换语言时会被整表遍历一遍）。
            for (int i = _localDisplayNames.Count - 1; i >= 0; i--)
            {
                if (_localDisplayNames[i].Preset == preset) _localDisplayNames.RemoveAt(i);
            }

            string presetName = preset.name;
            // 撤销忽略登记必须与销毁成对，理由见 EliteEnemyCore.UnregisterIgnoredPreset。
            EliteEnemyCore.UnregisterIgnoredPreset(preset);

            if (followedOwner) _releasedWithOwner++;
            else _releasedImmediately++;

            Destroy(preset);

            if (DebugSwitch.Enabled)
            {
                Debug.Log($"{LogTag} 已释放副本预设 {presetName}（{reason}）");
            }
        }

        /// <summary>
        /// 记一笔「异常路径上刻意没释放」的副本。**这不是漏做，是核实过的取舍**：
        /// <c>character.characterPreset = this</c> 在 <c>CharacterRandomPreset.cs:311</c>，
        /// 而它之后还有 50+ 行才返回——<c>SetTeam</c>（<c>:338</c>）、<c>ai.Init</c>（<c>:358</c>，
        /// 我们的 <c>EliteSpawnPatch</c> 正挂在这句上）、多处 <c>LevelManager.Rule</c> 解引用。
        /// 所以 <c>await</c> 抛异常时，**角色可能已经存在并且正握着这个副本**——销毁它会让那个
        /// 敌人的身份卡静默变空，并让忽略名单里的 ID 提前进入"可复用"状态。
        /// 宁可让它留到关卡结束（与改动前一样，由场景卸载时的 <c>Resources.UnloadUnusedAssets</c> 回收）。
        /// </summary>
        private void MarkPresetAbandoned()
        {
            _abandoned++;
        }

        /// <summary>
        /// 打印并自检副本账本。调用点：<c>ModBehaviour.OnSceneUnloaded</c>（与其它诊断汇总放在一起）。
        /// 只在调试版有意义，故自门控。
        /// </summary>
        internal void DumpPresetLedger(string context)
        {
            // 自门控：账本是调试工具，生产路径上必须是 no-op。
            if (!DebugSwitch.Enabled) return;

            int tracked = _pendingPresets.Count;
            int accounted = _releasedWithOwner + _releasedImmediately + _abandoned + tracked;

            Debug.Log($"{LogTag} 副本账本（{context}）：创建 {_presetCreated} = " +
                      $"随生成体释放 {_releasedWithOwner} + 立即释放 {_releasedImmediately} + " +
                      $"异常孤儿 {_abandoned} + 跟踪中 {tracked}");

            if (accounted != _presetCreated)
            {
                Debug.LogError($"{LogTag} 副本账本不平：创建 {_presetCreated}，入账 {accounted}" +
                               "——有副本既没被跟踪、也没被释放，或者被释放了两次。");
            }
        }

        // ========== 公共 API：生成敌人 ==========

        /// <summary>
        /// 生成一个原体的克隆。**同步返回 null 是常态**（生成是异步的），
        /// 结果经 <paramref name="onSpawned"/> 交出。
        ///
        /// <para>⚠ <b>契约：<paramref name="onSpawned"/> 一定会被调用恰好一次</b>，
        /// 失败时参数为 <c>null</c>。<c>SpawnCloneCircleCoroutine</c> 靠它计完成数
        /// （<c>while (completedCount &lt; count)</c>）——一旦某条失败路径漏了回调，
        /// 那个协程就会**永久空转**。这是改动前实际存在的洞：三条早退路径都不回调。</para>
        /// </summary>
        public CharacterMainControl SpawnClone(
            CharacterMainControl originalEnemy,
            Vector3 position,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            bool preventElite = true,
            string customKeySuffix = null,
            string customDisplayName = null,
            System.Action<CharacterMainControl> onSpawned = null)
        {
            if (!ValidateSpawnConditions(originalEnemy))
            {
                // ⚠ **失败也要回调**，见方法注释的契约。
                onSpawned?.Invoke(null);
                return null;
            }

            try
            {
                var preset = originalEnemy.characterPreset;
                if (preset == null)
                {
                    onSpawned?.Invoke(null);
                    return null;
                }

                var modifiedPreset = CreateModifiedPreset(
                    preset,
                    healthMultiplier,
                    damageMultiplier,
                    speedMultiplier,
                    customKeySuffix,
                    customDisplayName
                );

                if (preventElite)
                {
                    EliteEnemyCore.RegisterIgnoredPreset(modifiedPreset);
                }

                // `.Forget()` 是**故意**的：本 API 是同步的（返回 null），生成结果经
                // `onSpawned` 回调交出。不加它编译器会报 CS4014（"调用未等待"）——
                // 而那正是我们想要的显式意图标记，不要靠忽略警告来消掉。
                SpawnAndApply(
                    modifiedPreset,
                    position,
                    originalEnemy,
                    scaleMultiplier,
                    affixes,
                    preventElite,
                    onSpawned).Forget();

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 生成克隆敌人异常: {ex.Message}");
                onSpawned?.Invoke(null);
                return null;
            }
        }

        /// <summary>
        /// 按预设生成敌人。结果经 <paramref name="onSpawned"/> 交出；
        /// **契约同 <see cref="SpawnClone"/>：回调一定会被调用恰好一次**，失败时为 <c>null</c>。
        /// </summary>
        public CharacterMainControl SpawnByPreset(
            CharacterRandomPreset preset,
            Vector3 position,
            CharacterMainControl spawner = null,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            bool preventElite = true,
            string customKeySuffix = null,
            string customDisplayName = null,
            System.Action<CharacterMainControl> onSpawned = null)
        {
            if (!_isReady || preset == null)
            {
                // ⚠ 这里原先**只回调 null、一个字都不说**——调用方拿不到生成体，
                //   而日志里没有任何线索（"召唤物没出现"就是这么变成无解的）。
                //   两种原因都报出来，并且区分开：调用方要查的方向完全不同。
                Debug.LogError($"{LogTag} 生成失败：{(!_isReady ? "生成助手尚未就绪（IsReady=false）" : string.Empty)}" +
                               $"{(preset == null ? "预设为空" : string.Empty)}" +
                               $"（预设={preset?.name ?? "(null)"}）");

                // **失败也要回调**，见方法注释的契约。
                onSpawned?.Invoke(null);
                return null;
            }

            try
            {
                var modifiedPreset = CreateModifiedPreset(
                    preset,
                    healthMultiplier,
                    damageMultiplier,
                    speedMultiplier,
                    customKeySuffix,
                    customDisplayName
                );

                if (preventElite)
                {
                    EliteEnemyCore.RegisterIgnoredPreset(modifiedPreset);
                }

                CharacterMainControl effectiveSpawner = spawner ?? _player;
                // `.Forget()` 是**故意**的：本 API 是同步的（返回 null），生成结果经
                // `onSpawned` 回调交出。不加它编译器会报 CS4014（"调用未等待"）——
                // 而那正是我们想要的显式意图标记，不要靠忽略警告来消掉。
                SpawnAndApply(
                    modifiedPreset,
                    position,
                    effectiveSpawner,
                    scaleMultiplier,
                    affixes,
                    preventElite,
                    onSpawned).Forget();

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 通过预设生成敌人异常: {ex.Message}");
                onSpawned?.Invoke(null);
                return null;
            }
        }

        public CharacterMainControl SpawnByPresetName(
            string resourceName,
            Vector3 position,
            CharacterMainControl spawner = null,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            List<string> affixes = null,
            bool preventElite = true,
            string customKeySuffix = null,
            string customDisplayName = null,
            System.Action<CharacterMainControl> onSpawned = null)
        {
            var preset = FindPreset(resourceName);
            if (preset == null)
            {
                Debug.LogError($"{LogTag} 未找到预设资源: {resourceName}");
                return null;
            }

            return SpawnByPreset(preset, position, spawner, healthMultiplier, damageMultiplier, speedMultiplier,
                scaleMultiplier, affixes, preventElite, customKeySuffix, customDisplayName, onSpawned);
        }


        /// <summary>
        /// 在指定中心点环绕生成多个敌人克隆体
        /// </summary>
        public void SpawnCloneCircle(
            CharacterMainControl originalEnemy,
            Vector3 centerPosition,
            int count,
            float radius = 3f,
            float healthMultiplier = 1f,
            float damageMultiplier = 1f,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            bool preventElite = true,
            string customKeySuffix = null,
            string customDisplayName = null,
            System.Action<List<CharacterMainControl>> onAllSpawned = null)
        {
            if (!ValidateSpawnConditions(originalEnemy) || count <= 0)
            {
                onAllSpawned?.Invoke(null);
                return;
            }

            StartCoroutine(SpawnCloneCircleCoroutine(
                originalEnemy, centerPosition, count, radius,
                healthMultiplier, damageMultiplier, speedMultiplier, scaleMultiplier,
                preventElite, customKeySuffix, customDisplayName, onAllSpawned));
        }

        /// <summary>
        /// 等待分身生成回调的超时（秒，**真实时间**口径）。
        /// 它是契约之外的一道保险丝，见 <see cref="SpawnCloneCircleCoroutine"/>。
        /// </summary>
        private const float SpawnWaitTimeoutSeconds = 5f;

        private IEnumerator SpawnCloneCircleCoroutine(
            CharacterMainControl originalEnemy,
            Vector3 centerPosition,
            int count,
            float radius,
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            float scaleMultiplier,
            bool preventElite,
            string customKeySuffix,
            string customDisplayName,
            System.Action<List<CharacterMainControl>> onAllSpawned)
        {
            float angleStep = 360f / count;
            List<CharacterMainControl> spawnedEnemies = new List<CharacterMainControl>();
            int completedCount = 0;

            // 确定基础后缀，若未传则使用默认值
            string finalSuffix = !string.IsNullOrEmpty(customKeySuffix) ? customKeySuffix : "EE_Circle";

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector3 spawnPosition = centerPosition + offset;

                SpawnClone(
                    originalEnemy: originalEnemy,
                    position: spawnPosition,
                    healthMultiplier: healthMultiplier,
                    damageMultiplier: damageMultiplier,
                    speedMultiplier: speedMultiplier,
                    scaleMultiplier: scaleMultiplier,
                    affixes: null,
                    preventElite: preventElite,
                    customKeySuffix: finalSuffix,
                    customDisplayName: customDisplayName,
                    onSpawned: (enemy) =>
                    {
                        if (enemy != null) spawnedEnemies.Add(enemy);
                        completedCount++;
                    });
            }

            // 等待所有成员完成生成（防止回调拿到的列表不完整）。
            //
            // ⚠ 契约是「onSpawned 必定被调用一次」（见 SpawnClone 的注释），但**不能只靠契约**：
            //    一旦将来某条失败路径漏了回调，这里就会永久空转，而分裂体拿不到
            //    SplitCloneMarker / Buff 继承，preventElite:false 下还可能继续分裂下去。
            //    故加一道保险丝：超时后**带着已有结果继续**（列表可能为空或不全——
            //    调用方 SplitBehavior 已能处理空列表，见它的 onAllSpawned）。
            // 用**真实时间**：即便时间被暂停/减速（时停词条），超时也必须能报出来。
            // 超时后仍可能有迟到回调往 spawnedEnemies 里追加——当前唯一调用方
            // （SplitBehavior）是同步遍历后即弃，不受影响；将来若有人**留着**这个列表
            // 跨帧读，就要改成超时时交出一份副本。
            float deadline = Time.realtimeSinceStartup + SpawnWaitTimeoutSeconds;
            while (completedCount < count && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (completedCount < count)
            {
                Debug.LogWarning($"{LogTag} 等待分身生成超时（{completedCount}/{count}，" +
                                 $"{SpawnWaitTimeoutSeconds} 秒）——有路径漏调了 onSpawned 回调。" +
                                 "已带着现有结果继续，不再等待。");
            }

            onAllSpawned?.Invoke(spawnedEnemies);
        }

        // ========== 核心逻辑重构 ==========

        /// <summary>
        /// 造一个改动过的预设副本（倍率、键名后缀、名字覆写）。
        ///
        /// <para><b>⚠ 寿命契约：副本必须活到生成体死亡。</b> 生成体经
        /// <c>characterPreset</c> 持有它（游戏侧 <c>CharacterRandomPreset.cs:311</c> 赋值），
        /// 而且**死后仍在读**——死亡帧的击杀计数（<c>CharacterMainControl.cs:1806</c>）、
        /// 血条图标与名字（<c>HealthBar.cs:252-274</c>）、任务判定
        /// （<c>QuestTask_KillCount.cs:306</c>）。所以「生成完就销毁」会把这些读点全部打断；
        /// 释放只能由生成体的销毁来触发（<see cref="ReleaseDeadOwners"/>）。</para>
        ///
        /// <para>副本的释放走 <see cref="ReleasePreset"/> 这一个出口——账本
        /// （<see cref="_presetCreated"/> 等）在这里起算，漏一处就会被
        /// <see cref="DumpPresetLedger"/> 的断言抓出来。</para>
        /// </summary>
        private CharacterRandomPreset CreateModifiedPreset(
            CharacterRandomPreset original,
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            string customKeySuffix = null,
            string customDisplayName = null)
        {
            CharacterRandomPreset modified = Instantiate(original);
            // 副本从这一刻起存在 ⇒ 账本起算。此后的步骤若抛异常，调用方只会拿到一句日志，
            // 这个副本既没被跟踪也没被释放——账本会不平，那正是我们想要它发出的声音。
            _presetCreated++;

            // 1. 确定标识后缀
            string suffix = !string.IsNullOrEmpty(customKeySuffix) ? customKeySuffix : "EE_Clone";

            // 2. 同步修改 name 和 nameKey，确保与新的判定系统兼容
            // 同时增加 EndsWith 检查防止递归生成导致名称无限延长
            if (!original.name.EndsWith($"_{suffix}"))
            {
                modified.name = $"{original.name}_{suffix}";
                modified.nameKey = $"{original.nameKey}_{suffix}";
            }
            else
            {
                modified.name = original.name;
                modified.nameKey = original.nameKey;
            }

            // 3. 应用属性倍率
            modified.health = original.health * healthMultiplier;
            modified.damageMultiplier = original.damageMultiplier * damageMultiplier;
            modified.moveSpeedFactor = original.moveSpeedFactor * speedMultiplier;

            // 4. 处理本地化注入
            if (!string.IsNullOrEmpty(customDisplayName))
            {
                modified.showName = true;
                modified.showHealthBar = true;
                string targetKey = modified.nameKey;

                // 检查是否已经注入过相同文本，避免重复调用开销
                if (!LocalizationManager.TryGetOverrideText(targetKey, out string currentVal) || currentVal != customDisplayName)
                {
                    LocalizationManager.SetOverrideText(targetKey, customDisplayName);
                }
            }

            return modified;
        }

        /// <summary>
        /// 按**跨机标识**找预设：先比 <c>nameKey</c>、再比资源名 <c>name</c>。
        ///
        /// <para>⚠ <b>口径与联机模组自己的 <c>IsPresetMatch</c> 一致</b>
        /// （<c>AISyncService.cs:3719-3724</c>：<c>nameKey</c> 或 <c>name</c> 相等即算命中），
        /// 取值口径见 <c>EliteSummonRelay.PresetKey</c>。</para>
        ///
        /// <para><b>为什么 nameKey 优先</b>：<c>Instantiate</c> 只会把 <c>name</c> 改成
        /// <c>"XXX(Clone)"</c>，<c>nameKey</c> 是序列化字段、不受影响 ⇒
        /// 运行期被克隆过的预设只有 nameKey 还指得回原对象。
        /// 先扫一遍全部、把 <c>name</c> 的命中留作兜底（而不是立刻返回），
        /// 是为了让 <c>nameKey</c> 的精确命中优先于某个碰巧同名的克隆。</para>
        /// </summary>
        private CharacterRandomPreset FindPresetByKey(string presetKey)
        {
            if (string.IsNullOrEmpty(presetKey)) return null;

            try
            {
                var allPresets = Resources.FindObjectsOfTypeAll<CharacterRandomPreset>();

                CharacterRandomPreset byName = null;
                foreach (var preset in allPresets)
                {
                    if (preset == null) continue;

                    if (!string.IsNullOrEmpty(preset.nameKey) &&
                        preset.nameKey.Equals(presetKey, StringComparison.OrdinalIgnoreCase))
                        return preset;

                    if (byName == null &&
                        preset.name.Equals(presetKey, StringComparison.OrdinalIgnoreCase))
                        byName = preset;
                }

                return byName;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 按标识查找预设异常: {ex.Message}");
                return null;
            }
        }

        private CharacterRandomPreset FindPreset(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;

            try
            {
                var allPresets = Resources.FindObjectsOfTypeAll<CharacterRandomPreset>();
                foreach (var preset in allPresets)
                {
                    if (preset.name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return preset;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 查找预设异常: {ex.Message}");
            }

            return null;
        }

        // ========== 客机侧 API：给召唤体复制体套显示名 ==========

        /// <summary>
        /// 客机侧：给一个<b>召唤体复制体</b>套上"带自定义名字"的预设副本。
        ///
        /// <para><b>背景</b>：名字挂在预设上（<c>showName</c> + <c>nameKey</c> 的覆盖文本），
        /// 而主机那份副本是<b>运行期</b>造的、客机上不存在这个对象；联机模组按
        /// <c>CharacterPresetKey</c> 本地精确匹配必然失败，兜底会把<b>本机玩家的预设</b>
        /// 套到复制体上（<c>AISyncService.cs:3740-3745</c>）
        /// ⇒ 名字不显示，血条图标也是玩家的（<c>HealthBar.cs:259/:269</c>）。
        /// 完整链路见 <see cref="Affixes.EliteSummonRelay"/> 的类注释。</para>
        ///
        /// <para><b>这里做什么</b>：按传进来的三样在**本机**重建那份副本——
        /// 基预设按标识本地找，名字按<b>本机语言</b>重拼。
        /// 刻意不接收渲染好的名字（<c>AGENT.md §3.5</c>：传译文等于把主机那门语言焊死）。</para>
        ///
        /// <para>倍率一律 <c>1</c>：这份副本只为名字与图标，<b>不参与任何数值</b>
        /// （复制体的血量/伤害由联机模组单独同步）。副本里那个 <c>health</c> 字段在创建之后
        /// 再没人读（唯一读点在 <c>CharacterRandomPreset</c> 自己的创建流程里），
        /// 所以留着基预设的值是安全的。</para>
        ///
        /// <para>副本的寿命走与本文件主机侧<b>同一条</b>路（<see cref="TrackPreset"/> +
        /// <see cref="ReleaseDeadOwners"/>），不另造一套账。</para>
        ///
        /// <para>返回 <c>false</c> = 没套上（基预设找不到）。调用方应当把它报出来——
        /// 静默失败会让"名字怎么不显示"重新变成无解的。</para>
        /// </summary>
        internal bool AttachSummonDisplayName(CharacterMainControl enemy,
                                              string basePresetKey,
                                              string nameKey,
                                              string prefixPresetKey)
        {
            if (enemy == null || string.IsNullOrEmpty(nameKey)) return false;

            var basePreset = FindPresetByKey(basePresetKey);
            if (basePreset == null)
            {
                Debug.LogError($"{LogTag} 召唤体显示名：本机找不到基预设 '{basePresetKey}'" +
                               "——该复制体会继续用当前预设，名字与图标都不对。");
                return false;
            }

            string displayName = ComposeSummonDisplayName(nameKey, prefixPresetKey, out bool prefixOk);
            if (!prefixOk)
            {
                Debug.LogError($"{LogTag} 召唤体显示名：本机找不到前缀预设 '{prefixPresetKey}'" +
                               "——这个名字会缺掉前面那一截。");
            }

            // ⚠ 后缀必须**由键唯一决定**：`SetOverrideText` 是按 nameKey 记账的，
            //   后缀撞车 ⇒ 两只名字不同的召唤体共用同一份文本，**后写的赢且不报错**。
            //   `nameKey` 蕴含词条（鸳鸯伴侣与守护伴侣用的是两个不同的后缀键）、
            //   基预设蕴含敌人类型 ⇒ 这个组合唯一决定文本。
            string suffix = $"EE_CoopName_{nameKey}";

            var clone = CreateModifiedPreset(basePreset, 1f, 1f, 1f, suffix, displayName);

            enemy.characterPreset = clone;

            // 与游戏在预设创建流程里的做法对齐（`CharacterRandomPreset.cs:341` 的
            // `character.Health.showHealthBar = showHealthBar`）——我们是在角色**创建之后**
            // 才换的预设，那一步不会自己再跑一次。
            if (enemy.Health != null) enemy.Health.showHealthBar = clone.showHealthBar;

            // 与主机侧一样登记"忽略精英化"：两端都不该让召唤体变成精英。
            // 与副本的销毁成对（`ReleasePreset` 里调 `UnregisterIgnoredPreset`）。
            EliteEnemyCore.RegisterIgnoredPreset(clone);

            TrackPreset(clone, enemy);
            _localDisplayNames.Add(new LocalDisplayName
            {
                Preset = clone,
                NameKey = nameKey,
                PrefixPresetKey = prefixPresetKey
            });

            return true;
        }

        /// <summary>
        /// 按<b>本机语言</b>把显示名拼出来。
        /// <paramref name="prefixOk"/> 为假 = 前缀预设没找到（调用方据此报错；
        /// 返回的仍是不带前缀的可用名字）。
        /// </summary>
        private string ComposeSummonDisplayName(string nameKey, string prefixPresetKey,
                                                out bool prefixOk)
        {
            prefixOk = true;

            // `ToPlainText()` 是游戏自己的"键 → 文本"解析（`EliteEnemyCore.ResolveBaseName`
            // 用的就是同一条），它算上了本模组推进去的 CSV 文本 ⇒ 跟着语言走。
            string suffix = nameKey.ToPlainText();

            if (string.IsNullOrEmpty(prefixPresetKey)) return suffix;

            var prefixPreset = FindPresetByKey(prefixPresetKey);
            if (prefixPreset == null)
            {
                prefixOk = false;
                return suffix;
            }

            // ⚠ 必须与主机侧那句 `$"{_self.characterPreset.DisplayName} ({PartnerSuffix})"`
            //   逐字同形（MandarinDuckBehavior / GuardianBehavior），否则两端名字不一样。
            return $"{prefixPreset.DisplayName} ({suffix})";
        }

        /// <summary>
        /// 换语言后把已经套过的名字按新语言重拼一遍。
        ///
        /// <para>名字是走 <c>SetOverrideText</c> 推给游戏本地化器的**一份文本**，
        /// 而那份文本是拼的时候那门语言的（后缀与前缀都取自语言）⇒ 不重推的话，
        /// 玩家切一次语言，客机头顶的召唤体名字会**停在旧语言**上。
        /// 换语言是低频事件，所以不做逐帧缓存失效，只在版本号变过之后整表过一遍。</para>
        /// </summary>
        private void RepushLocalDisplayNames()
        {
            if (_localDisplayNames.Count == 0) return;

            // 先摘掉已销毁的副本：复制体死后副本由 `ReleaseDeadOwners` 释放（销毁是帧末），
            // 这张表不能因此留下悬空引用。
            for (int i = _localDisplayNames.Count - 1; i >= 0; i--)
            {
                if (_localDisplayNames[i].Preset == null) _localDisplayNames.RemoveAt(i);
            }

            for (int i = 0; i < _localDisplayNames.Count; i++)
            {
                var entry = _localDisplayNames[i];
                string text = ComposeSummonDisplayName(entry.NameKey,
                                                       entry.PrefixPresetKey, out _);
                LocalizationManager.SetOverrideText(entry.Preset.nameKey, text);
            }

            Debug.Log($"{LogTag} 已按新语言重推 {_localDisplayNames.Count} 个召唤体名字");
        }

        // ========== 内部辅助 ==========

        private bool ValidateSpawnConditions(CharacterMainControl originalEnemy = null)
        {
            if (!_isReady) return false;
            if (originalEnemy != null && originalEnemy.characterPreset == null) return false;
            return true;
        }

        /// <summary>
        /// 生成一个敌人并立即施加全部修改。**整条链上没有任何"猜"**。
        ///
        /// <para>对照原先的三段式（<c>SpawnEgg</c> → <c>WaitForSeconds</c> →
        /// <c>FindEnemyNearPosition</c>）：现在 <c>await</c> 回来的就是生成体本身，
        /// 失败时是显式的 <c>null</c> 而不是"找不到就算了"。</para>
        /// </summary>
        private async UniTaskVoid SpawnAndApply(
            CharacterRandomPreset preset,
            Vector3 position,
            CharacterMainControl spawner,
            float scaleMultiplier,
            List<string> affixes,
            bool preventElite,
            Action<CharacterMainControl> onSpawned)
        {
            CharacterMainControl enemy = null;

            try
            {
                if (preset == null)
                {
                    Debug.LogError($"{LogTag} 预设为空，无法生成");
                    onSpawned?.Invoke(null);
                    return;
                }

                // Egg 用的是 MainScene 的 buildIndex，方向固定 forward（见 Egg.cs 的 Spawn）
                int relatedScene = MultiSceneCore.MainScene.Value.buildIndex;
                Vector3 spawnPos = position + Vector3.down * SpawnGroundOffset;

                enemy = await preset.CreateCharacterAsync(
                    spawnPos, Vector3.forward, relatedScene, group: null, isLeader: false);
            }
            catch (Exception ex)
            {
                // ⚠ **这条路径刻意不释放副本**，理由与计数见 MarkPresetAbandoned 的注释：
                // await 抛异常时角色可能已经存在并握着这个副本。
                MarkPresetAbandoned();
                Debug.LogError($"{LogTag} 生成敌人异常: {ex.Message}");
                onSpawned?.Invoke(null);
                return;
            }

            // 失败是显式的：CreateCharacterAsync 用 null 表示 CreateCharacter 失败
            if (enemy == null)
            {
                // 这条路**确定没有生成体**：返回 null 的唯一出口是
                // CharacterRandomPreset.cs:309（character 为 null），而 CharacterCreator.cs:18-22
                // 在那种情况下已先把 GameObject 销毁。所以副本可以立刻释放，不必等生成体。
                ReleasePreset(preset, "生成失败（未创建角色）", followedOwner: false);

                Debug.LogError($"{LogTag} 生成敌人失败（CreateCharacterAsync 返回 null）");
                onSpawned?.Invoke(null);
                return;
            }

            // 副本的寿命从这里起与生成体绑定：生成体活着它就必须活着（理由见 CreateModifiedPreset
            // 的寿命契约），生成体一销毁就由 Update 扫描释放。
            TrackPreset(preset, enemy);

            try
            {
                ApplySpawnerRelation(enemy, spawner);

                // 这些原先散在「延迟 + 就近搜索」那一段里，现在有了确定的生成体，直接施加。
                // 不再需要等 0.3 秒——那是为了等异步生成完成而猜的经验值。
                // 兜底：召唤物绝不能带词条。主防护是生成**前**登记的忽略预设，
                // 这里再验一次——失效时**报错并就地撤销**，而不是让它静默地带着词条。
                if (preventElite) EliteEnemyCore.EnsureNotElite(enemy);
                if (!Mathf.Approximately(scaleMultiplier, 1f))
                    enemy.transform.localScale = Vector3.one * scaleMultiplier;
                if (affixes != null && affixes.Count > 0 && !preventElite)
                    EliteEnemyCore.ForceMakeElite(enemy, affixes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 生成后施加修改异常: {ex.Message}");
            }

            onSpawned?.Invoke(enemy);
        }

        /// <summary>
        /// 复刻 <c>Egg.Spawn()</c> 对生成体做的两件事，**以保持行为与改动前一致**：
        /// 继承召唤者的队伍，并把它设成召唤者的跟随者。
        ///
        /// <para>⚠ <b>「分裂分身是否也该跟随原体」是个玩法问题，本轮刻意不改。</b>
        /// 原先经 <c>Egg</c> 时不区分词条、一律设 <c>leader</c>，所以这里也一律设——
        /// 否则这次改动就会**顺手改掉手感**，而那是另一件事。
        /// 将来若要让分身独立作战，判据应当由调用方（词条的生成方法）传入，
        /// 而不是在这里按类名猜。</para>
        /// </summary>
        private static void ApplySpawnerRelation(CharacterMainControl enemy, CharacterMainControl spawner)
        {
            if (spawner == null) return;

            enemy.SetTeam(spawner.Team);

            AICharacterController ai = enemy.GetComponentInChildren<AICharacterController>();
            if (ai == null) return;

            PetAI pet = ai.GetComponent<PetAI>();
            if (pet != null) pet.SetMaster(spawner);

            ai.leader = spawner;
        }
    }
}