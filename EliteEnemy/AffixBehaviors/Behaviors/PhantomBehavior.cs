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
        
        private const float TRIGGER_DISTANCE = 45f;             // 玩家距离精英怪多少米内开始产生幻听

        private const float TIMER_MIN = 0.5f;                   // 最小间隔
        private const float TIMER_MAX = 3.0f;                   // 最大间隔

        private const float FAKE_DIST_MIN = 8f;                // 伪造声源距离玩家最小距离
        private const float FAKE_DIST_MAX = 22f;                // 伪造声源距离玩家最大距离
        private const float BEHIND_ANGLE_HALF = 100f;            // 后方扇区半角

        private const float VOICE_CHANCE = 0.3f;                // 产生声纹时，播放叫声的概率
        private const float SOUND_RADIUS = 15f;                 // 声音传播半径

        private float _timer;
        private float _nextInterval;

        public override void OnEliteInitialized(CharacterMainControl character)
        {
            base.OnEliteInitialized(character);
            ResetTimer();
        }

        public void OnUpdate(CharacterMainControl character, float deltaTime)
        {
            var player = CharacterMainControl.Main;
            if (player == null) return;

            float sqrDist = (character.transform.position - player.transform.position).sqrMagnitude;
            if (sqrDist > TRIGGER_DISTANCE * TRIGGER_DISTANCE) return;

            _timer += deltaTime;
            if (_timer >= _nextInterval)
            {
                TriggerPhantomEffect(player, character);
                _timer = 0;
                ResetTimer();
            }
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

        private void ResetTimer()
        {
            _nextInterval = Random.Range(TIMER_MIN, TIMER_MAX);
        }

        public override void OnEliteDeath(CharacterMainControl character, DamageInfo damageInfo) { }
        public override void OnCleanup(CharacterMainControl character) { }
    }
}