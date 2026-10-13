using System;
using UnityEngine;

namespace IslandTycoon
{
    [Serializable]
    public class NeedConfig
    {
        public NeedType need;
        public string displayName;
        public string buildingName;
        [Tooltip("Sprite key: Buildings/<key>_lv1..3")]
        public string spriteKey;
        [Tooltip("Seconds to serve one guest at level 1 with one worker")]
        public float baseServiceTime = 4f;
        public int coinsPerServe = 5;
        public int upgradeCostBase = 60;
        public float weight = 1f;
    }

    [Serializable]
    public class PlotConfig
    {
        public NeedType need;
        [Tooltip("Centre of the sand plot in background pixels")]
        public Vector2 pixel;
        [Tooltip("Plot width in background pixels")]
        public float width = 250;
        [Tooltip("Completed waves on this island before the area can be bought (0 = open from start)")]
        public int unlockAfterWave;
        public int unlockCost;
        public bool jungleLock;
    }

    [Serializable]
    public class IslandConfig
    {
        public string name = "Palm Island";
        public string background = "Backgrounds/island_main";
        public string fallbackBackground = "Backgrounds/island_main";
        public Color fallbackTint = Color.white;
        public Vector2 backgroundPixels = new Vector2(1672, 941);
        public Vector2 spawnPixel = new Vector2(790, 772);
        public Vector2 freeWorkersPixel = new Vector2(700, 655);
        [Tooltip("Waves completed on the first island before this one can be bought")]
        public int unlockAfterWaves;
        public int unlockCost;
        public int startWorkers = 3;
        public int workersPerUnlock = 1;
        public float coinMultiplier = 1f;
        public float guestMultiplier = 1f;
        public float spacingMultiplier = 1f;
        public float vipChance;
        public PlotConfig[] plots = new PlotConfig[0];
    }

    [Serializable]
    public class WaveConfig
    {
        public int guests = 6;
        [Tooltip("Seconds between guest arrivals")]
        public float spacing = 3f;
        public int minNeeds = 1;
        public int maxNeeds = 1;
        [Tooltip("Per-need weight multipliers: Rooms, Drinks, Food, Relax, OceanView, Spa")]
        public float[] weights = { 1, 1, 1, 1, 1, 1 };

        public WaveConfig Clone()
        {
            var c = (WaveConfig)MemberwiseClone();
            c.weights = (float[])weights.Clone();
            return c;
        }
    }

    /// <summary>
    /// All tunable numbers live here, separate from game logic (NFR-13).
    /// Edit the asset at Assets/IslandTycoon/Resources/IslandTycoon/GameConfig.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Island Tycoon/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Tooltip("Bump when defaults change: older assets are re-created by Island Tycoon > Setup")]
        public int configVersion = 3;

        [Header("Start")]
        public int startCoins = 60;
        public int startEnergy = 6;

        [Header("Guests")]
        public float walkTime = 2f;
        public float patience = 12f;
        [Tooltip("Served within this fraction of patience = perfect score")]
        public float fastServeFraction = 0.35f;
        [Tooltip("Score of a request served right before patience runs out")]
        public float lateScore = 0.4f;
        public float vipCoinMultiplier = 2f;
        public float vipPatienceMultiplier = 0.8f;

        [Header("Service")]
        public float noWorkerFactor = 0.35f;
        public float extraWorkerBonus = 0.5f;
        public float[] levelSpeed = { 1f, 1.35f, 1.75f };
        public int[] levelCapacity = { 1, 2, 3 };
        public float[] upgradeCostMult = { 1f, 2.5f };

        [Header("Energy boost (FR-20)")]
        public int boostEnergyCost = 2;
        public float boostDuration = 12f;
        public float boostMultiplier = 2f;

        [Header("Bottleneck (FR-05)")]
        public int bottleneckQueue = 3;
        public float bottleneckWaitFraction = 0.5f;

