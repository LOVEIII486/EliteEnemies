using System;
using System.Collections.Generic;
using UnityEngine;

namespace EliteEnemies.Modifiers
{
    /// <summary>
    /// AI 字段覆盖的状态容器：**原值快照** + **待写入队列**。
    ///
    /// <para>AI 字段是「直接覆盖」语义（不像 Stat 有游戏自带的 source 机制），
    /// 原值没有留底就**永远还原不回来**。所以这个类存在的唯一理由就是留底。</para>
    ///
    /// <para><b>为什么是挂在角色身上的组件，而不是静态字典</b>：生命周期随角色
    /// GameObject 走，销毁即消失，不需要任何手工清理。静态字典按 Unity 对象做 key
    /// 会累积、且外层 key 永不删除——QuackMod 调研报告 §4.15 / §4.16 记录的正是这类缺陷。</para>
    ///
    /// <para><b>为什么不用 ConditionalWeakTable</b>：它的值是还原闭包，闭包捕获 AI 组件，
    /// AI 组件反向引用角色 GameObject，构成「值 → 键」引用，条目永远不会被回收，
    /// 等于把静态字典的泄漏换个形式重演。</para>
    ///
    /// <para><b>克隆体会不会把它复制走</b>：不会。克隆走
    /// <c>EggSpawnHelper.SpawnClone</c>——改 <c>CharacterRandomPreset</c> 后**重新生成**角色
    /// （<c>EggSpawnHelper.cs:292-300</c>），不是 <c>Instantiate</c> 角色对象本身。</para>
    /// </summary>
    internal sealed class AIFieldOverrideState : MonoBehaviour
    {
        private const string LogTag = "[EliteEnemies.AIFieldOverrideState]";

        /// <summary>取不到 AI 组件时最多重试多少帧，超过就报错并丢弃（防止无声空转与队列无限增长）。</summary>
        private const int MaxWaitFrames = 60;

        private CharacterMainControl _character;

        /// <summary>
        /// 字段访问器 → 该字段的记账。
        ///
        /// <para>⚠ <b>按字段记账，而不是按 source 记快照链</b>——这是刻意的。
        /// 若每个 source 各存一份「写前原值」，还原就必须**后进先出**才对；
        /// 而本工程的清理顺序是 <c>EliteBehaviorComponent</c> 正序遍历
        /// （初始化 <c>:48</c>、<c>OnDestroy</c> 清理 <c>:194</c>），正好是先进先出。
        /// 两者错配的结果是：Berserk + Nimble 都写 <c>canDash</c> 时，正序还原会留下
        /// <c>canDash = true</c>（原值是 false）——正是本模块要消灭的那类残留，
        /// 而且它取决于模块**外部**的遍历顺序，非常隐蔽。</para>
        ///
        /// <para>按字段记账后，还原只发生在「该字段已无任何 source 持有」时，
        /// 与顺序完全无关；顺带也修好了「撤销某一个 source 却把别的 source 的加成
        /// 一并抹掉」的问题。</para>
        /// </summary>
        private readonly Dictionary<object, FieldState> _fieldStates = new Dictionary<object, FieldState>();

        /// <summary>尚未执行的写入。AI 组件在角色生成期可能还没就绪，故推迟到下一帧。</summary>
        private readonly List<Pending> _pending = new List<Pending>();

        private int _waitFrames;

        private sealed class FieldState
        {
            /// <summary>把该字段还原成**最早那次写入之前**的原值。</summary>
            public Action Restore;

            /// <summary>当前还在改这个字段的 source 集合。</summary>
            public readonly HashSet<object> Owners = new HashSet<object>();
        }

        private readonly struct Pending
        {
            public readonly object Source;
            public readonly Action<AICharacterController> Write;

            public Pending(object source, Action<AICharacterController> write)
            {
                Source = source;
                Write = write;
            }
        }

        /// <summary>
        /// 取（必要时创建）该角色的状态组件。
        /// </summary>
        public static AIFieldOverrideState For(CharacterMainControl character)
        {
            var state = character.GetComponent<AIFieldOverrideState>();
            if (state == null)
            {
                state = character.gameObject.AddComponent<AIFieldOverrideState>();
            }

            // AddComponent 会立刻跑 Awake，但那时拿不到 character，所以在这里补上。
            if (state._character == null) state._character = character;
            return state;
        }

