using System;
using System.Collections.Generic;
using UnityEngine;

namespace IslandTycoon
{
    /// <summary>
    /// Pure game logic. No rendering, no MonoBehaviour: the view only reads state
    /// and calls the public commands, so every rule is testable (NFR-12, NFR-14).
    /// Commands act on the island the player is currently on.
    /// </summary>
    public class GameModel
    {
        public readonly GameConfig Cfg;
        public GameState S { get; private set; }

        public event Action Changed;
        public event Action<string> Toast;
        public event Action<Guest, int> GuestServed;   // guest, need
        public event Action<Guest, int> GuestFailed;
        public event Action WaveStarted;
        public event Action WaveFinished;
        public event Action<int> BuildingUpgraded;
        public event Action<int> BuildingUnlocked;
        public event Action<int> WorkersMoved;
        public event Action<int> IslandChanged;
        public event Action<int> IslandPurchased;
        public event Action Victory;

        public GameModel(GameConfig cfg, GameState state = null)
        {
            Cfg = cfg;
            S = state ?? NewState();
            Repair();
        }

        GameState NewState()
        {
            var s = new GameState { coins = Cfg.startCoins, energy = Cfg.startEnergy };
            for (int i = 0; i < Cfg.islands.Length; i++) s.islands.Add(NewIsland(i, i == 0));
            return s;
        }

        IslandState NewIsland(int id, bool purchased)
        {
            var ic = Cfg.Island(id);
            var isl = new IslandState { id = id, purchased = purchased, totalWorkers = ic.startWorkers };
            foreach (var n in Needs.All)
            {
                var plot = Cfg.Plot(id, (int)n);
                isl.buildings.Add(new BuildingState
                {
                    need = (int)n,
                    exists = plot != null,
                    unlocked = plot != null && plot.unlockAfterWave == 0 && plot.unlockCost == 0,
                });
            }
            int w = isl.totalWorkers;
            while (w > 0)
            {
                bool any = false;
                foreach (var b in isl.buildings) if (b.unlocked && w > 0) { b.workers++; w--; any = true; }
                if (!any) break;
            }
            isl.stats = NewStats();
            return isl;
        }

        /// <summary>Makes an older or partial save consistent with the current config.</summary>
        void Repair()
        {
            if (S.islands == null) S.islands = new List<IslandState>();
            if (S.settings == null) S.settings = new SettingsState();
            for (int i = S.islands.Count; i < Cfg.islands.Length; i++) S.islands.Add(NewIsland(i, i == 0));
            S.islands[0].purchased = true;
            S.current = Mathf.Clamp(S.current, 0, S.islands.Count - 1);
            if (!S.islands[S.current].purchased) S.current = 0;

            foreach (var isl in S.islands)
            {
                if (isl.buildings == null) isl.buildings = new List<BuildingState>();
                foreach (var n in Needs.All)
                    if (isl.buildings.Find(b => b.need == (int)n) == null)
                        isl.buildings.Add(new BuildingState { need = (int)n });
                isl.buildings.Sort((a, b) => a.need.CompareTo(b.need));
                foreach (var b in isl.buildings)
                {
                    var plot = Cfg.Plot(isl.id, b.need);
                    b.exists = plot != null;
                    if (!b.exists) { b.unlocked = false; b.workers = 0; }
                    else if (plot.unlockAfterWave == 0 && plot.unlockCost == 0) b.unlocked = true;
                    b.level = Mathf.Clamp(b.level, 1, MaxLevel);
                    if (!b.unlocked) b.workers = 0;
                }
                if (isl.plans == null) isl.plans = new List<GuestPlan>();
                if (isl.guests == null) isl.guests = new List<Guest>();
                if (isl.stats == null || isl.stats.Count != Needs.Count) isl.stats = NewStats();
                if (isl.result == null) isl.result = new WaveResult();
                int assigned = 0;
                foreach (var b in isl.buildings) assigned += b.workers;
                while (assigned > isl.totalWorkers)
                    foreach (var b in isl.buildings) if (b.workers > 0 && assigned > isl.totalWorkers) { b.workers--; assigned--; }
            }
        }

        static List<NeedStats> NewStats()
        {
            var l = new List<NeedStats>();
            for (int i = 0; i < Needs.Count; i++) l.Add(new NeedStats());
            return l;
        }

