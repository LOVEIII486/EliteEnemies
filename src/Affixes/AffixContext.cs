using Duckov.Buffs;
using ECM2;
using EliteEnemies.Core;
using UnityEngine;

namespace EliteEnemies.Affixes
{
    /// <summary>
    /// 一个精英身上「常用引用」的解析结果——**每个敌人只解析一次**，行为直接取用。
    ///
    /// <para><b>为什么要有它</b>：见 <c>docs\词条模块审查与设计.md</c> §6。词条行为原先各自在
    /// <c>OnEliteInitialized</c> 里 <c>GetComponent</c>，同一批引用被反复查找。更值得修的是
    /// 「游戏其实已经持有、或已经给了口子，却还被重新找一遍」的那些——例如
    /// <see cref="BuffManager"/> 走的是官方方法 <c>CharacterMainControl.GetBuffManager()</c>
    /// （<c>CharacterMainControl.cs:2647</c>），而不是 <c>GetComponent&lt;CharacterBuffManager&gt;()</c>。</para>
    ///
    /// <para><b>每一项都注明取法与依据</b>（游戏公开字段 / 官方方法 / 只能自己 GetComponent）。
    /// 改这里之前先回查 <c>..\DuckovSource-ILSpy\</c>，不要照抄猜测。</para>
    ///
    /// <para>⚠ <b>允许为 null</b>：不是每个角色都有全部组件。这里只省掉重复查找，
    /// **不保证存在**，行为使用前仍要判空。</para>
    /// </summary>
    public sealed class AffixContext
    {
        /// <summary>行为所依附的精英本体。</summary>
        public CharacterMainControl Character { get; }

        /// <summary>
        /// 行为组件本身。用途是**当协程宿主**——见
        /// <see cref="AffixBehaviorBase.StartManagedCoroutine"/>。
        /// </summary>
        public EliteBehaviorComponent Host { get; }

        /// <summary>生命组件。<c>CharacterMainControl.Health</c>（公开成员，直接取）。</summary>
        public Health Health { get; }

        /// <summary>AI 控制器。<c>CharacterMainControl.aiCharacterController</c>（公开字段，<c>:68</c>）。</summary>
        public AICharacterController Ai { get; }

        /// <summary>主伤害接收器。<c>CharacterMainControl.mainDamageReceiver</c>（公开字段，<c>:151</c>）。</summary>
        public DamageReceiver DamageReceiver { get; }

        /// <summary>角色模型。<c>CharacterMainControl.characterModel</c>（公开字段，<c>:64</c>）。</summary>
        public CharacterModel Model { get; }

        /// <summary>
        /// 游戏自己的移动控制。<c>CharacterMainControl.movementControl</c>（公开字段，<c>:43</c>）。
        /// 它有 <c>IsOnGround</c> / <c>Velocity</c> 等公开 API（<c>Movement.cs:85</c> / <c>:109</c>）——
        /// 想读脚是否着地、当前速度，**先看它**，别自己算。
        /// </summary>
        public Movement Movement { get; }

        /// <summary>
        /// ECM2 的角色移动组件（第三方角色控制器，编译进 <c>Assembly-CSharp</c>）。
        /// 取法仍是 <c>GetComponent</c>：游戏的 <c>Movement</c> 内部持有它但那是 private 字段
        /// （<c>Movement.cs:9</c>），本工程**未能**从源码确认它一定被赋值，故不依赖它。
        /// </summary>
        public CharacterMovement Ecm2Movement { get; }

        /// <summary>
        /// 状态（Buff）管理器。**走官方方法** <c>CharacterMainControl.GetBuffManager()</c>
        /// （<c>CharacterMainControl.cs:2647</c>，内部直接返回私有字段），不需要 GetComponent。
        /// </summary>
        public CharacterBuffManager BuffManager { get; }

        /// <summary>
        /// 声音组件。用 <c>GetComponent</c> 是对的——**游戏自己就是这么拿的**
        /// （<c>CharacterModelAudioPoster.cs:40</c>），没有更直接的口子。
        /// </summary>
        public CharacterSoundMaker SoundMaker { get; }

        /// <summary>
        /// 刚体。用 <c>GetComponent</c>：ECM2 的角色控制器会带一个刚体在角色根上，
        /// 但游戏没有把它暴露成公开成员。
        /// </summary>
        public Rigidbody Body { get; }

        /// <summary>精英标记组件（本模组自己的）。<see cref="EliteEnemyCore.TagAsElite"/> 在挂行为组件之前就加好了。</summary>
        public EliteMarker Marker { get; }

        /// <remarks>
        /// 有意**没有**放进来的两个，别"顺手补上"：
        /// <list type="bullet">
        /// <item><c>NavMeshAgent</c>——游戏代码里**一次都没引用**它（AI 走 A*：
        /// <c>AstarPath</c> / <c>Seeker</c> / <c>ABPath</c>，见 <c>AICharacterController.cs:697-710</c>），
        /// 行为里那几处 <c>GetComponent&lt;NavMeshAgent&gt;()</c> 要么拿到 null、要么作用在一个
        /// 游戏自己都不用的组件上。要停 AI 的移动，应走游戏自己的导航接口——见
        /// <c>docs\词条模块审查与设计.md</c> §6 的记录。</item>
        /// <item><c>GraphOwner</c>——NodeCanvas（第三方行为树）的类型，只被个别行为用到，
        /// 不属于"人人都在找"的那一类；等审到那个行为时再决定怎么收。</item>
        /// </list>
        /// </remarks>
        internal AffixContext(EliteBehaviorComponent host, CharacterMainControl character)
        {
            Host = host;
            Character = character;

            Health = character.Health;
            Ai = character.aiCharacterController;
            DamageReceiver = character.mainDamageReceiver;
            Model = character.characterModel;
            Movement = character.movementControl;
            Marker = character.GetComponent<EliteMarker>();

            BuffManager = character.GetBuffManager();
            SoundMaker = character.GetComponent<CharacterSoundMaker>();
            Body = character.GetComponent<Rigidbody>();
            Ecm2Movement = Movement != null ? Movement.GetComponent<CharacterMovement>() : null;
        }
    }
}
