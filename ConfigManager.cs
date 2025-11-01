using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Bank
{
    [Serializable]
    public class BankConfig
    {
        public bool EnableTestMode { get; set; } = false;
        public int BalanceRatePerLevel { get; set; } = 5000;
        public int DueDays { get; set; } = 2;

        public bool enableSpawnChance { get; set; } = true;
        public bool checkTime { get; set; } = true;
        public float spawnTimeOnDayStart { get; set; } = 6f;
        public float spawnTimeOnDayEnd { get; set; } = 21f;
        public bool checkWeather { get; set; } = true;
        public float spawnStartTime { get; set; } = 60f;
        public float spawnEndTime { get; set; } = 240f;
        public float spawnDistanceMin { get; set; } = 20f;
        public float spawnDistanceMax { get; set; } = 25f;
        public bool enableJLab { get; set; } = false;

        [Serializable]
        public class BossInfo
        {
            public string presetObjName { get; set; }
            public string displayName { get; set; }
            public int bossCount { get; set; } = 1;
            public bool isGroupBoss { get; set; }
            public int levelIndex { get; set; }
            public string childPresetObjName { get; set; }
            public int childCount { get; set; }
            public bool hasLeader { get; set; }
        }

        public List<BossInfo> BossInfoList { get; set; } = new List<BossInfo>();
    }

    public static class ConfigManager
    {
        private const string ModFolderName = "Mods/ImNotGonnaPay";
        private const string ConfigFileName = "config.yaml";
        private const string EmbeddedDefaultName = "DefaultConfig.yaml";

        private static readonly object _lock = new object();
        private static BankConfig _config;
        public static BankConfig Config
        {
            get
            {
                if (_config == null)
                {
                    LoadOrCreate();
                }
                return _config;
            }
        }

        public static string ConfigDirectory
        {
            get
            {
                var root = Application.dataPath.Replace("\\", "/");
                return Path.Combine(root, ModFolderName).Replace("\\", "/");
            }
        }

        public static string ConfigPath => Path.Combine(ConfigDirectory, ConfigFileName).Replace("\\", "/");

        public static void LoadOrCreate()
        {
            lock (_lock)
            {
                Directory.CreateDirectory(ConfigDirectory);

                if (!File.Exists(ConfigPath))
                {
                    CopyDefaultTo(ConfigPath);
                }

                try
                {
                    var yaml = File.ReadAllText(ConfigPath);
                    var deserializer = new DeserializerBuilder()
                        .IgnoreUnmatchedProperties()
                        .Build();

                    _config = deserializer.Deserialize<BankConfig>(yaml) ?? new BankConfig();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Bank][Config] 解析配置失败，使用内置默认。\n{ex}");
                    _config = new BankConfig();
                }
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(ConfigDirectory);
                    var serializer = new SerializerBuilder()
                        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                        .Build();
                    var yaml = serializer.Serialize(_config);
                    File.WriteAllText(ConfigPath, yaml);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Bank][Config] 保存配置失败：{ex}");
                }
            }
        }

        public static void ApplyToGame()
        {
            try
            {
                var cfg = Config;
                // 应用到 ModBehaviour
                ModBehaviour.EnableTestMode = cfg.EnableTestMode;
                ModBehaviour.BalanceRatePerLevel = cfg.BalanceRatePerLevel;
                ModBehaviour.DueDays = cfg.DueDays;

                // 应用到 EnemyCreator（基础配置）
                EnemyCreator.enableSpawnChance = cfg.enableSpawnChance;
                EnemyCreator.checkTime = cfg.checkTime;
                EnemyCreator.spawnTimeOnDayStart = cfg.spawnTimeOnDayStart;
                EnemyCreator.spawnTimeOnDayEnd = cfg.spawnTimeOnDayEnd;
                EnemyCreator.checkWeather = cfg.checkWeather;
                EnemyCreator.spawnStartTime = cfg.spawnStartTime;
                EnemyCreator.spawnEndTime = cfg.spawnEndTime;
                EnemyCreator.spawnDistanceMin = cfg.spawnDistanceMin;
                EnemyCreator.spawnDistanceMax = cfg.spawnDistanceMax;
                EnemyCreator.enableJLab = cfg.enableJLab;

                // 覆盖 Boss 列表
                if (cfg.BossInfoList != null && cfg.BossInfoList.Count > 0)
                {
                    var mapped = new List<EnemyCreator.BossInfo>(cfg.BossInfoList.Count);
                    foreach (var b in cfg.BossInfoList)
                    {
                        var boss = new EnemyCreator.BossInfo
                        {
                            presetObjName = b.presetObjName,
                            displayName = b.displayName,
                            bossCount = b.bossCount <= 0 ? 1 : b.bossCount,
                            isGroupBoss = b.isGroupBoss,
                            levelIndex = b.levelIndex,
                            childPresetObjName = b.childPresetObjName,
                            childCount = b.childCount,
                            hasLeader = b.hasLeader,
                        };
                        mapped.Add(boss);
                    }
                    EnemyCreator.BossInfoList = mapped;
                }

                Debug.Log("[Bank][Config] 配置已应用（含 Boss 列表）");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bank][Config] 应用配置失败：{ex}");
            }
        }

        private static void CopyDefaultTo(string path)
        {
            try
            {
                using var stream = GetEmbeddedDefaultStream();
                if (stream == null)
                {
                    Debug.LogWarning("[Bank][Config] 找不到内置默认配置，写入最小模板。");
                    File.WriteAllText(path, "EnableTestMode: false\nBalanceRatePerLevel: 5000\n");
                    return;
                }
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                File.WriteAllText(path, content);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bank][Config] 写入默认配置失败：{ex}");
                File.WriteAllText(path, "EnableTestMode: false\nBalanceRatePerLevel: 5000\n");
            }
        }

        private static Stream GetEmbeddedDefaultStream()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var names = asm.GetManifestResourceNames();
                var name = names.FirstOrDefault(n => n.EndsWith(EmbeddedDefaultName, StringComparison.OrdinalIgnoreCase));
                if (name == null)
                {
                    Debug.LogWarning($"[Bank][Config] 未找到内置资源：{EmbeddedDefaultName}. 可用: {string.Join(", ", names)}");
                    return null;
                }
                return asm.GetManifestResourceStream(name);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bank][Config] 读取内置资源失败：{ex}");
                return null;
            }
        }
    }
}
