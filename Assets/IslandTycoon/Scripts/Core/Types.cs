using System;
using System.Collections.Generic;

namespace IslandTycoon
{
    /// <summary>Guest need = service type. One building serves exactly one need.</summary>
    public enum NeedType { Rooms = 0, Drinks = 1, Food = 2, Relax = 3, OceanView = 4, Spa = 5 }

    /// <summary>Main resort state machine (FR-02, FR-10, FR-12).</summary>
    public enum GamePhase { AwaitingWave = 0, WaveActive = 1, WaveResult = 2 }

    public enum GuestStage { Walking = 0, Queued = 1, InService = 2, Leaving = 3, Done = 4 }

    public static class Needs
    {
        public const int Count = 6;
        public static readonly NeedType[] All =
            { NeedType.Rooms, NeedType.Drinks, NeedType.Food, NeedType.Relax, NeedType.OceanView, NeedType.Spa };
    }

    [Serializable]
    public class BuildingState
    {
        public int need;
        public bool exists;      // this island has a plot for the need
        public bool unlocked;
        public int level = 1;
        public int workers;
        public float boostLeft;
    }

    [Serializable]
    public class GuestPlan
    {
        public float time;
        public int skin;
        public bool vip;
        public List<int> needs = new List<int>();
    }

    [Serializable]
    public class Guest
    {
        public int id;
        public int skin;
        public bool vip;
        public List<int> needs = new List<int>();
        public int index;          // which need of the list is current
        public GuestStage stage;
        public float timer;        // walking / leaving countdown
        public float waited;       // time spent in queue for the current need
        public float progress;     // service progress 0..1
        public bool angry;
        public int CurrentNeed => index < needs.Count ? needs[index] : -1;
    }

    [Serializable]
    public class NeedStats
    {
        public int requested;
        public int served;
        public int failed;
        public float score;
        public int coins;
        public float AvgScore => served + failed == 0 ? 1f : score / (served + failed);
    }

    [Serializable]
    public class WaveResult
    {
        public int wave;            // 1-based
        public float satisfaction;  // 0..100
        public int stars;
        public int coins;
        public int energy;
        public int resortValue;
        public List<NeedStats> stats = new List<NeedStats>();
    }

    /// <summary>One resort. Every island keeps its own buildings, workers and wave progress.</summary>
    [Serializable]
    public class IslandState
    {
        public int id;
        public bool purchased;
        public GamePhase phase = GamePhase.AwaitingWave;
        public int totalWorkers;
        public int waveIndex;        // 0-based index of the next / current wave
        public int completedWaves;
        public List<BuildingState> buildings = new List<BuildingState>();

        // active wave
        public float waveTime;
        public int nextPlan;
        public List<GuestPlan> plans = new List<GuestPlan>();
        public List<Guest> guests = new List<Guest>();
        public int nextGuestId;
        public List<NeedStats> stats = new List<NeedStats>();

        // result waiting to be collected
        public bool hasResult;
        public WaveResult result = new WaveResult();
    }

    [Serializable]
    public class SettingsState
    {
        public bool music = true;
        public bool sound = true;
        public bool vibration = true;
    }

    /// <summary>Everything that must survive an app interruption (NFR-07, FR-19).</summary>
    [Serializable]
    public class GameState
    {
        public int version = 2;
        public int coins;
        public int energy;
        public int resortValue;
        public int current;
        public List<IslandState> islands = new List<IslandState>();
        public SettingsState settings = new SettingsState();
        public int tutorialStep;
        public bool won;
        public int totalStars;
    }
}
