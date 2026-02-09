namespace EliteEnemies.EliteEnemy.Core
{
    public static class NPCPresetNames
    {
        public static class Boss
        {
            #region 监狱

            public const string PrisonBoss = "EnemyPreset_Prison_Boss"; // 典狱长

            #endregion

            #region 零号区

            public const string UltraMan = "EnemyPreset_Melee_UltraMan"; // 光之男
            public const string ShortEagle = "EnemyPreset_Boss_ShortEagle"; // 矮鸭

            #endregion

            #region 仓库区

            public const string Deng = "EnemyPreset_Boss_Deng"; // 劳登
            public const string Speedy = "EnemyPreset_Boss_Speedy"; // 急速团长

            #endregion

            #region 农场镇

            public const string ThreeShot = "EnemyPreset_Boss_3Shot"; // 三枪哥
            public const string RPG = "EnemyPreset_Boss_RPG"; // 迷塞尔
            public const string Arcade = "EnemyPreset_Boss_Arcade"; // 暴走街机
            public const string BALeader = "EnemyPreset_Boss_BALeader"; // BA队长
            public const string Fly = "EnemyPreset_Boss_Fly"; // 蝇蝇队长
            public const string Grenade = "EnemyPreset_Boss_Grenade"; // 炸弹狂人
            public const string SenorEngineer = "EnemyPreset_Boss_SenorEngineer"; // 高级工程师
            public const string ServerGuardian = "EnemyPreset_Boss_ServerGuardian"; // 矿长
            public const string Shot = "EnemyPreset_Boss_Shot"; // 喷子
            public const string Vida = "EnemyPreset_Boss_Vida"; // 维达
            public const string SchoolBully = "EnemyPreset_BossMelee_SchoolBully"; // 校霸
            public const string Red = "EnemyPreset_Boss_Red"; // ???

            #endregion

            #region 实验室

            public const string Roadblock = "EnemyPreset_Boss_Roadblock"; // 路障

            #endregion

            #region 风暴区

            public const string Storm1 = "EnemyPreset_Boss_Storm_1_BreakArmor"; // 噗咙噗咙
            public const string Storm2 = "EnemyPreset_Boss_Storm_2_Poison"; // 咕噜咕噜
            public const string Storm3 = "EnemyPreset_Boss_Storm_3_Fire"; // 啪啦啪啦
            public const string Storm4 = "EnemyPreset_Boss_Storm_4_Electric"; // 比利比利
            public const string Storm5 = "EnemyPreset_Boss_Storm_5_Space"; // 口口口口

            #endregion

            #region 船票挑战

            public const string XING = "EnemyPreset_Boss_XING"; // 大兴兴

            #endregion

            #region 雪地船票

            public const string SnowBigIce = "EnemyPreset_Boss_Snow_BigIce"; // 大冰冰
            public const string SpeedyIce = "EnemyPreset_Boss_Speedy_Ice"; // 急冻团长

            #endregion
            
            
            public const string Alex = "EnemyPreset_Boss_Alex"; // 艾力克斯
            public const string Killa = "EnemyPreset_Boss_Killa"; // Killa
            public const string KillaBasaka = "EnemyPreset_Boss_Killa_Basaka"; // Killa 需测试区别
            public const string SnowFleeze = "EnemyPreset_Boss_Snow_Fleeze"; // 弗里兹
            public const string SnowIgny = "EnemyPreset_Boss_Snow_Igny"; // 伊格尼
            public const string SnowMan = "EnemyPreset_Boss_SnowMan"; // 机械雪人
            public const string SnowMan2 = "EnemyPreset_Boss_SnowMan 2"; // 机械雪人
            public const string Tagilla = "EnemyPreset_Boss_Tagilla"; // Tagilla
            public const string TagillaBasaka = "EnemyPreset_Boss_Tagilla_Basaka"; // Tagilla
            public const string WolfKingIce = "EnemyPreset_Boss_WolfKing_Ice"; // 雪原狼王
            
            public static readonly string[] All = 
            { 
                PrisonBoss,
                UltraMan, ShortEagle,
                Deng, Speedy,
                ThreeShot, RPG, Arcade, BALeader, Fly, Grenade, SenorEngineer, ServerGuardian, Shot, Vida, SchoolBully, Red,
                Roadblock,
                Storm1, Storm2, Storm3, Storm4, Storm5,
                XING,
                SnowBigIce, SpeedyIce,
                Alex, Killa, KillaBasaka, SnowFleeze, SnowIgny, SnowMan, SnowMan2, Tagilla, TagillaBasaka, WolfKingIce
            };
        }
        
        public static class Minions
        {
            public const string ShortEagleElite = "EnemyPreset_Boss_ShortEagle_Elete"; // 矮鸭营地的雇佣兵
            
            public const string DengWolf = "EnemyPreset_Boss_Deng_Wolf"; // 劳登的狗
            public const string SpeedyChild = "EnemyPreset_Boss_Speedy_Child"; // 急速团成员
            
            public const string ThreeShotChild = "EnemyPreset_Boss_3Shot_Child"; // 三枪弟
            public const string BALeaderChild = "EnemyPreset_Boss_BALeader_Child"; // 普通BA
            public const string FlyChild = "EnemyPreset_Boss_Fly_Child"; // 蝇蝇队员
            public const string FlyAlone = "EnemyPreset_Boss_Fly_Alone"; // 落单的蝇蝇队员
            public const string SchoolBullyChild = "EnemyPreset_BossMelee_SchoolBully_Child"; // 校友
            
            public const string Storm1Child = "EnemyPreset_Boss_Storm_1_Child"; // 噗咙
            
            public const string XINGChild = "EnemyPreset_Boss_XING_Child"; // 小小兴
            
            public const string SpeedyIceChild = "EnemyPreset_Boss_Speedy_Ice_Child"; // 急冻团员
            
            public static readonly string[] All = 
            { 
                ShortEagleElite,
                DengWolf, SpeedyChild,
                ThreeShotChild, BALeaderChild, FlyChild, FlyAlone, SchoolBullyChild,
                Storm1Child,
                XINGChild,
                SpeedyIceChild
            };
        }
        
        public static class Animal
        {
            public const string Bear = "EnemyPreset_Animal_Bear"; // 熊
            public const string BearGun = "EnemyPreset_Animal_Bear_Gun"; // 熊
            public const string Ghost = "EnemyPreset_Animal_Ghost"; // 幽灵
            public const string Wolf = "EnemyPreset_Animal_Wolf"; // 狼
            public const string WolfFarm = "EnemyPreset_Animal_Wolf_Farm"; // 狼
            public const string WolfIce = "EnemyPreset_Animal_Wolf_Ice"; // 狼
            public const string Chick = "SpawnPreset_Animal_Jinitaimei"; // *Cname_Chick*
            public const string Mushroom = "EnemyPreset_Mushroom"; // 行走菇
            
            public static readonly string[] All =
            { 
                Bear, BearGun, Ghost, Wolf, WolfFarm, WolfIce, Chick, Mushroom
            }; 
        }

        public static class Enemies
        {
            public const string Scav = "EnemyPreset_Scav"; // 拾荒者
            public const string ScavElite = "EnemyPreset_Scav_Elete"; // 拾荒者
            public const string ScavFarm = "EnemyPreset_Scav_Farm"; // 拾荒者
            public const string ScavIce = "EnemyPreset_Scav_Ice"; // 拾荒者
            public const string ScavLow = "EnemyPreset_Scav_low"; // 拾荒者
            public const string ScavLowAK = "EnemyPreset_Scav_low_ak74"; // 拾荒者
            public const string ScavMelee = "EnemyPreset_Scav_Melee"; // 暴走拾荒者
            public const string ScavSnow = "EnemyPreset_Scav_Snow"; // 拾荒者
            public const string ColdStorageScav = "EnemyPreset_ColdStorageScav"; // 拾荒者
            
            public const string USECFarm = "EnemyPreset_USEC_Farm"; // 雇佣兵
            public const string USECHidden = "EnemyPreset_USEC_HiddenWareHouse"; // 雇佣兵
            public const string USECIce = "EnemyPreset_USEC_Ice"; // 雇佣兵
            public const string USECIceMilitary = "EnemyPreset_USEC_Ice_Military"; // 雇佣兵
            public const string USECLow = "EnemyPreset_USEC_Low"; // 雇佣兵
            public const string USECSnowMilitary = "EnemyPreset_USEC_SnowMilitary"; // 雇佣兵
            
            public const string Raider = "EnemyPreset_JLab_Raider"; // 游荡者
            public const string SnowRaider = "EnemyPreset_Snow_Raider"; // 观测者
            public const string StormRaider = "EnemyPreset_Storm_Raider"; // 游荡者

            public const string Spider = "EnemyPreset_Spider_Rifle"; // 机械蜘蛛
            public const string SpiderStrong = "EnemyPreset_Spider_Rifle_Strong"; // 机械蜘蛛
            public const string SpiderJLab = "EnemyPreset_Spider_Rifle_JLab"; // 机械蜘蛛
            public const string SpiderRotate = "EnemyPreset_Spider_RotateShoot"; // 机械蜘蛛
            public const string SpiderRing = "EnemyPreset_Spider_Ring"; // 机械蜘蛛
            public const string SpiderSnow = "EnemyPreset_Spider_Snow"; // 机械蜘蛛
            public const string SpiderSuper = "EnemyPreset_Spider_Super"; // 超级蜘蛛
            public const string SpiderScare = "EnemyPreset_Spider_Scare"; // 失控机械蜘蛛

            public const string Zombie = "EnemyPreset_Zombie"; // ???
            public const string ZombieSnow = "EnemyPreset_Zombie_Snow"; // ???
            
            public const string Football1 = "EnemyPreset_Football_1"; // 足球
            public const string Football2 = "EnemyPreset_Football_2"; // 足球
            
            public const string PrisonMelee = "EnemyPreset_Prison_Melee"; // 监狱近战
            public const string PrisonPistol = "EnemyPreset_Prison_Pistol"; // 监狱手枪
            
            public const string JLabInvisible = "EnemyPreset_JLab_Melee_Invisable"; // 测试对象
            
            public const string StormCreature = "EnemyPreset_StormCreature" ; // 风暴生物
            public const string StormCreatureVirus = "EnemyPreset_StormCreature_Virus" ; // 风暴？
            public const string StormMonsterClimb = "EnemyPreset_Storm_MonsterClimb" ; // 风暴虫
            
            public static readonly string[] All = 
            { 
                Scav, ScavElite, ScavFarm, ScavIce, ScavLow, ScavLowAK, ScavMelee, ScavSnow, ColdStorageScav,
                USECFarm, USECHidden, USECIce, USECIceMilitary, USECLow, USECSnowMilitary,
                Raider, SnowRaider, StormRaider,
                Spider, SpiderStrong, SpiderJLab, SpiderRotate, SpiderRing, SpiderSnow, SpiderSuper, SpiderScare,
                Zombie, ZombieSnow,
                Football1, Football2,
                PrisonMelee, PrisonPistol,
                JLabInvisible,
                StormCreature, StormCreatureVirus, StormMonsterClimb
            };
        }
        
        public static class Merchant
        {
            public const string Myst = "EnemyPreset_Merchant_Myst"; // 神秘商人
            public const string Myst0 = "EnemyPreset_Merchant_Myst0"; // 神秘商人
            public const string Jeff = "EnemyPreset_Merchant_Jeff"; // 杰夫，可以受击
            
            public static readonly string[] All = { Myst, Myst0, Jeff };
        }

        public static class Quest
        {
            public const string QuestFo = "EnemyPreset_QuestGiver_Fo"; // 佛哥
            public const string QuestXiaoMing = "EnemyPreset_QuestGiver_XiaoMing"; // 小明
            public const string QuestAlex = "EnemyPreset_QuestGiver_Alex"; // 艾力克斯
            
            public static readonly string[] All = { QuestFo, QuestXiaoMing, QuestAlex };
        }
        
        public static class Vehicle
        {
            public const string VehicleTest = "EnemyPreset_VehicleTest"; // 马
            public const string VehicleTest2 = "EnemyPreset_VehicleTest 2"; // 马
            
            public static readonly string[] All = { VehicleTest, VehicleTest2 };
        }

        public static class Special
        {
            public const string BoomCar = "EnemyPreset_BoomCar"; // 炸弹小车
            public const string SnowPMC = "EnemyPreset_SnowPMC"; // 煤球
            public const string Drone = "EnemyPreset_Drone_Rifle"; // 侦察机
            public const string GunTurret = "EnemyPreset_GunTurret"; // 自动炮台
            public const string Pet = "PetPreset_NormalPet"; // 宠物
            
            public static readonly string[] All = { BoomCar, SnowPMC, Drone, GunTurret, Pet };
        }

        public static class Unknown
        {
            public const string MatePMC = "MatePreset_PMC"; // 雇佣兵，未知
            public const string Hunter = "EnemyPreset_Boss_Hunter"; // 猎户，使用弩箭
            public const string PMCLeader = "EnemyPreset_Boss_PMCLeader"; // 呆头鹅 没见过
            public const string Blue = "EnemyPreset_Boss_Blue"; // ??? 蓝色，可能是废案
            public const string LittleBoss = "EnemyPreset_LittleBoss"; // 小Boss，似乎是矮鸭废案
            
            public static readonly string[] All = { MatePMC, Hunter, PMCLeader, Blue, LittleBoss };
        }

        public static class Test
        {
            public const string DummyLv0 = "DummyEnemyCharacterRandomPresetLv 0"; //靶场靶子
            public const string DummyLv1 = "DummyEnemyCharacterRandomPresetLv 1"; //靶场靶子
            public const string DummyLv2 = "DummyEnemyCharacterRandomPresetLv 2"; //靶场靶子
            public const string DummyLv3 = "DummyEnemyCharacterRandomPresetLv 3"; //靶场靶子
            public const string DummyLv4 = "DummyEnemyCharacterRandomPresetLv 4"; //靶场靶子
            public const string DummyLv5 = "DummyEnemyCharacterRandomPresetLv 5"; //靶场靶子
            
            public const string ScavTest = "EnemyPreset_Scav_Test"; // 拾荒者
            
            public const string MerchantTest = "EnemyPreset_Merchant_Test"; // *MerchantName_Test*
            
            public const string VidaTest = "EnemyPreset_Boss_Vida_Test"; // 维达
            
            public const string Basement = "EnemyPreset_Basement";
            
            public static readonly string[] All = { DummyLv0, DummyLv1, DummyLv2, DummyLv3, DummyLv4, DummyLv5, ScavTest, MerchantTest, VidaTest, Basement };
        }
    }
}