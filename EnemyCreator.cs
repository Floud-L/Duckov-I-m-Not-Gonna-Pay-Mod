using Cysharp.Threading.Tasks;
using Duckov.Modding;
using Duckov.Utilities;
using Duckov.Weathers;
using Duckvo.Beacons;
using SodaCraft.Localizations;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bank {
    public static class EnemyCreator {
        private static float spawnChance => ModBehaviour.Debt / ModBehaviour.TotalBalance;
        private static List<GameObject> createdSpawnerList = new List<GameObject>();
        private static Dictionary<string, CharacterRandomPreset> originalpresetDic = new Dictionary<string, CharacterRandomPreset>();
        private static Dictionary<string, CharacterRandomPreset> modifypresetDic = new Dictionary<string, CharacterRandomPreset>();
        private static List<Vector3> spawnPoints = new List<Vector3>();
        private static Vector3 spawnPosition = Vector3.zero;
        private static int levelLcok = 1;

        private static UniTask spawnTask = UniTask.CompletedTask;
        private static CancellationTokenSource spawnCts;

        public static bool enableSpawnChance = true;
        public static bool enableJLab = false;
        public static bool allwaysHasLeader = true;
        public static bool checkTime = true;
        public static float spawnTimeOnDayStart = 6f;
        public static float spawnTimeOnDayEnd = 21f;
        public static bool checkWeather = true;
        public static List<Weather> targetWeathers = new List<Weather> { 
            Weather.Sunny,
            Weather.Cloudy,
            Weather.Rainy
        };

        public static float spawnStartTime = 60f;
        public static float spawnEndTime = 240f;
        public static float spawnDistanceMin = 20;
        public static float spawnDistanceMax = 25;
        public static List<string> spawnLevelName = new List<string> { "Level_GroundZero_Main", "Level_HiddenWarehouse_Main", "Level_Farm_Main" };

        public static bool EnableTestMode => ModBehaviour.EnableTestMode;
        private static int testSpawnBossIndex = 0;

        public static void Init() {
            if (originalpresetDic.Count == 0) { 
                GetBossPreset();
            }
        }

        public static void OnUpdate() {
            if (EnableTestMode) { 
                TestSpawnBankBoss();
            }
        }

        public static void OnLevelLoaded() {
            GetALLSpawnPoints();
            CheckLevelLcok();

            if (spawnCts != null) { 
                spawnCts.Cancel();
                spawnCts.Dispose();
                Debug.Log("[Bank] 取消上一个债主生成任务");
            }

            spawnCts = new CancellationTokenSource();

            spawnTask = SpawnBankBoss(spawnCts.Token);
            spawnTask.Forget();
        }

        public static async UniTask<bool> SpawnBankBoss(CancellationToken token) {
            if (!ModBehaviour.isOverDue) { 
                Debug.Log("[Bank] 未逾期,不生成债主");
                return false;
            }

            if (ModBehaviour.HasDebtNote) { 
                Debug.Log("[Bank] 玩家已拥有欠条,不生成债主");
                return false;
            }

            var levelInfo = LevelManager.GetCurrentLevelInfo();
            Debug.Log($"[Bank] 当前关卡: {levelInfo.sceneName}");

            var canLevelSpawn = false;
            if (enableJLab) {
                spawnLevelName.Add("Level_JLab_Main");
            }
            foreach (var levelName in spawnLevelName) {
                if (levelInfo.sceneName == levelName) {
                    canLevelSpawn = true;
                    break;
                }
            }
            if (!canLevelSpawn) {
                Debug.Log("[Bank] 当前关卡不允许生成债主");
                return false;
            }

            if(enableSpawnChance)
            if (Random.Range(0f, 1f) > spawnChance) {
                Debug.Log("[Bank] 未达到生成概率");
                return false;
            }

            var spawnTime = Random.Range(spawnStartTime,spawnEndTime);
            Debug.Log($"[Bank] 计划生成时间: {spawnTime}");

            while (!LevelTimeChecker(spawnTime)) {
                await UniTask.WaitForSeconds(5f, cancellationToken: token);
            }
            Debug.Log("[Bank] 达到生成时间");

            if (spawnPoints.Count == 0) {
                Debug.LogWarning("[Bank] 生成点列表为空!!!");
                return false;
            }
            Debug.Log("[Bank] 查找生成点");
            while (!SetSpawnPoint()) {
                await UniTask.WaitForSeconds(1f, cancellationToken: token);
            }

            if (token.IsCancellationRequested) { 
                return false;            
            }

            var bossInfo = RandomBossInfo();
            var spawnerObj = CreateSpawnerObject(bossInfo);
            spawnerObj.SetActive(true);

            return true;
        }

        private static bool LevelTimeChecker(float time) {
            if (LevelManager.Instance == null) { 
                Debug.Log($"[Bank] LevelManager不存在");
                return false;
            }
            var levelTime = LevelManager.Instance.LevelTime;
            Debug.Log($"[Bank] 关卡时间: {levelTime}");
            if (levelTime >= time) { 
                return true;
            }
            else {
                return false;
            }
        }

        private static bool SetSpawnPoint() {
            var playerPos = CharacterMainControl.Main.transform.position;
            List<Vector3> validSpawnPoints = new List<Vector3>();
            foreach (var point in spawnPoints) {
                var distance =  Vector3.Distance(playerPos,point);
                if (distance >= spawnDistanceMin && distance <= spawnDistanceMax) { 
                    validSpawnPoints.Add(point);
                }
            }
            if (validSpawnPoints.Count == 0) { 
                Debug.Log("[Bank] 未找到合适的生成点");
                return false;
            }
            var randomSpawnPointIndex = Random.Range(0, validSpawnPoints.Count - 1);
            spawnPosition = validSpawnPoints[randomSpawnPointIndex];
            Debug.Log($"[Bank] 选择生成点: {spawnPosition}");

            return true;
        }

        private static BossInfo RandomBossInfo() {
            var availableBoss = BossInfoList.FindAll(boss => boss.levelIndex <= levelLcok);
            var randomIndex = Random.Range(0, availableBoss.Count - 1);
            return availableBoss[randomIndex];
        }

        public static void TestSpawnBankBoss() {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame) {
                if (testSpawnBossIndex < BossInfoList.Count - 1) {
                    testSpawnBossIndex += 1;
                    Debug.Log($"SpawnedBossIndex: {testSpawnBossIndex}");
                }
            }
            if (Keyboard.current.downArrowKey.wasPressedThisFrame) {
                if (testSpawnBossIndex > 0) {
                    testSpawnBossIndex -= 1;
                    Debug.Log($"SpawnedBossIndex: {testSpawnBossIndex}");
                }
            }
            if (Keyboard.current.f2Key.wasPressedThisFrame) {
                spawnPosition = CharacterMainControl.Main.transform.position;
                var spawnerObj = CreateSpawnerObject(BossInfoList[testSpawnBossIndex]);
                spawnerObj.SetActive(true);
                Debug.Log(LevelManager.GetCurrentLevelInfo().sceneName);
            }
        }

        private static GameObject CreateSpawnerObject(BossInfo bossInfo) {
            //生成器物体创建
            var spawnerObjName = "EnemySpawner_Bank_" + bossInfo.presetObjName;
            var spawnerObj = new GameObject(spawnerObjName);
            spawnerObj.SetActive(false);

            //生成器根组件创建
            var spawnerRoot = spawnerObj.AddComponent<CharacterSpawnerRoot>();
            spawnerRoot.needTrigger = false;
            spawnerRoot.spawnChance = 1f;
            spawnerRoot.minDistanceToPlayer = 0;
            spawnerRoot.useTimeOfDay = checkTime;
            spawnerRoot.spawnTimeRangeFrom = spawnTimeOnDayStart;
            spawnerRoot.spawnTimeRangeTo = spawnTimeOnDayEnd;
            spawnerRoot.checkWeather = checkWeather;
            spawnerRoot.targetWeathers = targetWeathers;

            //生成器GUID设置
            var GUID = spawnerObj.GetInstanceID().ToString()[^4..] + spawnerRoot.GetInstanceID().ToString()[^4..];
            Debug.Log($"Spawner_Bank GUID: {GUID}");
            spawnerRoot.SpawnerGuid = int.Parse(GUID);

            //生成器群组件创建
            var spawnerGroup = spawnerObj.AddComponent<CharacterSpawnerGroup>();
            PrivateAccessor.TrySet(spawnerRoot, "spawnerComponent", spawnerGroup);

            //Boss生成器物体创建
            var bossSpawnerObj = new GameObject("BossSpawner");
            bossSpawnerObj.transform.SetParent(spawnerObj.transform);

            //Boss生成器组件添加
            var bossPoints = bossSpawnerObj.AddComponent<Points>();
            var bossSpawner = bossSpawnerObj.AddComponent<RandomCharacterSpawner>();

            //生成器群设置
            spawnerGroup.spawners = new List<RandomCharacterSpawner>() {
                bossSpawner
            };
            spawnerGroup.hasLeader = allwaysHasLeader || bossInfo.hasLeader;

            //Boss生成器设置
            bossSpawner.isStaticTarget = false;
            bossSpawner.spawnCountRange = new Vector2Int(bossInfo.bossCount, bossInfo.bossCount);

            //如果字典中没有该Boss预设体的修改版则进行复制与修改
            var bossName = bossInfo.presetObjName + "_Bank";
            if (!modifypresetDic.ContainsKey(bossName)) {
                //Boss预设体复制与修改
                var bossPresetCopy = Object.Instantiate(originalpresetDic[bossInfo.presetObjName]);
                bossPresetCopy.name = bossName;
                bossPresetCopy.nameKey = bossName;
                LocalizationManager.SetOverrideText(bossName, "债主:" + bossInfo.displayName);

                //获取掉落物列表
                PrivateAccessor.TryGet(bossPresetCopy, "itemsToGenerate", out List<RandomItemGenerateDescription> itemsToGenerate);

                //创建欠条掉落物描述器
                var itemGenerateDescription = new RandomItemGenerateDescription {
                    chance = 1f,
                    randomCount = new Vector2Int(1, 1),
                    controlDurability = false,
                    randomFromPool = true,
                    itemPool = new RandomContainer<RandomItemGenerateDescription.Entry>(),
                };
                //创建欠条掉落物条目
                var entry = new RandomItemGenerateDescription.Entry {
                    itemTypeID = ModBehaviour.DebtNoteTypeID
                };
                //将欠条条目添加到掉落物描述器中
                itemGenerateDescription.itemPool.AddEntry(entry,1);

                //设置描述器备注并添加到掉落物列表中
                PrivateAccessor.TrySet(itemGenerateDescription, "comment","欠条");
                itemsToGenerate.Insert(0, itemGenerateDescription);

                //将克隆修改后的Boss预设体添加到字典中
                modifypresetDic.Add(bossName, bossPresetCopy);    
            }

            //Boss生成器添加Boss预设体
            bossSpawner.randomPresetInfos = new List<CharacterRandomPresetInfo> {
                new CharacterRandomPresetInfo {
                    randomPreset = modifypresetDic[bossName],
                    weight = 1f,
                }
            };

            //Boss生成点设置
            bossPoints.points = new List<Vector3>();
            for (int i = 0; i < bossInfo.bossCount; i++) {
                var bossSpawnPos = spawnPosition + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                bossPoints.points.Add(bossSpawnPos);
            }

            //子单位生成器创建
            if (bossInfo.isGroupBoss) {
                //子单位生成器物体创建
                var childSpawnerObj = new GameObject("ChildSpawner");
                childSpawnerObj.transform.SetParent(spawnerObj.transform);

                //子单位生成器组件添加
                var childPoints = childSpawnerObj.AddComponent<Points>();
                var childSpawner = childSpawnerObj.AddComponent<RandomCharacterSpawner>();

                //生成器群添加子单位生成器
                spawnerGroup.spawners.Add(childSpawner);

                //子单位生成器设置
                childSpawner.isStaticTarget = false;
                childSpawner.spawnCountRange = new Vector2Int(bossInfo.childCount, bossInfo.childCount);

                //如果字典中没有该子单位预设体的修改版则进行复制与修改
                var childName = bossInfo.childPresetObjName + "_Bank";
                if (!modifypresetDic.ContainsKey(childName)) {
                    //子单位预设体复制与修改
                    var childPresetCopy = Object.Instantiate(originalpresetDic[bossInfo.childPresetObjName]);
                    childPresetCopy.name = childName;
                    childPresetCopy.nameKey = childName;
                    //设置子单位显示名称
                    childPresetCopy.showName = true;
                    LocalizationManager.SetOverrideText(childName, "债主的打手");

                    //将克隆修改后的子单位预设体添加到字典中
                    modifypresetDic.Add(childName, childPresetCopy);
                }

                //子单位生成器添加子单位预设体
                childSpawner.randomPresetInfos = new List<CharacterRandomPresetInfo> {
                    new CharacterRandomPresetInfo {
                        randomPreset = modifypresetDic[childName],
                        weight = 1f,
                    }
                };

                //子单位生成点设置
                childPoints.points = new List<Vector3>();
                for (int i = 0; i < bossInfo.childCount;i++) { 
                    var childSpawnPos = spawnPosition + new Vector3(Random.Range(-2f,2f),0,Random.Range(-2f,2f));
                    childPoints.points.Add(childSpawnPos);
                }
            }

            //将生成器物体添加到已创建列表中并返回
            createdSpawnerList.Add(spawnerObj);
            return spawnerObj;
        }

        private static void CheckLevelLcok() {
            if (BeaconManager.GetBeaconUnlocked(spawnLevelName[1],0)) { 
                levelLcok = 2;
            } 
            if (BeaconManager.GetBeaconUnlocked(spawnLevelName[2],0)) {
                levelLcok = 3;
            } 
            if (BeaconManager.GetBeaconUnlocked(spawnLevelName[2],1)) {
                levelLcok = 3;
            }
        }

        private static void GetALLSpawnPoints() {
            spawnPoints.Clear();
            var allSpawner = Resources.FindObjectsOfTypeAll<RandomCharacterSpawner>();
            foreach (var spawner in allSpawner) {
                if (!spawner.name.Contains("EnemySpawner")) { 
                    continue;
                }
                string[] negativeKeywords = new string[] { "Boss", "Myst", "Spider", "Red", "Storm","PMC" , "Mushroom", "UltraMan"};
                
                // 检查 spawner 及其所有父物体名称是否包含任一排除关键字
                var current = spawner.transform;
                var shouldSkip = false;
                while (current != null) {
                    var currentName = current.name;
                    for (int i = 0; i < negativeKeywords.Length; i++) {
                        var kw = negativeKeywords[i];
                        if (!string.IsNullOrEmpty(currentName) && currentName.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0) {
                            shouldSkip = true;
                            break;
                        }
                    }
                    if (shouldSkip) break;
                    current = current.parent;
                }
                if (shouldSkip) {
                    // 跳过该 spawner
                    continue;
                }

                var spawnerPoints = spawner.GetComponent<Points>();
                spawnPoints.AddRange(spawnerPoints.points);
            }

        }

        private static void GetBossPreset() { 
            var allPreset = Resources.FindObjectsOfTypeAll<CharacterRandomPreset>();
            foreach (var preset in allPreset) {
                var index1 = BossInfoList.FindIndex(info => info.presetObjName == preset.name);
                var index2 = BossInfoList.FindIndex(info => info.childPresetObjName == preset.name);
                if (index1 != -1||index2!=-1) {
                    originalpresetDic[preset.name] = preset;
                }
            }
        }

        public struct BossInfo {
            public string presetObjName;
            public string displayName;
            public int bossCount; // 新增字段：同种Boss同时生成的数量
            public bool isGroupBoss;
            public int levelIndex;
            public string childPresetObjName;
            public int childCount;
            public bool hasLeader;
        }

        public static List<BossInfo> BossInfoList = new List<BossInfo>() {
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_ShortEagle",
                displayName = "矮鸭",
                bossCount = 1,
                levelIndex = 1,
                isGroupBoss = true,
                childPresetObjName = "EnemyPreset_Boss_ShortEagle_Elete",
                childCount = 4,
                hasLeader = false,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_Deng",
                displayName = "劳登",
                bossCount = 1,
                levelIndex = 2,
                isGroupBoss = true,
                childPresetObjName ="EnemyPreset_Boss_Deng_Wolf",
                childCount = 1,
                hasLeader = true,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_Speedy",
                displayName = "急速团长",
                bossCount = 1,
                levelIndex = 2,
                isGroupBoss = true,
                childPresetObjName = "EnemyPreset_Boss_Speedy_Child",
                childCount = 4,
                hasLeader = true,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_Vida",
                displayName = "维达",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = false,
                hasLeader = true,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_Grenade",
                displayName = "炸弹狂人",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = false,
                hasLeader = true,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_ServerGuardian",
                displayName = "矿长",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = true,
                childPresetObjName = "EnemyPreset_Scav_Elete",
                childCount = 4,
                hasLeader = true,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_3Shot",
                displayName = "三枪哥",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = true,
                childPresetObjName = "EnemyPreset_Boss_3Shot_Child",
                childCount = 3,
                hasLeader = true,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_Fly",
                displayName = "蝇蝇队长",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = true,
                childPresetObjName = "EnemyPreset_Boss_Fly_Child",
                childCount = 2,
                hasLeader = false,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_BossMelee_SchoolBully",
                displayName = "校霸",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = true,
                childPresetObjName = "EnemyPreset_BossMelee_SchoolBully_Child",
                childCount = 3,
                hasLeader = false,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_BALeader",
                displayName = "BA队长",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = true,
                childPresetObjName = "EnemyPreset_Boss_BALeader_Child",
                childCount = 3,
                hasLeader = true,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_RPG",
                displayName = "迷塞尔",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = false,
                hasLeader = true,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_Shot",
                displayName = "喷子",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = true,
                childPresetObjName = "EnemyPreset_Scav",
                childCount = 1,
                hasLeader = true,
            },
            new BossInfo {
                presetObjName = "EnemyPreset_Boss_SenorEngineer",
                displayName = "高级工程师",
                bossCount = 1,
                levelIndex = 3,
                isGroupBoss = false,
                hasLeader = true,
            },
        };

        
    }
}
