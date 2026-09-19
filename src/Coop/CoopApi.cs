using System;
using System.Reflection;
using UnityEngine;

namespace EliteEnemies.Coop
{
    /// <summary>
    /// 联机模组（Escape From Duckov Coop Mod）API 的接入点：探测、按需激活、收发桥接。
    ///
    /// <para><b>为什么这里用反射（属规范 <c>03 §2.3</c> 的 A2 类例外）</b>：
    /// 本模组依赖的 <c>EscapeFromDuckovModApi</c> 是<b>第三方可选依赖</b>——
    /// 玩家没订阅联机模组时那个 DLL 根本不在，
    /// 硬引用会让<b>单机玩家模组加载失败</b>（直接崩溃）。
    /// 这与 <c>ModSettingDiagnostics</c> 是同一理由下的同一类例外，判据是
    /// <b>硬引用会不会导致加载失败</b>：会，就是 A2。
    /// <b>不标 <c>TODO(反射待清)</c></b>——它不是历史包袱，是正当用法。</para>
    ///
    /// <para><b>探测方式：按程序集名扫 AppDomain，不按路径。</b>
    /// 该 DLL 由联机模组复制到<b>它自己的</b>模组文件夹（实测其 csproj 的 <c>_ModFolder</c> 是
    /// <c>$(DUCKOV_MODS_DIRECTORY)\联机Mod1</c>），与我们无关，且那个文件夹名会变。
    /// 扫已加载程序集是唯一稳定的做法。</para>
    ///
    /// <para><b>激活时机</b>：本模组可能先于或后于联机模组加载，两种情形都要覆盖——
    /// 启动时探一次，没探到就挂 <see cref="AppDomain.AssemblyLoad"/> 等它。
    /// 激活是<b>幂等</b>的，重复调用只花一次 bool 判断。</para>
    ///
    /// <para>⚠ <b>单机下的开销</b>：<see cref="Active"/> 是 <c>static bool</c>，
    /// 未激活时所有联机路径都在它后面短路。这个类不做任何每帧工作。</para>
    /// </summary>
    internal static class CoopApi
    {
        private const string LogTag = "[EliteEnemies.Coop]";

        /// <summary>API 程序集的<b>简单名</b>（不含版本与公钥——<c>LoadFrom</c> 加载的 FullName 带这些）。</summary>
        private const string ApiAssemblyName = "EscapeFromDuckovModApi";

        /// <summary>
        /// 联机模组<b>主程序集</b>的简单名。它和 API 程序集是<b>两个不同的 DLL</b>，
        /// 且实测本模组要用的 <c>NetService</c> 只在这里面（API 程序集里没有）。
        /// </summary>
        private const string MainAssemblyName = "EscapeFromDuckovCoopMod";

        private const string NetApiTypeName = "EscapeFromDuckovCoopMod.ModNetworkApi";
        private const string EventsTypeName = "EscapeFromDuckovCoopMod.ModApiEvents";
        private const string ContextTypeName = "EscapeFromDuckovCoopMod.ModMessageContext";
        private const string NetServiceTypeName = "EscapeFromDuckovCoopMod.NetService";

        private static bool _initialized;
        private static bool _watchingForApi;

        /// <summary>「契约对不上」只报一次，避免每次程序集加载都刷屏。</summary>
        private static bool _mismatchReported;

        private static Action<byte[]> _broadcast;
        private static Action<byte[]> _sendToServer;
        private static Func<bool> _isServer;
        private static Func<bool> _networkStarted;
        private static IDisposable _messageSubscription;
        private static EventInfo _aiSpawnedEvent;
        private static Action<int, CharacterMainControl> _aiSpawnedHandler;

        /// <summary>联机 API 是否已就绪。**单机下恒为 false**，所有联机路径在此短路。</summary>
        public static bool Active { get; private set; }

        /// <summary>本端是不是主机（服务器）。未激活时恒为 false。</summary>
        public static bool IsServer => Active && _isServer != null && _isServer();