        // ------------------------------------------------------------------ queries

        public IslandState I => S.islands[S.current];
        public IslandConfig IC => Cfg.Island(S.current);
        public GamePhase Phase => I.phase;
        public int MaxLevel => Mathf.Min(Cfg.levelSpeed.Length, Cfg.levelCapacity.Length);
        public BuildingState B(int need) => I.buildings[need];
        public BuildingState B(NeedType need) => I.buildings[(int)need];
        public int WaveNumber => I.waveIndex + 1;
        public int AssignedWorkers { get { int n = 0; foreach (var b in I.buildings) n += b.workers; return n; } }
        public int FreeWorkers => I.totalWorkers - AssignedWorkers;
        public bool Exists(int need) => B(need).exists;

        public float Speed(int need)
        {
            var b = B(need);
            float workers = b.workers == 0 ? Cfg.noWorkerFactor : 1f + Cfg.extraWorkerBonus * (b.workers - 1);
            float boost = b.boostLeft > 0 ? Cfg.boostMultiplier : 1f;
            return Cfg.levelSpeed[b.level - 1] * workers * boost;
        }

        public int Capacity(int need) => Cfg.levelCapacity[B(need).level - 1];

        public float Patience(Guest g) => Cfg.patience * (g.vip ? Cfg.vipPatienceMultiplier : 1f);

        public int QueueCount(int need)
        {
            int n = 0;
            foreach (var g in I.guests) if (g.stage == GuestStage.Queued && g.CurrentNeed == need) n++;
            return n;
        }

        public int InServiceCount(int need)
        {
            int n = 0;
            foreach (var g in I.guests) if (g.stage == GuestStage.InService && g.CurrentNeed == need) n++;
            return n;
        }

        public float LongestWaitFraction(int need)
        {
            float w = 0;
            foreach (var g in I.guests)
                if (g.stage == GuestStage.Queued && g.CurrentNeed == need) w = Mathf.Max(w, g.waited / Patience(g));
            return w;
        }

        /// <summary>FR-05: queue too long or a guest waited too long.</summary>
        public bool IsBottleneck(int need) =>
            I.phase == GamePhase.WaveActive &&
            (QueueCount(need) >= Cfg.bottleneckQueue || LongestWaitFraction(need) > Cfg.bottleneckWaitFraction);

        /// <summary>FR-09: live wave satisfaction 0..100.</summary>
        public float Satisfaction()
        {
            float score = 0; int resolved = 0;
            foreach (var st in I.stats) { score += st.score; resolved += st.served + st.failed; }
            return resolved == 0 ? 100f : 100f * score / resolved;
        }

        public bool IsNeedActive(int need) => B(need).exists && B(need).unlocked;

        // ------------------------------------------------------------------ wave generation

        /// <summary>Deterministic per island + wave, so the forecast always matches the real wave (FR-03).</summary>
        public List<GuestPlan> GeneratePlans(int waveIndex)
        {
            var wc = Cfg.Wave(waveIndex);
            var ic = IC;
            var rnd = new System.Random(1000 + waveIndex * 7919 + S.current * 104729);
            var plans = new List<GuestPlan>();
            var active = new List<int>();
            foreach (var n in Needs.All) if (IsNeedActive((int)n)) active.Add((int)n);
            if (active.Count == 0) return plans;

            int guests = Mathf.Max(1, Mathf.RoundToInt(wc.guests * ic.guestMultiplier));
            float spacing = wc.spacing * ic.spacingMultiplier;
            for (int i = 0; i < guests; i++)
            {
                bool vip = rnd.NextDouble() < ic.vipChance;
                var p = new GuestPlan { time = i * spacing, vip = vip, skin = vip ? rnd.Next(0, 4) : rnd.Next(0, 6) };
                int count = Mathf.Clamp(rnd.Next(wc.minNeeds, wc.maxNeeds + 1), 1, active.Count);
                var pool = new List<int>(active);
                for (int k = 0; k < count; k++)
                {
                    float total = 0;
                    foreach (var n in pool) total += Weight(wc, n);
                    float r = (float)rnd.NextDouble() * total;
                    int pick = pool[pool.Count - 1];
                    foreach (var n in pool) { r -= Weight(wc, n); if (r <= 0) { pick = n; break; } }
                    p.needs.Add(pick);
                    pool.Remove(pick);
                }
                plans.Add(p);
            }
            return plans;
        }