        [Header("Rating")]
        public int star1 = 40;
        public int star2 = 70;
        public int star3 = 90;

        [Header("Goal")]
        [Tooltip("Island index whose wave count wins the game")]
        public int winIsland = 1;
        public int winWaves = 10;

        [Header("Needs / buildings")]
        public NeedConfig[] needs =
        {
            new NeedConfig { need = NeedType.Rooms,     displayName = "Rooms",      buildingName = "Guest Hotel",  spriteKey = "rooms",     baseServiceTime = 8.0f, coinsPerServe = 6,  upgradeCostBase = 70 },
            new NeedConfig { need = NeedType.Drinks,    displayName = "Drinks",     buildingName = "Tiki Bar",     spriteKey = "bar",       baseServiceTime = 4.8f, coinsPerServe = 4,  upgradeCostBase = 50 },
            new NeedConfig { need = NeedType.Food,      displayName = "Food",       buildingName = "Restaurant",   spriteKey = "food",      baseServiceTime = 7.2f, coinsPerServe = 5,  upgradeCostBase = 60 },
            new NeedConfig { need = NeedType.Relax,     displayName = "Relax",      buildingName = "Pool",         spriteKey = "pool",      baseServiceTime = 8.0f, coinsPerServe = 7,  upgradeCostBase = 90 },
            new NeedConfig { need = NeedType.OceanView, displayName = "Ocean View", buildingName = "Bungalows",    spriteKey = "bungalows", baseServiceTime = 9.6f, coinsPerServe = 10, upgradeCostBase = 130 },
            new NeedConfig { need = NeedType.Spa,       displayName = "Spa",        buildingName = "Spa Pavilion", spriteKey = "spa",       baseServiceTime = 8.5f, coinsPerServe = 9,  upgradeCostBase = 120 },
        };

        [Header("Islands")]
        public IslandConfig[] islands =
        {
            new IslandConfig
            {
                name = "Palm Island",
                background = "Backgrounds/island_main",
                startWorkers = 3,
                plots = new[]
                {
                    new PlotConfig { need = NeedType.Rooms,     pixel = new Vector2(460, 355),  width = 250 },
                    new PlotConfig { need = NeedType.Drinks,    pixel = new Vector2(435, 570),  width = 270 },
                    new PlotConfig { need = NeedType.Food,      pixel = new Vector2(1160, 615), width = 270 },
                    new PlotConfig { need = NeedType.Relax,     pixel = new Vector2(1230, 395), width = 255, unlockAfterWave = 3, unlockCost = 200 },
                    new PlotConfig { need = NeedType.OceanView, pixel = new Vector2(835, 235),  width = 250, unlockAfterWave = 6, unlockCost = 450, jungleLock = true },
                },
            },
            new IslandConfig
            {
                name = "Coral Lagoon",
                background = "Backgrounds/island_2",
                fallbackTint = new Color(1f, 0.86f, 0.72f),
                spawnPixel = new Vector2(855, 835),
                freeWorkersPixel = new Vector2(815, 690),
                unlockAfterWaves = 8,
                unlockCost = 900,
                startWorkers = 4,
                coinMultiplier = 1.6f,
                guestMultiplier = 1.25f,
                spacingMultiplier = 0.95f,
                vipChance = 0.2f,
                plots = new[]
                {
                    new PlotConfig { need = NeedType.Rooms,     pixel = new Vector2(655, 630),  width = 225 },
                    new PlotConfig { need = NeedType.Drinks,    pixel = new Vector2(1078, 632), width = 225 },
                    new PlotConfig { need = NeedType.Food,      pixel = new Vector2(448, 475),  width = 220 },
                    new PlotConfig { need = NeedType.Spa,       pixel = new Vector2(1285, 492), width = 230, unlockAfterWave = 2, unlockCost = 500 },
                    new PlotConfig { need = NeedType.Relax,     pixel = new Vector2(553, 245),  width = 215, unlockAfterWave = 4, unlockCost = 700, jungleLock = true },
                    new PlotConfig { need = NeedType.OceanView, pixel = new Vector2(1175, 268), width = 225, unlockAfterWave = 6, unlockCost = 900 },
                },
            },
        };