        /// <summary>
        /// 联机是否**真的已启动**（开了房或进了房）。未激活、或激活了但没开始联机时恒为 false。
        ///
        /// <para>⚠ <b>这个区分很要紧</b>：玩家<b>装了联机模组却自己单机玩</b>时，
        /// <c>NetService.IsServer</c> 也是 <c>false</c>（从没 <c>StartNetwork</c> 过）。
        /// 若只按 <c>IsServer</c> 判断"是不是客户端"，就会把单机玩家误判成客户端，
        /// 进而<b>关掉他全部精英</b>——一个不报错、不留日志的静默失效。</para>
        /// </summary>
        public static bool NetworkStarted => Active && _networkStarted != null && _networkStarted();

        /// <summary>
        /// 启动接入。**幂等**，可安全重复调用。
        /// 由 <c>ModBehaviour</c> 的「联机兼容」子系统在 <c>Phase.Early</c> 调用。
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // 先探一次：联机模组可能已经加载好了
            if (TryActivate()) return;

            // 还没到。挂上汇编加载监听等它来。
            //
            // ⚠ **不能"等到第一个就退订"**：联机模组有**两个**程序集
            // （EscapeFromDuckovModApi 与 EscapeFromDuckovCoopMod），它们可能分先后加载，
            // 而我们需要的类型横跨两者（NetService 只在后者里）。
            // 所以这里一直等到**真正激活**才退订，见 OnAssemblyLoad。
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            _watchingForApi = true;

            // ⚠ 这条**刻意用 Debug.Log 而不是 CoopLog**：走到这里说明联机模组不在，
            //   那层日志过滤器也就没被装上，普通日志看得见。
            //   反过来若在这里借错误级别，就会给**每个单机玩家每次启动刷一条红字**。
            //   判据：**只有在联机 API 已激活时才借错误级别**（那时过滤器才是活的）。
            Debug.Log($"{LogTag} 未检测到联机模组，按单机模式运行" +
                      "（若联机模组之后加载，会自动接入）。");
        }

        /// <summary>停机：退订、解绑回调。可重复调用。</summary>
        public static void Shutdown()
        {
            if (_watchingForApi)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                _watchingForApi = false;
            }

            if (_aiSpawnedEvent != null && _aiSpawnedHandler != null)
            {
                try
                {
                    _aiSpawnedEvent.RemoveEventHandler(null, _aiSpawnedHandler);
                }
                catch (Exception ex)
                {
                    CoopLog.Info($"退订 AiSpawned 失败（忽略）: {ex.Message}");
                }
            }

            _aiSpawnedEvent = null;
            _aiSpawnedHandler = null;

            if (_messageSubscription != null)
            {
                try
                {
                    _messageSubscription.Dispose();
                }
                catch (Exception ex)
                {
                    CoopLog.Info($"注销消息处理器失败（忽略）: {ex.Message}");
                }
            }

            // 先让业务模块收尾（它要打一条摘要，此刻日志与通道都还得是活的）。
            try
            {
                CoopEliteSync.Shutdown();
            }
            catch (Exception ex)
            {
                CoopLog.Warn($"联机模块收尾失败（忽略）: {ex.Message}");
            }

            _messageSubscription = null;
            _broadcast = null;
            _sendToServer = null;
            _isServer = null;
            _networkStarted = null;
            Active = false;
            _mismatchReported = false;

