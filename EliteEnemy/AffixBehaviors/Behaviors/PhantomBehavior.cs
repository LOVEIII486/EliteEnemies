using Duckov;
using UnityEngine;

namespace EliteEnemies.EliteEnemy.AffixBehaviors.Behaviors
{
    /// <summary>
    /// 幻听
    /// </summary>
    public class PhantomBehavior : AffixBehaviorBase, IUpdateableAffixBehavior
    {
        public override string AffixName => "Phantom";
        

        private const float TRIGGER_DISTANCE = 50f;             // 激活距离

        // 远距离时的干扰频率
        private const float TIMER_FAR_MIN = 2.0f;               
        private const float TIMER_FAR_MAX = 5.0f;

        // 近距离时的干扰频率 
        private const float TIMER_CLOSE_MIN = 0.5f;             
        private const float TIMER_CLOSE_MAX = 1.5f;

        private const float FAKE_DIST_MIN = 8f;                // 伪造声源距离玩家最小距离
        private const float FAKE_DIST_MAX = 20f;               // 伪造声源距离玩家最大距离
        private const float BEHIND_ANGLE_HALF = 100f;          // 后方扇区半角

        private const float VOICE_CHANCE = 0.3f;              // 语音触发概率
        private const float SOUND_RADIUS = 15f;                // 声音传播半径

        private float _timer;
        private float _nextInterval;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            base.OnEliteInitialized(character);
            ResetDynamicTimer(character);
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            var player = CharacterMainControl.Main;
            if (player == null) return;

            float dist = Vector3.Distance(character.transform.position, player.transform.position);
            
            if (dist > TRIGGER_DISTANCE) return;

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
                _nextInterval = TIMER_FAR_MAX;
                return;
            }

            float dist = Vector3.Distance(owner.transform.position, player.transform.position);
            
            // 计算距离权重系数
            float t = Mathf.Clamp01(dist / TRIGGER_DISTANCE);
            
            float currentMin = Mathf.Lerp(TIMER_CLOSE_MIN, TIMER_FAR_MIN, t);
            float currentMax = Mathf.Lerp(TIMER_CLOSE_MAX, TIMER_FAR_MAX, t);

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
            float randomRot = Random.Range(-BEHIND_ANGLE_HALF, BEHIND_ANGLE_HALF);
            Vector3 spawnDir = Quaternion.AngleAxis(randomRot, Vector3.up) * backwardDir;
            
            float randomDist = Random.Range(FAKE_DIST_MIN, FAKE_DIST_MAX);
            Vector3 fakePos = player.transform.position + (spawnDir * randomDist);

            // 3. 生成虚假声纹
            AISound fakeSound = new AISound
            {
                pos = fakePos,
                radius = SOUND_RADIUS,
                fromTeam = Teams.usec,
                soundType = SoundTypes.combatSound,
                fromCharacter = null,
                fromObject = null
            };
            AIMainBrain.MakeSound(fakeSound);

            // 4. 播放干扰语音
            if (Random.value < VOICE_CHANCE)
            {
                string voicePath = GetVoicePath(owner.AudioVoiceType);
                AudioManager.Post(voicePath, fakePos);
            }
        }
        
        private string GetVoicePath(AudioManager.VoiceType type)
        {
            switch (type)
            {
                case AudioManager.VoiceType.Duck:    return "Char/Voice/vo_duck_surprise";
                case AudioManager.VoiceType.Robot:   return "Char/Voice/vo_robot_surprise";
                case AudioManager.VoiceType.Wolf:    return "Char/Voice/vo_wolf_surprise";
                case AudioManager.VoiceType.Chicken: return "Char/Voice/vo_chicken_surprise";
                case AudioManager.VoiceType.Crow:    return "Char/Voice/vo_crow_surprise";
                case AudioManager.VoiceType.Eagle:   return "Char/Voice/vo_eagle_surprise";
                default:                             return "Char/Voice/vo_duck_surprise";
            }
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}