        [Header("Waves (FR-18, at least 8)")]
        public WaveConfig[] waves =
        {
            new WaveConfig { guests = 5,  spacing = 3.0f,  minNeeds = 1, maxNeeds = 1, weights = new float[] { 1, 1, 1, 1, 1, 1 } },
            new WaveConfig { guests = 7,  spacing = 2.7f,  minNeeds = 1, maxNeeds = 1, weights = new float[] { 1, 1.5f, 1, 1, 1, 1 } },
            new WaveConfig { guests = 8,  spacing = 2.55f, minNeeds = 1, maxNeeds = 2, weights = new float[] { 1, 1, 1.5f, 1, 1, 1 } },
            new WaveConfig { guests = 9,  spacing = 2.4f,  minNeeds = 1, maxNeeds = 2, weights = new float[] { 1, 1, 1, 1.5f, 1, 1 } },
            new WaveConfig { guests = 10, spacing = 2.2f,  minNeeds = 1, maxNeeds = 2, weights = new float[] { 1.5f, 1, 1, 1, 1, 1.5f } },
            new WaveConfig { guests = 12, spacing = 2.05f, minNeeds = 1, maxNeeds = 2, weights = new float[] { 1, 1.5f, 1.5f, 1, 1, 1 } },
            new WaveConfig { guests = 13, spacing = 1.85f, minNeeds = 1, maxNeeds = 2, weights = new float[] { 1, 1, 1, 1, 1.5f, 1 } },
            new WaveConfig { guests = 15, spacing = 1.7f,  minNeeds = 2, maxNeeds = 2, weights = new float[] { 1, 1, 1, 1, 1, 1 } },
        };

        [Header("Endless waves after the list")]
        public int endlessExtraGuests = 2;
        public float endlessSpacingMult = 0.95f;
        public float minSpacing = 1.4f;

        public const string ResourcePath = "IslandTycoon/GameConfig";

        public static GameConfig Load()
        {
            var cfg = Resources.Load<GameConfig>(ResourcePath);
            return cfg != null ? cfg : CreateInstance<GameConfig>();
        }

        public NeedConfig Need(NeedType t) => Need((int)t);

        public NeedConfig Need(int t)
        {
            foreach (var n in needs) if ((int)n.need == t) return n;
            return new NeedConfig { need = (NeedType)t, displayName = ((NeedType)t).ToString(), buildingName = ((NeedType)t).ToString(), spriteKey = ((NeedType)t).ToString().ToLower() };
        }

        public IslandConfig Island(int i) => islands[Mathf.Clamp(i, 0, islands.Length - 1)];

        public PlotConfig Plot(int island, int need)
        {
            foreach (var p in Island(island).plots) if ((int)p.need == need) return p;
            return null;
        }

        /// <summary>0-based wave index. Past the authored list waves keep scaling.</summary>
        public WaveConfig Wave(int index)
        {
            if (index < waves.Length) return waves[index];
            var last = waves[waves.Length - 1].Clone();
            int extra = index - waves.Length + 1;
            last.guests += endlessExtraGuests * extra;
            last.spacing = Mathf.Max(minSpacing, last.spacing * Mathf.Pow(endlessSpacingMult, extra));
            for (int i = 0; i < last.weights.Length; i++) last.weights[i] = 1f + 0.5f * ((index + i) % 3 == 0 ? 1 : 0);
            return last;
        }

        public int Stars(float satisfaction)
        {
            if (satisfaction >= star3) return 3;
            if (satisfaction >= star2) return 2;
            if (satisfaction >= star1) return 1;
            return 0;
        }
    }
}