            // ⚠ **必须重置**：否则模组停用后再启用时 Initialize 会直接早退，
            // 而这个模块已经在上面的 Shutdown 里把自己拆干净了——结果是
            // 「重新启用后联机兼容永久失效」，且不报错、不留日志（静默失效）。
            _initialized = false;
        }

        /// <summary>把一段自定义载荷广播给全体客户端。未激活时静默丢弃（返回 false）。</summary>
        public static bool Broadcast(byte[] payload)
        {
            if (!Active || _broadcast == null || payload == null) return false;
            _broadcast(payload);
            return true;
        }

        /// <summary>把一段自定义载荷发给主机。未激活时静默丢弃（返回 false）。</summary>
        public static bool SendToServer(byte[] payload)
        {
            if (!Active || _sendToServer == null || payload == null) return false;
            _sendToServer(payload);
            return true;
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (!Active) TryActivate();

            // 激活成功就没必要再听下去了，就地退订，不做常驻监听。
            if (Active && _watchingForApi)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                _watchingForApi = false;
            }
        }

        /// <summary>
        /// 探测并接入。**幂等**：已激活直接返回 true。
        ///
        /// <para><b>程序集还没加载齐时不算失败</b>：联机模组的两个程序集
        /// （<c>EscapeFromDuckovModApi</c> 与 <c>EscapeFromDuckovCoopMod</c>）
        /// 可能分先后加载，所以"找不到类型"要区分两种情况——
        /// <b>还没到</b>（静默，等下一次汇编加载再试）与
        /// <b>到了但形状不对</b>（记一次错，那是契约漂移）。</para>
        ///
        /// <para>任何异常都只记日志并退回单机，绝不抛出——
        /// 联机兼容是附加能力，它挂掉不该影响单机玩法。</para>
        /// </summary>
        private static bool TryActivate()
        {
            if (Active) return true;

            // ⚠ **按全名扫所有已加载程序集**，不假定类型属于哪个程序集。
            //   实测（2026-09-19）：ModNetworkApi / ModMessageContext / ModApiEvents 在
            //   EscapeFromDuckovModApi.dll 里，而 NetService 在 EscapeFromDuckovCoopMod.dll 里。
            //   早先要求"四个类型同属一个程序集"，于是必定失败。
            var netApi = FindType(NetApiTypeName);
            var events = FindType(EventsTypeName);
            var context = FindType(ContextTypeName);
            var netService = FindType(NetServiceTypeName);

            if (netApi == null || events == null || context == null || netService == null)
            {
                if (IsAnyCoopAssemblyLoaded() && !_mismatchReported)
                {
                    _mismatchReported = true;
                    CoopLog.Info("检测到联机模组，但它里面找不到本模组依赖的类型，将按单机运行。" +
                                 "（联机模组的版本可能已变，本模组的联机兼容需要同步更新）\n" +
                                 $"  缺的类型：" +
                                 $"{(netApi == null ? NetApiTypeName + " " : string.Empty)}" +
                                 $"{(events == null ? EventsTypeName + " " : string.Empty)}" +
                                 $"{(context == null ? ContextTypeName + " " : string.Empty)}" +
                                 $"{(netService == null ? NetServiceTypeName : string.Empty)}");
                }

                return false;
            }

            try
            {
                Activate(netApi, events, context, netService);
                Active = true;
                // 已激活 ⇒ 联机模组的日志过滤器是活的 ⇒ 必须走 CoopLog 才看得见。
                CoopLog.Info($"已接入联机模组 API（{netApi.Assembly.GetName().Name}），" +
                             "精英词条将随 AI 同步广播。");
                return true;
            }
            catch (Exception ex)
            {
                if (!_mismatchReported)
                {
                    _mismatchReported = true;
                    CoopLog.Info("检测到联机模组，但接入其 API 失败，将按单机运行。" +
                                 $"（联机模组的版本可能已变，本模组的联机兼容需要同步更新）\n{ex}");
                }

                return false;
            }
        }

        private static bool IsAnyCoopAssemblyLoaded()
            => FindAssembly(ApiAssemblyName) != null || FindAssembly(MainAssemblyName) != null;

        private static Assembly FindAssembly(string simpleName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (assembly == null) continue;

                string name;
                try
                {
                    name = assembly.GetName().Name;
                }
                catch
                {
                    continue;   // 动态程序集可能取不到名字，跳过
                }

                if (string.Equals(name, simpleName, StringComparison.Ordinal))
                    return assembly;
            }

            return null;
        }

        /// <summary>按全名在所有已加载程序集里找类型（不限定属于哪个程序集，理由见 <see cref="TryActivate"/>）。</summary>
        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = null;
                try
                {
                    type = assemblies[i]?.GetType(fullName, false);
                }
                catch
                {
                    continue;   // 个别程序集反射会抛（动态程序集等），跳过即可
                }

                if (type != null) return type;
            }

            return null;
        }

        private static void Activate(Type netApi, Type events, Type context, Type netService)
        {
            // 1) 主机/客户端角色判断
            _isServer = BuildIsServerProbe(netService);
            _networkStarted = BuildNetworkStartedProbe(netService);

            // 2) 发送通道——主机用 Broadcast（一对多），客户端用 SendToServer（一对一）
            var broadcastMethod = FindSenderMethod(netApi, "Broadcast", 3);
            var writerType = broadcastMethod.GetParameters()[1].ParameterType.GetGenericArguments()[0];
            _broadcast = BuildSender(broadcastMethod, writerType);

            var sendToServerMethod = FindSenderMethod(netApi, "SendToServer", 2);
            _sendToServer = BuildSender(sendToServerMethod, writerType);

            // 3) 接收通道
            _messageSubscription = RegisterMessageHandler(netApi, context);

            // 4) AI 上线事件
            SubscribeAiSpawned(events);

            // 5) 通知上层的业务模块：通道已就绪
            CoopEliteSync.Initialize();
        }

        /// <summary>
        /// <c>NetService.Instance.IsServer</c> 的取值探针。
        ///
        /// <para>取 <c>Instance</c> 时可能为 null——联机模组的 <c>NetService</c> 在它自己的
        /// <c>OnEnable</c> 里才给 API 装 backend（<c>ModNetworkApi.SetBackend</c>），
        /// 在那之前我们是探不到"角色"的。null 一律当"不是主机"，
        /// 于是本模组在那段时间<b>不发广播</b>——宁可漏发（响的），不要误发。</para>
        /// </summary>
        private static Func<bool> BuildIsServerProbe(Type netServiceType)
        {
            var getInstance = BuildStaticMemberGetter(netServiceType, "Instance");
            var getIsServer = BuildInstanceMemberGetter(netServiceType, "IsServer");

            return () =>
            {
                var service = getInstance();
                if (service == null) return false;
                return (bool)getIsServer(service);
            };
        }

        /// <summary>
        /// 取静态成员的读取器。**字段与属性都接受**。
        ///
        /// <para>⚠ <b>这一条是实测踩出来的</b>：联机模组的 <c>NetService.Instance</c> 是
        /// <c>public static NetService Instance;</c>——一个 <b>字段</b>，不是属性
        /// （<c>Main\NetService.cs:35</c>），而同一个类里的 <c>IsServer</c> 却是属性
        /// （同文件 <c>:66</c>）。只认属性的写法会在这里拿到 null，
        /// 表现为「联机模组明明装了，本模组却一直按单机跑」。</para>
        /// </summary>
        private static Func<object> BuildStaticMemberGetter(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (property != null) return () => property.GetValue(null);

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field != null) return () => field.GetValue(null);

            throw new MissingMemberException(type.FullName, name);
        }

        /// <summary>
        /// <c>NetService.networkStarted</c> 的取值探针——**用它是为了把"装了联机模组但单机玩"
        /// 与"真的在联机局里当客户端"区分开**，见 <see cref="NetworkStarted"/>。
        ///
        /// <para>实测该成员在 <c>Main/NetService.cs:42</c> 是 <c>public bool networkStarted;</c>
        /// （**字段**），同文件 <c>:67</c> 另有一个 <c>NetworkStarted</c> 属性转发它。
        /// 两个都试，取到哪个都行。</para>
        /// </summary>
        private static Func<bool> BuildNetworkStartedProbe(Type netServiceType)
        {
            var getInstance = BuildStaticMemberGetter(netServiceType, "Instance");
            var getStarted = BuildInstanceBoolGetter(netServiceType, "NetworkStarted", "networkStarted");

            return () =>
            {
                var service = getInstance();
                return service != null && getStarted(service);
            };
        }

        /// <summary>取实例 bool 成员的读取器：先找属性，再找字段。两者都接受，理由同 <see cref="BuildStaticMemberGetter"/>。</summary>
        private static Func<object, bool> BuildInstanceBoolGetter(Type type, string propertyName, string fieldName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return target => (bool)property.GetValue(target);

            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return target => (bool)field.GetValue(target);

            throw new MissingMemberException(type.FullName, $"{propertyName} / {fieldName}");
        }

        /// <summary>取实例成员的读取器。字段与属性都接受，理由同上。</summary>
        private static Func<object, object> BuildInstanceMemberGetter(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return target => property.GetValue(target);

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return target => field.GetValue(target);

            throw new MissingMemberException(type.FullName, name);
        }

        /// <summary>
        /// 找形如 <c>(string channel, Action&lt;NetDataWriter&gt; builder, …)</c> 的发送方法。
        ///
        /// <para>按<b>签名形状</b>找而不是按名字硬编码参数类型：<c>NetDataWriter</c> 来自 LiteNetLib，
        /// 是本模组编译期引用不到的类型。从方法签名里反解出它，比写死
        /// <c>"LiteNetLib.Utils.NetDataWriter"</c> 稳——联机模组换 LiteNetLib 版本时不会静默失效。</para>
        ///
        /// <para>实测两个目标方法的形状：<c>Broadcast(string, Action&lt;NetDataWriter&gt;, bool)</c>
        /// 与 <c>SendToServer(string, Action&lt;NetDataWriter&gt;)</c>——只差尾部的
        /// <c>includeServer</c> 参数，所以用参数个数区分。</para>
        /// </summary>
        private static MethodInfo FindSenderMethod(Type netApiType, string name, int parameterCount)
        {
            foreach (var method in netApiType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != name) continue;

                var parameters = method.GetParameters();
                if (parameters.Length != parameterCount) continue;
                if (parameters[0].ParameterType != typeof(string)) continue;

                var builderType = parameters[1].ParameterType;
                if (!builderType.IsGenericType || builderType.GetGenericArguments().Length != 1) continue;

                // 尾部多出来的那个参数（Broadcast 的 includeServer）必须是 bool，
                // 否则可能误配到别的重载上。
                for (int i = 2; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != typeof(bool)) goto next;
                }

                return method;

            next: ;
            }

            throw new MissingMethodException(netApiType.FullName, $"{name}（{parameterCount} 参）");
        }

        private static Action<byte[]> BuildSender(MethodInfo method, Type writerType)
        {
            var bridgeType = typeof(PayloadWriter<>).MakeGenericType(writerType);
            var bridge = (PayloadWriter)Activator.CreateInstance(bridgeType);

            var writeTo = bridgeType.GetMethod(nameof(PayloadWriter<object>.WriteTo));
            var writerAction = Delegate.CreateDelegate(
                typeof(Action<>).MakeGenericType(writerType), bridge, writeTo);

            var args = new object[method.GetParameters().Length];
            args[0] = CoopEliteSync.ChannelName;
            args[1] = writerAction;
            // 尾部若还有参数，那是 Broadcast 的 includeServer：要 true，
            // 否则主机的本地回环收不到自己的消息（我们依赖它做一致性检查）。
            for (int i = 2; i < args.Length; i++)
                args[i] = true;

            return payload =>
            {
                // 复用同一个 bridge 实例：这些发送方法是同步的，改完字段立刻调用，不存在竞态。
                bridge.Bytes = payload;
                method.Invoke(null, args);
            };
        }

        private static IDisposable RegisterMessageHandler(Type netApiType, Type contextType)
        {
            var handlerType = typeof(Action<>).MakeGenericType(contextType);
            var registerMethod = netApiType.GetMethod("RegisterHandler", new[] { typeof(string), handlerType });

            if (registerMethod == null)
                throw new MissingMethodException(netApiType.FullName, "RegisterHandler(string, Action<ModMessageContext>)");

            var coreMethod = typeof(CoopApi)
                .GetMethod(nameof(OnNetworkMessageCore), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(contextType);

            var handler = Delegate.CreateDelegate(handlerType, coreMethod);

            return (IDisposable)registerMethod.Invoke(null, new object[] { CoopEliteSync.ChannelName, handler });
        }

        /// <summary>
        /// 消息处理的泛型外壳。
        ///
        /// <para>存在的唯一理由是 <c>ModMessageContext</c> 是本模组编译期引用不到的类型，
        /// 而它的 <c>Payload</c>（<c>ReadOnlyMemory&lt;byte&gt;</c>）与 <c>IsServer</c>（<c>bool</c>）
        /// <b>都是 BCL 类型</b>——所以只需要在这个壳里反射取一次，内层就是强类型代码。</para>
        ///
        /// <para><c>PropertyInfo.GetValue</c> 对结构体会装箱，这里<b>刻意不优化</b>：
        /// 消息频率是"每次精英生成"，不是每帧。</para>
        /// </summary>
        private static void OnNetworkMessageCore<TContext>(TContext context)
        {
            try
            {
                var type = typeof(TContext);

                var payloadProperty = type.GetProperty("Payload", BindingFlags.Public | BindingFlags.Instance);
                var isServerProperty = type.GetProperty("IsServer", BindingFlags.Public | BindingFlags.Instance);

                if (payloadProperty == null || isServerProperty == null)
                {
                    CoopLog.Info($"联机消息类型 {type.FullName} 上找不到 Payload / IsServer，" +
                                 "联机模组的 API 可能已变。");
                    return;
                }

                var payload = (ReadOnlyMemory<byte>)payloadProperty.GetValue(context);
                var isServer = (bool)isServerProperty.GetValue(context);

                CoopEliteSync.OnNetworkMessage(payload, isServer);
            }
            catch (Exception ex)
            {
                // 隔离：这是联机模组派发链上的回调，抛出去会带走它的整个消息分发。
                CoopLog.Info($"处理联机消息失败（已隔离）: {ex}");
            }
        }

        private static void SubscribeAiSpawned(Type eventsType)
        {
            var eventInfo = eventsType.GetEvent("AiSpawned", BindingFlags.Public | BindingFlags.Static);
            if (eventInfo == null)
                throw new MissingMemberException(eventsType.FullName, "AiSpawned");

            // Action<int, CharacterMainControl> 两端都是编译期可引用的类型，不需要泛型桥。
            _aiSpawnedHandler = CoopEliteSync.OnAiSpawned;
            eventInfo.AddEventHandler(null, _aiSpawnedHandler);
            _aiSpawnedEvent = eventInfo;
        }

        /// <summary>
        /// 承载待发送字节的桥。
        ///
        /// <para>联机 API 的发送签名是 <c>Action&lt;NetDataWriter&gt;</c>，而 <c>NetDataWriter</c>
        /// 来自 LiteNetLib——本模组既没有也不该有那个依赖。于是用泛型类在运行期闭出
        /// <c>Action&lt;TWriter&gt;</c>，实际只调它的 <c>Put(byte[])</c>。</para>
        ///
        /// <para>载荷就是<b>原样字节</b>：已核实 LiteNetLib 的 <c>NetDataWriter.Put(byte[])</c>
        /// 不写长度前缀（<c>PutBytesWithLength</c> 才写），而联机模组把 payloadBuilder 写入的内容
        /// 原样放进消息（<c>ModNetworkApi.BuildPayload</c> → <c>writer.CopyData()</c>），
        /// 所以对端 <c>Payload</c> 拿到的就是这里的 <see cref="Bytes"/>。</para>
        /// </summary>
        private abstract class PayloadWriter
        {
            public byte[] Bytes;
        }

        private sealed class PayloadWriter<TWriter> : PayloadWriter
        {
            private static readonly Action<TWriter, byte[]> s_putBytes = BuildPutBytes();

            private static Action<TWriter, byte[]> BuildPutBytes()
            {
                var put = typeof(TWriter).GetMethod("Put", new[] { typeof(byte[]) });
                if (put == null)
                    throw new MissingMethodException(typeof(TWriter).FullName, "Put(byte[])");

                return (Action<TWriter, byte[]>)Delegate.CreateDelegate(
                    typeof(Action<TWriter, byte[]>), put);
            }

            public void WriteTo(TWriter writer)
            {
                s_putBytes(writer, Bytes);
            }
        }
    }
}