        float Weight(WaveConfig wc, int need)
        {
            float w = need < wc.weights.Length ? wc.weights[need] : 1f;
            return Mathf.Max(0.01f, w * Cfg.Need(need).weight);
        }

        /// <summary>FR-03: number of requests per need in the next wave.</summary>
        public int[] Forecast()
        {
            var counts = new int[Needs.Count];
            foreach (var p in GeneratePlans(I.waveIndex)) foreach (var n in p.needs) counts[n]++;
            return counts;
        }

        public int ForecastGuests() => GeneratePlans(I.waveIndex).Count;

        // ------------------------------------------------------------------ commands

        /// <summary>FR-02</summary>
        public bool StartWave()
        {
            if (I.phase != GamePhase.AwaitingWave) return false;
            I.plans = GeneratePlans(I.waveIndex);
            I.guests.Clear();
            I.stats = NewStats();
            I.waveTime = 0;
            I.nextPlan = 0;
            foreach (var p in I.plans) foreach (var n in p.needs) I.stats[n].requested++;
            I.phase = GamePhase.WaveActive;
            WaveStarted?.Invoke();
            Changed?.Invoke();
            return true;
        }

        /// <summary>FR-06, FR-07: instant and free, allowed before and during a wave.</summary>
        public bool MoveWorker(int need, int delta)
        {
            var b = B(need);
            if (!b.unlocked) return false;
            if (delta > 0 && FreeWorkers <= 0) return false;
            if (delta < 0 && b.workers <= 0) return false;
            b.workers += delta > 0 ? 1 : -1;
            WorkersMoved?.Invoke(need);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes a worker from the building with the most workers if none is free (FR-07).</summary>
        public bool PullWorker(int need)
        {
            if (FreeWorkers > 0) return MoveWorker(need, +1);
            int donor = -1, most = 0;
            foreach (var b in I.buildings)
                if (b.need != need && b.workers > most) { most = b.workers; donor = b.need; }
            if (donor < 0) return false;
            B(donor).workers--;
            WorkersMoved?.Invoke(donor);
            return MoveWorker(need, +1);
        }

        public int UpgradeCost(int need)
        {
            var b = B(need);
            if (b.level >= MaxLevel) return -1;
            int idx = Mathf.Clamp(b.level - 1, 0, Cfg.upgradeCostMult.Length - 1);
            return Mathf.RoundToInt(Cfg.Need(need).upgradeCostBase * Cfg.upgradeCostMult[idx] * Mathf.Sqrt(IC.coinMultiplier));
        }

        public bool CanUpgrade(int need) => B(need).unlocked && UpgradeCost(need) > 0 && S.coins >= UpgradeCost(need);

        /// <summary>FR-13: costs coins, raises speed and capacity.</summary>
        public bool Upgrade(int need)
        {
            if (!CanUpgrade(need)) return false;
            S.coins -= UpgradeCost(need);
            B(need).level++;
            BuildingUpgraded?.Invoke(need);
            Toast?.Invoke($"{Cfg.Need(need).buildingName} upgraded to level {B(need).level}!");
            Changed?.Invoke();
            return true;
        }

        public PlotConfig Plot(int need) => Cfg.Plot(S.current, need);

        /// <summary>FR-15, FR-17 step 1: milestone reached.</summary>
        public bool IsUnlockAvailable(int need)
        {
            var p = Plot(need);
            return p != null && !B(need).unlocked && I.completedWaves >= p.unlockAfterWave;
        }

        public bool CanUnlock(int need) => IsUnlockAvailable(need) && S.coins >= Plot(need).unlockCost;

        /// <summary>FR-17 step 2: purchase.</summary>
        public bool Unlock(int need)
        {
            if (!CanUnlock(need)) return false;
            S.coins -= Plot(need).unlockCost;
            var b = B(need);
            b.unlocked = true;
            I.totalWorkers += IC.workersPerUnlock;
            b.workers += Mathf.Min(IC.workersPerUnlock, FreeWorkers);
            BuildingUnlocked?.Invoke(need);
            Toast?.Invoke($"{Cfg.Need(need).buildingName} opened! New need: {Cfg.Need(need).displayName}");
            Changed?.Invoke();
            return true;
        }

        public bool CanBoost(int need) =>
            I.phase == GamePhase.WaveActive && B(need).unlocked && B(need).boostLeft <= 0 && S.energy >= Cfg.boostEnergyCost;

        /// <summary>FR-20: optional, temporary speed-up of one building.</summary>
        public bool Boost(int need)
        {
            if (!CanBoost(need)) return false;
            S.energy -= Cfg.boostEnergyCost;
            B(need).boostLeft = Cfg.boostDuration;
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ islands

        public bool IslandAvailable(int island) =>
            island < S.islands.Count && !S.islands[island].purchased &&
            S.islands[0].completedWaves >= Cfg.Island(island).unlockAfterWaves;

        public bool CanBuyIsland(int island) => IslandAvailable(island) && S.coins >= Cfg.Island(island).unlockCost;

        public bool BuyIsland(int island)
        {
            if (!CanBuyIsland(island)) return false;
            S.coins -= Cfg.Island(island).unlockCost;
            S.islands[island].purchased = true;
            IslandPurchased?.Invoke(island);
            Toast?.Invoke($"{Cfg.Island(island).name} is yours!");
            Travel(island);
            return true;
        }

        /// <summary>Only between waves, so no island is ever left mid-wave.</summary>
        public bool CanTravel(int island) =>
            island != S.current && island < S.islands.Count && S.islands[island].purchased && I.phase == GamePhase.AwaitingWave;

        public bool Travel(int island)
        {
            if (island == S.current || island >= S.islands.Count || !S.islands[island].purchased) return false;
            if (I.phase != GamePhase.AwaitingWave) return false;
            S.current = island;
            IslandChanged?.Invoke(island);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ simulation

        public void Tick(float dt)
        {
            var isl = I;
            if (isl.phase != GamePhase.WaveActive || dt <= 0) return;
            dt = Mathf.Min(dt, 0.25f);
            isl.waveTime += dt;

            foreach (var b in isl.buildings) if (b.boostLeft > 0) b.boostLeft = Mathf.Max(0, b.boostLeft - dt);

            // arrivals
            while (isl.nextPlan < isl.plans.Count && isl.plans[isl.nextPlan].time <= isl.waveTime)
            {
                var p = isl.plans[isl.nextPlan++];
                isl.guests.Add(new Guest
                {
                    id = isl.nextGuestId++, skin = p.skin, vip = p.vip, needs = new List<int>(p.needs),
                    stage = GuestStage.Walking, timer = Cfg.walkTime,
                });
            }

            // guests
            for (int i = 0; i < isl.guests.Count; i++)
            {
                var g = isl.guests[i];
                switch (g.stage)
                {
                    case GuestStage.Walking:
                        g.timer -= dt;
                        if (g.timer <= 0) { g.stage = GuestStage.Queued; g.waited = 0; }
                        break;
                    case GuestStage.Queued:
                        g.waited += dt;
                        if (g.waited > Patience(g)) Fail(g);
                        break;
                    case GuestStage.InService:
                        g.progress += dt * Speed(g.CurrentNeed) / Mathf.Max(0.1f, Cfg.Need(g.CurrentNeed).baseServiceTime);
                        if (g.progress >= 1f) Serve(g);
                        break;
                    case GuestStage.Leaving:
                        g.timer -= dt;
                        if (g.timer <= 0) g.stage = GuestStage.Done;
                        break;
                }
            }
            isl.guests.RemoveAll(g => g.stage == GuestStage.Done);

            // queues -> service slots (FIFO)
            foreach (var b in isl.buildings)
            {
                if (!b.unlocked) continue;
                int free = Capacity(b.need) - InServiceCount(b.need);
                foreach (var g in isl.guests)
                {
                    if (free <= 0) break;
                    if (g.stage != GuestStage.Queued || g.CurrentNeed != b.need) continue;
                    g.stage = GuestStage.InService;
                    g.progress = 0;
                    free--;
                }
            }

            // FR-10: wave ends when nobody else arrives and every request is resolved
            if (isl.nextPlan >= isl.plans.Count && isl.guests.Count == 0) FinishWave();
            Changed?.Invoke();
        }

        void Serve(Guest g)
        {
            int need = g.CurrentNeed;
            float pat = Patience(g);
            float fast = pat * Cfg.fastServeFraction;
            float score = g.waited <= fast ? 1f
                : Mathf.Lerp(1f, Cfg.lateScore, Mathf.InverseLerp(fast, pat, g.waited));
            var st = I.stats[need];
            st.served++;
            st.score += score;
            st.coins += Mathf.RoundToInt(Cfg.Need(need).coinsPerServe * (1f + 0.25f * (B(need).level - 1))
                                         * IC.coinMultiplier * (g.vip ? Cfg.vipCoinMultiplier : 1f));
            GuestServed?.Invoke(g, need);
            g.index++;
            g.progress = 0;
            g.waited = 0;
            g.timer = Cfg.walkTime;
            g.stage = g.index < g.needs.Count ? GuestStage.Walking : GuestStage.Leaving;
        }

        void Fail(Guest g)
        {
            int need = g.CurrentNeed;
            I.stats[need].failed++;
            // the guest gives up on the remaining needs as well
            for (int k = g.index + 1; k < g.needs.Count; k++) I.stats[g.needs[k]].failed++;
            g.angry = true;
            GuestFailed?.Invoke(g, need);
            g.index = g.needs.Count;
            g.timer = Cfg.walkTime;
            g.stage = GuestStage.Leaving;
        }

        void FinishWave()
        {
            var isl = I;
            float sat = Satisfaction();
            int stars = Cfg.Stars(sat);
            int coins = 0, served = 0;
            foreach (var st in isl.stats) { served += st.served; coins += st.coins; }
            coins += Mathf.RoundToInt(sat * 0.2f * (1 + isl.waveIndex * 0.25f) * IC.coinMultiplier);

            var r = new WaveResult
            {
                wave = WaveNumber,
                satisfaction = sat,
                stars = stars,
                coins = coins,
                energy = stars,
                resortValue = Mathf.RoundToInt((served * 2 + stars * 10) * IC.coinMultiplier),
            };
            foreach (var st in isl.stats)
                r.stats.Add(new NeedStats { requested = st.requested, served = st.served, failed = st.failed, score = st.score, coins = st.coins });

            isl.result = r;
            isl.hasResult = true;
            isl.phase = GamePhase.WaveResult;
            isl.plans.Clear();
            foreach (var b in isl.buildings) b.boostLeft = 0;
            WaveFinished?.Invoke();
        }

        /// <summary>FR-12: rewards are applied only when the player takes them.</summary>
        public bool CollectRewards()
        {
            var isl = I;
            if (isl.phase != GamePhase.WaveResult) return false;
            var r = isl.result;
            S.coins += r.coins;
            S.energy += r.energy;
            S.resortValue += r.resortValue;
            S.totalStars += r.stars;
            isl.completedWaves++;
            isl.waveIndex++;
            isl.hasResult = false;
            isl.phase = GamePhase.AwaitingWave;

            foreach (var p in IC.plots)
                if (!B((int)p.need).unlocked && p.unlockAfterWave == isl.completedWaves)
                    Toast?.Invoke($"New area available: {Cfg.Need(p.need).buildingName}! Tap it to unlock.");
            for (int i = 1; i < Cfg.islands.Length; i++)
                if (S.current == 0 && !S.islands[i].purchased && Cfg.Island(i).unlockAfterWaves == isl.completedWaves)
                    Toast?.Invoke($"A new island can be bought: {Cfg.Island(i).name}! Open the map.");

            if (!S.won && S.current == Cfg.winIsland && isl.completedWaves >= Cfg.winWaves)
            {
                S.won = true;
                Victory?.Invoke();
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>FR-14: the weakest upgradable service of the last wave, or -1.</summary>
        public int SuggestUpgrade()
        {
            int best = -1; float worst = 0.95f;
            var res = I.result;
            for (int n = 0; n < Needs.Count; n++)
            {
                if (!B(n).unlocked || UpgradeCost(n) < 0) continue;
                var st = res.stats.Count > n ? res.stats[n] : null;
                if (st == null || st.requested == 0) continue;
                float q = st.AvgScore;
                if (q < worst) { worst = q; best = n; }
            }
            return best;
        }

        /// <summary>Goal progress for the HUD: waves done on the goal island.</summary>
        public int GoalProgress => S.islands.Count > Cfg.winIsland && S.islands[Cfg.winIsland].purchased
            ? Mathf.Min(Cfg.winWaves, S.islands[Cfg.winIsland].completedWaves) : 0;
    }
}
