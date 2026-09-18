using Duckov;
using UnityEngine;

namespace EliteEnemies.Affixes.Behaviors
{
    /// <summary>
    /// 幻听
    /// </summary>
    public class PhantomBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Phantom";
        

        private const float TriggerDistance = 50f;             // 激活距离

        // 远距离时的干扰频率
        private const float TimerFarMin = 2.0f;               
        private const float TimerFarMax = 5.0f;

        // 近距离时的干扰频率 
        private const float TimerCloseMin = 0.5f;             
        private const float TimerCloseMax = 1.5f;

        private const float FakeDistMin = 8f;                // 伪造声源距离玩家最小距离
        private const float FakeDistMax = 20f;               // 伪造声源距离玩家最大距离
        private const float BehindAngleHalf = 100f;          // 后方扇区半角

        private const float VoiceChance = 0.3f;              // 语音触发概率
        private const float SoundRadius = 15f;                // 声音传播半径

        private float _timer;
        private float _nextInterval;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            ResetDynamicTimer(character);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            var player = CharacterMainControl.Main;
            if (player == null) return;

            float dist = Vector3.Distance(character.transform.position, player.transform.position);
            
            if (dist > TriggerDistance) return;

            _timer += deltaTime;
            if (_timer >= _nextInterval)
            {
                TriggerPhantomEffect(player, character);
                _timer = 0;
                ResetDynamicTimer(character);
            }
        }
        
        private void ResetDynamicTimer(CharacterMainControl owner)
        {
            var player = CharacterMainControl.Main;
            if (player == null) 
            {
                _nextInterval = TimerFarMax;
                return;
            }

            float dist = Vector3.Distance(owner.transform.position, player.transform.position);
            
            // 计算距离权重系数
            float t = Mathf.Clamp01(dist / TriggerDistance);
            
            float currentMin = Mathf.Lerp(TimerCloseMin, TimerFarMin, t);
            float currentMax = Mathf.Lerp(TimerCloseMax, TimerFarMax, t);

            _nextInterval = Random.Range(currentMin, currentMax);
            
            // Debug.Log($"[Phantom] 距离: {dist:F1}m, 下次干扰间隔: {_nextInterval:F2}s");
        }

        private void TriggerPhantomEffect(CharacterMainControl player, CharacterMainControl owner)
        {
            // 1. 获取玩家视线方向
            Vector3 aimDir = player.CurrentAimDirection;
            aimDir.y = 0;
            aimDir.Normalize();
            if (aimDir.sqrMagnitude < 0.1f) aimDir = Vector3.forward;

            // 2. 从玩家背后选取一个落点
            Vector3 backwardDir = -aimDir;
            float randomRot = Random.Range(-BehindAngleHalf, BehindAngleHalf);
            Vector3 spawnDir = Quaternion.AngleAxis(randomRot, Vector3.up) * backwardDir;
            
            float randomDist = Random.Range(FakeDistMin, FakeDistMax);
            Vector3 fakePos = player.transform.position + (spawnDir * randomDist);

            // 3. 生成虚假声纹
            AISound fakeSound = new AISound
            {
                pos = fakePos,
                radius = SoundRadius,
                fromTeam = Teams.usec,
                soundType = SoundTypes.combatSound,
                fromCharacter = null,
                fromObject = null
            };
            AIMainBrain.MakeSound(fakeSound);

            // 4. 播放干扰语音
            if (Random.value < VoiceChance)
            {
                string voicePath = GetVoicePath(owner.AudioVoiceType, "surprise");
                AudioManager.Post(voicePath, fakePos);
            }
        }
        
        /// <summary>
        /// 惊吓语音的资源路径。**格式照抄游戏**：
        /// <c>"Char/Voice/vo_" + voiceType.ToString().ToLower() + "_" + soundKey</c>
        /// （<c>AudioObject.cs:48</c>）。
        ///
        /// <para>原先这里是手写 switch 逐一举 6 种语音——漏了 <c>coalball</c>
        /// （<c>AudioManager.cs:112-121</c> 的枚举里确实有，而且是全小写那个），
        /// 游戏以后加新语音也照样漏；**每次漏都不报错，只是静默回落到鸭子叫**。
        /// 改用官方格式后自动覆盖全部语音类型。</para>
        ///
        /// <para>为什么不直接调 <c>AudioManager.PostQuak(soundKey, voiceType, gameObject)</c>
        /// （<c>AudioManager.cs:309</c>，Publicizer 可访问）：它只能把声音挂在某个 GameObject 上，
        /// 而本词条要的是**假声源位置**（在玩家身后伪造声源），所以只能用
        /// <c>AudioManager.Post(eventName, position)</c>（<c>:300</c>）。路径格式仍与官方一致。</para>
        /// </summary>
        private static string GetVoicePath(AudioManager.VoiceType type, string soundKey)
            => "Char/Voice/vo_" + type.ToString().ToLower() + "_" + soundKey;



    }
}