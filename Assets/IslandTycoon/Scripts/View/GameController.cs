using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IslandTycoon
{
    /// <summary>
    /// Creates the whole game at runtime in whatever scene is open, so nothing
    /// has to be wired by hand in the editor. Just press Play.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (UnityEngine.Object.FindAnyObjectByType<GameController>() != null) return;
            new GameObject("IslandTycoon").AddComponent<GameController>();
        }
    }

    /// <summary>Connects model, world view, UI and audio; owns ticking, saving and pausing.</summary>
    public class GameController : MonoBehaviour
    {
        public GameModel Model { get; private set; }
        public int Selected { get; private set; } = -1;
        public bool Paused => _appPaused || (_ui != null && _ui.ModalOpen);
        public CameraRig Rig { get; private set; }

        GameConfig _cfg;
        Camera _cam;
        WorldView _world;
        UIView _ui;
        AudioManager _audio;
        float _autosave;
        bool _appPaused;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _cfg = GameConfig.Load();
            Model = new GameModel(_cfg, SaveSystem.Load());

            SetupCamera();
            SetupEventSystem();
            Rig = new CameraRig(_cam);

            _audio = new GameObject("Audio").AddComponent<AudioManager>();
            _audio.transform.SetParent(transform, false);
            _audio.Apply(Model.S.settings);

            _world = new GameObject("World").AddComponent<WorldView>();
            _world.transform.SetParent(transform, false);
            _world.Build(this);

            _ui = new GameObject("UIRoot").AddComponent<UIView>();
            _ui.transform.SetParent(transform, false);
            _ui.Build(this);

            Bind();
            if (Model.Phase == GamePhase.WaveActive) _ui.ShowToast("Welcome back! The wave continues.");
        }

        void Bind()
        {
            Model.Toast += msg => _ui.ShowToast(msg);
            Model.GuestServed += OnServed;
            Model.GuestFailed += OnFailed;
            Model.WaveStarted += () => _audio.Play(Sfx.WaveStart);
            Model.WaveFinished += () =>
            {
                Selected = -1;
                _audio.Play(Sfx.WaveDone);
                if (Model.S.tutorialStep == 2) Model.S.tutorialStep = 3;
                Save();
            };
            Model.BuildingUpgraded += n => _audio.Play(Sfx.Upgrade);
            Model.BuildingUnlocked += n => { _audio.Play(Sfx.Unlock); Fx("FX/fx_coin_burst", _world.PlotTop(n), 1.4f, 0.6f); };
            Model.IslandPurchased += i => _audio.Play(Sfx.Unlock);
            Model.IslandChanged += i => { Selected = -1; _world.Rebuild(); Save(); };
            Model.Victory += () => { _audio.Play(Sfx.Unlock); _ui.ShowVictory(); };
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.transform.position = new Vector3(0, 0, -10);
            _cam.transform.rotation = Quaternion.identity;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.62f, 0.78f);
            if (_cam.GetComponent<Physics2DRaycaster>() == null) _cam.gameObject.AddComponent<Physics2DRaycaster>();
            if (_cam.GetComponent<AudioListener>() == null && UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
                _cam.gameObject.AddComponent<AudioListener>();
        }

        static void SetupEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // new Input System if the project uses it, otherwise the classic module
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) go.AddComponent(t);
            else go.AddComponent<StandaloneInputModule>();
        }

        void Update()
        {
            Rig.Update();
            if (Paused) return;
            Model.Tick(Time.deltaTime);
            if (Model.Phase == GamePhase.WaveActive)
            {
                _autosave += Time.unscaledDeltaTime;
                if (_autosave > 3f) { _autosave = 0; Save(); }
            }
        }

        // FR-19, NFR-07: freeze when the app is minimised, keep the exact state
        void OnApplicationPause(bool pause)
        {
            _appPaused = pause;
            if (pause) Save();
        }

        void OnApplicationQuit() => Save();

        public void Save() { if (Model != null) SaveSystem.Save(Model.S); }

        // ------------------------------------------------------------------ commands from UI / world

        public void SelectBuilding(int need)
        {
            if (Model.Phase == GamePhase.WaveResult || Paused) return;
            Selected = Selected == need ? -1 : need;
            if (Selected >= 0 && Model.S.tutorialStep == 0) Model.S.tutorialStep = 1;
        }

        public void StartWave()
        {
            if (!Model.StartWave()) return;
            Selected = -1;
            if (Model.S.tutorialStep <= 1) Model.S.tutorialStep = 2;
            Save();
        }

        public void MoveWorker(int delta)
        {
            if (Selected < 0) return;
            if (delta > 0) Model.PullWorker(Selected); else Model.MoveWorker(Selected, -1);
        }

        public void UpgradeSelected() { if (Selected >= 0) Upgrade(Selected); }

        public void Upgrade(int need)
        {
            if (Model.Upgrade(need)) Save();
        }

        public void UnlockSelected()
        {
            if (Selected >= 0 && Model.Unlock(Selected)) Save();
        }

        public void BoostSelected()
        {
            if (Selected >= 0 && Model.Boost(Selected))
            {
                _audio.Play(Sfx.Upgrade);
                Fx("FX/fx_upgrade_arrow", _world.PlotTop(Selected), 1f, 0.8f);
            }
        }

        public void CollectRewards()
        {
            if (!Model.CollectRewards()) return;
            _audio.Play(Sfx.Coin);
            Fx("FX/fx_coin_burst", Vector3.zero, 2.2f, 0.5f);
            if (Model.S.tutorialStep == 3) Model.S.tutorialStep = 4;
            Save();
            if (!Model.S.won || Model.I.completedWaves != _cfg.winWaves) _ui.ShowSuggestion(Model.SuggestUpgrade());
        }

        /// <summary>Map button: travel to an owned island or buy a new one.</summary>
        public void IslandAction(int island)
        {
            if (Model.S.islands[island].purchased) Model.Travel(island);
            else Model.BuyIsland(island);
            Save();
        }

        public void ToggleSetting(int which)
        {
            var s = Model.S.settings;
            if (which == 0) s.music = !s.music;
            else if (which == 1) s.sound = !s.sound;
            else s.vibration = !s.vibration;
            _audio.Apply(s);
            Save();
        }

        public void ResetProgress()
        {
            SaveSystem.Delete();
            var settings = Model.S.settings;
            Model = new GameModel(_cfg);
            Model.S.settings = settings;
            Selected = -1;
            Bind();
            _world.Rebuild();
            Save();
            _ui.ShowToast("A fresh start on Palm Island!");
        }

        // ------------------------------------------------------------------ editor test hooks

        /// <summary>Used by the editor tools to drive the game in automated checks.</summary>
        public static string DebugCommand(string cmd)
        {
            var c = UnityEngine.Object.FindAnyObjectByType<GameController>();
            if (c == null) return "no game running";
            var parts = cmd.Split(':');
            string arg = parts.Length > 1 ? parts[1] : "";
            int.TryParse(arg, out int n);
            var m = c.Model;
            switch (parts[0])
            {
                case "open": return c._ui.OpenByName(arg) ? "opened " + arg : "unknown modal";
                case "close": c._ui.CloseAll(); return "closed";
                case "select": c._ui.CloseAll(); c.Selected = -1; c.SelectBuilding(n); return "selected " + n;
                case "start": c._ui.CloseAll(); c.StartWave(); return "wave " + m.WaveNumber;
                case "collect": c.CollectRewards(); return "collected";
                case "coins": m.S.coins += n; return "coins " + m.S.coins;
                case "energy": m.S.energy += n; return "energy " + m.S.energy;
                case "waves": m.I.completedWaves = n; m.I.waveIndex = n; return "waves " + n;
                case "buy": return m.BuyIsland(n) ? "bought" : "cannot buy";
                case "travel": return m.Travel(n) ? "travelled" : "cannot travel";
                case "unlock": return m.Unlock(n) ? "unlocked" : "cannot unlock";
                case "upgrade": return m.Upgrade(n) ? "upgraded" : "cannot upgrade";
                case "pan": c.Rig.DebugMove(n, 1f); return "pan " + n;
                case "zoomout": c.Rig.DebugMove(0, 1.5f); return "zoomout";
                case "speed": Time.timeScale = Mathf.Max(0.1f, n); return "speed " + n;
                case "win": c._ui.ShowVictory(); return "victory";
                case "suggest": c._ui.ShowSuggestion(n); return "suggest";
                case "toast": c._ui.ShowToast(arg); return "toast";
            }
            return "unknown command";
        }

        // ------------------------------------------------------------------ feedback

        void OnServed(Guest g, int need)
        {
            var p = _world.GuestPosition(g.id) + Vector3.up * 1.4f;
            Fx("FX/fx_check", p, 0.42f, 0.5f);
            Fx("UI/Icons/icon_coin", p + new Vector3(0.3f, 0.1f, 0), g.vip ? 0.42f : 0.3f, 0.9f);
            _audio.Play(Sfx.Coin);
        }

        void OnFailed(Guest g, int need)
        {
            Fx("FX/fx_angry_cloud", _world.GuestPosition(g.id) + Vector3.up * 1.4f, 0.6f, 0.4f);
            _audio.Play(Sfx.Fail);
            Vibrate();
        }

        void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Model.S.settings.vibration) Handheld.Vibrate();
#endif
        }

        /// <summary>Pop-up sprite that rises and fades.</summary>
        public void Fx(string sprite, Vector3 pos, float size, float rise)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(FxRoutine(sprite, pos, size, rise));
        }

        IEnumerator FxRoutine(string sprite, Vector3 pos, float size, float rise)
        {
            var holder = new GameObject("Fx").transform;
            holder.position = new Vector3(pos.x, pos.y, 0);
            var sr = Art.MakeSprite("Sprite", holder, sprite, 9800);
            Art.FitCenter(sr, size);
            float t = 0, dur = 0.9f;
            while (t < dur && sr != null)
            {
                t += Time.deltaTime;
                float k = t / dur;
                float pop = k < 0.2f ? Mathf.Lerp(0.3f, 1.15f, k / 0.2f) : Mathf.Lerp(1.15f, 1f, (k - 0.2f) / 0.2f);
                holder.localScale = Vector3.one * pop;
                holder.position = new Vector3(pos.x, pos.y + rise * k, 0);
                sr.color = new Color(1, 1, 1, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                yield return null;
            }
            if (holder != null) Destroy(holder.gameObject);
        }
    }
}