        // 默认不需要每帧跑；只有队列非空时才启用自己。
        //
        // ⚠ 必须判空：若组件是加在**未激活**的 GameObject 上，Unity 会把 Awake 推迟到
        //   激活时再跑，而那时 Enqueue 可能已经执行过。无条件 enabled = false 会把
        //   刚排好队的写入**永久卡住**。
        private void Awake()
        {
            if (_pending.Count == 0) enabled = false;
        }

        /// <summary>
        /// 排队一次写入。实际执行推迟到 <see cref="LateUpdate"/>。
        /// </summary>
        public void Enqueue(object source, Action<AICharacterController> write)
        {
            _pending.Add(new Pending(source, write));
            enabled = true;
        }

        /// <summary>
        /// 撤销某个 source 对 AI 字段的修改：
        /// ① 丢弃它尚未执行的排队写入；
        /// ② 从各字段的持有者里摘掉它，**只有当某字段再没有别的持有者时才还原**。
        ///
        /// <para>重复调用无副作用。与还原顺序无关（见 <see cref="_fieldStates"/> 的说明）。</para>
        /// </summary>
        public void Revert(object source)
        {
            if (source == null) return;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Equals(_pending[i].Source, source)) _pending.RemoveAt(i);
            }

            // 先收集再删除：不能一边遍历 _fieldStates 一边改它。
            List<object> finished = null;
            foreach (var pair in _fieldStates)
            {
                if (!pair.Value.Owners.Remove(source)) continue;
                if (pair.Value.Owners.Count > 0) continue;

                pair.Value.Restore();
                (finished ?? (finished = new List<object>())).Add(pair.Key);
            }

            if (finished != null)
            {
                foreach (var field in finished) _fieldStates.Remove(field);
            }
        }

        private void LateUpdate()
        {
            if (_pending.Count == 0)
            {
                _waitFrames = 0;
                enabled = false;
                return;
            }

            var ai = AIFieldOverrides.GetAI(_character);
            if (ai == null)
            {
                // 还没就绪就下一帧再试——**不要**静默丢弃。但也不能无限等：
                // 既会每帧空转，又会让队列无上限增长。超过上限就报错并丢弃。
                if (++_waitFrames < MaxWaitFrames) return;

                Debug.LogError($"{LogTag} 等待 {MaxWaitFrames} 帧仍取不到 AICharacterController，" +
                               $"丢弃 {_pending.Count} 项 AI 字段修改。角色={(_character == null ? "null" : _character.name)}");
                _pending.Clear();
                _waitFrames = 0;
                enabled = false;
                return;
            }

            // 逐条隔离：单条出错不影响其余，也不会让整批在下一帧重放。
            // ⚠ 重放是有害的：float 是**乘算**语义，同一批再写一次就是再乘一次，数值会静默漂移。
            for (int i = 0; i < _pending.Count; i++)
            {
                try
                {
                    _pending[i].Write(ai);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogTag} 应用 AI 字段修改失败: {ex}");
                }
            }

            _pending.Clear();
            _waitFrames = 0;
            enabled = false;
        }

        /// <summary>
        /// 写 float 字段。**第一次**写某个字段前留底；之后（不管谁写）都复用那份底稿。
        /// </summary>
        public void WriteFloat(object source, FieldRef<AICharacterController, float> field,
            AICharacterController ai, float value, bool multiply)
        {
            EnsureField(source, field, ai);
            field(ai) = multiply ? field(ai) * value : value;
        }

        /// <summary>写 bool 字段（覆盖语义，没有「乘」这一说）。</summary>
        public void WriteBool(object source, FieldRef<AICharacterController, bool> field,
            AICharacterController ai, bool value)
        {
            EnsureField(source, field, ai);
            field(ai) = value;
        }

        private void EnsureField<T>(object source, FieldRef<AICharacterController, T> field,
            AICharacterController ai)
        {
            if (!_fieldStates.TryGetValue(field, out var state))
            {
                T original = field(ai);
                var character = _character;
                state = new FieldState
                {
                    Restore = () =>
                    {
                        // 还原时重新取一次 AI：期间角色可能被回收重建过。
                        var current = AIFieldOverrides.GetAI(character);
                        if (current != null) field(current) = original;
                    }
                };
                _fieldStates[field] = state;
            }

            state.Owners.Add(source);
        }
    }
}
