using System.Text;
using UnityEngine;

namespace IslandTycoon
{
    /// <summary>
    /// Headless checks of the key states and transitions (NFR-14) plus a balance report.
    /// Run from the menu Island Tycoon > Run Self Test (result goes to the Console and Logs/).
    /// </summary>
    public static class SelfTest
    {
        public static string Run()
        {
            var sb = new StringBuilder();
            int fails = 0;
            System.Action<bool, string> Check = (ok, what) => { if (!ok) fails++; sb.AppendLine((ok ? "PASS " : "FAIL ") + what); };

            var cfg = GameConfig.Load();

            // --- state machine
            var m = new GameModel(cfg);
            Check(m.Phase == GamePhase.AwaitingWave, "new game starts in AWAITING_WAVE");
            Check(m.S.coins == cfg.startCoins && m.S.energy == cfg.startEnergy, "start currencies");
            Check(!m.B(NeedType.Relax).unlocked && !m.B(NeedType.OceanView).unlocked, "Pool and Bungalows locked at start");
            var fc = m.Forecast();
            Check(m.StartWave(), "StartWave from AWAITING_WAVE");
            Check(!m.StartWave(), "StartWave refused during WAVE_ACTIVE");
            int req = 0; foreach (var st in m.I.stats) req += st.requested;
            int fsum = 0; foreach (var c in fc) fsum += c;
            Check(req == fsum, "forecast matches generated wave");

            // --- worker moves are instant and free during a wave
            int coins = m.S.coins;
            int before = m.B(NeedType.Drinks).workers;
            bool moved = m.MoveWorker((int)NeedType.Rooms, -1) && m.MoveWorker((int)NeedType.Drinks, +1);
            Check(moved && m.B(NeedType.Drinks).workers == before + 1 && m.S.coins == coins, "worker move instant & free");
            float s0 = m.Speed((int)NeedType.Rooms);
            Check(s0 < m.Speed((int)NeedType.Drinks), "worker count affects speed");

            // --- run the wave to the end
            int guard = 0;
            while (m.Phase == GamePhase.WaveActive && guard++ < 100000) m.Tick(0.1f);
            Check(m.Phase == GamePhase.WaveResult, "wave ends -> WAVE_RESULT");
            Check(m.I.guests.Count == 0, "no guests left after wave");
            int coinsBefore = m.S.coins;
            Check(m.CollectRewards() && m.S.coins > coinsBefore, "rewards collected");
            Check(m.Phase == GamePhase.AwaitingWave && m.WaveNumber == 2, "next wave prepared");

            // --- save round trip in the middle of a wave
            m.StartWave();
            for (int i = 0; i < 60; i++) m.Tick(0.1f);
            string json = JsonUtility.ToJson(m.S);
            var m2 = new GameModel(cfg, JsonUtility.FromJson<GameState>(json));
            Check(m2.Phase == GamePhase.WaveActive && m2.I.guests.Count == m.I.guests.Count
                  && Mathf.Approximately(m2.I.waveTime, m.I.waveTime), "save/restore mid-wave");

            // --- upgrades and unlocks
            var m3 = new GameModel(cfg);
            m3.S.coins = 10000;
            float sp = m3.Speed((int)NeedType.Food);
            Check(m3.Upgrade((int)NeedType.Food) && m3.Speed((int)NeedType.Food) > sp && m3.Capacity((int)NeedType.Food) > 1, "upgrade has functional effect");
            Check(!m3.Unlock((int)NeedType.Relax), "Pool cannot be bought before wave 3");
            m3.I.completedWaves = 3;
            Check(m3.IsUnlockAvailable((int)NeedType.Relax) && m3.Unlock((int)NeedType.Relax), "Pool unlocks after wave 3 (2 steps)");
            Check(!m3.IsUnlockAvailable((int)NeedType.OceanView), "Bungalows still locked before wave 6");

            // --- second island
            var m4 = new GameModel(cfg);
            m4.S.coins = 100000;
            Check(!m4.BuyIsland(1), "second island locked at start");
            m4.I.completedWaves = cfg.Island(1).unlockAfterWaves;
            Check(m4.BuyIsland(1) && m4.S.current == 1, "second island bought and travelled to");
            Check(m4.I.totalWorkers == cfg.Island(1).startWorkers && m4.WaveNumber == 1, "second island has its own progress");
            Check(m4.Travel(0) && m4.I.completedWaves == cfg.Island(1).unlockAfterWaves, "travel back keeps first island progress");
            m4.Travel(1);
            m4.StartWave();
            Check(!m4.Travel(0), "cannot leave an island during a wave");

            sb.AppendLine();
            sb.AppendLine("Balance report (auto-player, no Energy used):");
            sb.Append(Balance(cfg));
            sb.Insert(0, fails == 0 ? "ALL CHECKS PASSED\n" : $"{fails} CHECK(S) FAILED\n");
            return sb.ToString();
        }

        /// <summary>Plays 10 waves with a simple strategy; shows FR-20 (3 stars possible without Energy).</summary>
        static string Balance(GameConfig cfg)
        {
            var sb = new StringBuilder();
            var m = new GameModel(cfg);
            for (int w = 0; w < 26; w++)
            {
                if (m.S.current == 0 && m.CanBuyIsland(1)) { m.BuyIsland(1); sb.AppendLine($"  -> bought {cfg.Island(1).name}"); }
                foreach (var n in Needs.All) if (m.CanUnlock((int)n)) m.Unlock((int)n);
                var fc = m.Forecast();
                foreach (var b in m.I.buildings) b.workers = 0;
                for (int k = 0; k < m.I.totalWorkers; k++)
                {
                    int best = -1; float gain = -1;
                    for (int n = 0; n < Needs.Count; n++)
                    {
                        if (!m.IsNeedActive(n)) continue;
                        float before = fc[n] * cfg.Need(n).baseServiceTime / m.Speed(n);
                        m.B(n).workers++;
                        float after = fc[n] * cfg.Need(n).baseServiceTime / m.Speed(n);
                        m.B(n).workers--;
                        if (before - after > gain) { gain = before - after; best = n; }
                    }
                    if (best >= 0) m.B(best).workers++;
                }
                m.StartWave();
                int guard = 0;
                while (m.Phase == GamePhase.WaveActive && guard++ < 100000) m.Tick(0.1f);
                var r = m.I.result;
                sb.AppendLine($"  {cfg.Island(m.S.current).name} wave {r.wave}: satisfaction {r.satisfaction:0}%  stars {r.stars}  +{r.coins} coins (total {m.S.coins + r.coins})");
                m.CollectRewards();
                int s = m.SuggestUpgrade();
                if (s >= 0) m.Upgrade(s);
                int reserve = m.S.current == 0 && !m.S.islands[1].purchased && m.I.completedWaves >= 6 ? cfg.Island(1).unlockCost : 150;
                for (int n = 0; n < Needs.Count; n++) if (m.S.coins - m.UpgradeCost(n) > reserve && m.CanUpgrade(n)) m.Upgrade(n);
            }
            sb.AppendLine(m.S.won ? "  GOAL REACHED" : "  goal not reached");
            return sb.ToString();
        }
    }
}
