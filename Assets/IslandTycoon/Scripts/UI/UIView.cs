using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>
    /// Screen-space UI. Layout rules: thumbs reach the bottom corners (NFR-02),
    /// one primary orange CTA per screen (NFR-03), needs panel on the left edge
    /// so the island stays visible (NFR-09). Inside panels every element is placed
    /// by its centre (or left edge for left-aligned text) on a fixed grid.
    /// </summary>
    public class UIView : MonoBehaviour
    {
        GameController _c;
        GameModel M => _c.Model;
        Transform _root;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Vector2 Top = new Vector2(0.5f, 1), Bottom = new Vector2(0.5f, 0), Mid = new Vector2(0.5f, 0.5f);

        // HUD
        Text _coins, _energy, _value, _waveTitle, _waveSub, _freeWorkers, _toast, _satText, _hint;
        Image _satFace, _mapBadge;
        RectTransform _satFill, _satBar, _hintPanel;
        CanvasGroup _toastGroup;
        float _toastTime;

        // needs panel
        Text _needsHeader;
        readonly Image[] _rowBg = new Image[Needs.Count];
        readonly Text[] _rowName = new Text[Needs.Count];
        readonly Text[] _rowValue = new Text[Needs.Count];
        readonly Image[] _rowWarn = new Image[Needs.Count];
        RectTransform _needsPanel;

        Button _startWave;
        RectTransform _hud, _menuStatsPill;
        static readonly Color RowColor = new Color(0.55f, 0.33f, 0.14f, 0.13f);

        // building panel
        RectTransform _bPanel;
        Text _bTitle, _bInfo, _bWorkers, _bStatus;
        Image _bIcon;
        Button _bMinus, _bPlus, _bUpgrade, _bBoost, _bUnlock;
        Text _bUpgradeLabel, _bBoostLabel, _bUnlockLabel;

        // modals
        RectTransform _resultModal, _suggestModal, _settingsModal, _helpModal, _confirmModal, _mapModal, _victoryModal, _menuModal;
        Text _rTitle, _rSat, _sText, _setStats, _mapGoal, _victoryText, _menuStats;
        readonly Text[] _rReward = new Text[3];
        Image _rFace;
        readonly Image[] _rStars = new Image[3];
        readonly Image[] _rLineIcon = new Image[Needs.Count];
        readonly Text[] _rLineName = new Text[Needs.Count];
        readonly Text[] _rLineCount = new Text[Needs.Count];
        readonly Text[] _rLineState = new Text[Needs.Count];
        Button _sUpgrade;
        int _suggestNeed = -1;
        readonly Image[] _setIcon = new Image[3];
        readonly Text[] _setState = new Text[3];
        readonly Image[] _setPill = new Image[3];
        readonly Text[] _mapStatus = new Text[4];
        readonly Button[] _mapButton = new Button[4];
        readonly Text[] _mapButtonLabel = new Text[4];

        public bool ModalOpen =>
            Active(_settingsModal) || Active(_helpModal) || Active(_confirmModal) || Active(_mapModal) || Active(_victoryModal) || Active(_menuModal);

        static bool Active(Component c) => c != null && c.gameObject.activeSelf;

        public void Build(GameController c)
        {
            _c = c;
            var canvas = UIFactory.Canvas("UI");
            canvas.transform.SetParent(transform, false);
            var safe = UIFactory.Rect("SafeArea", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            UIFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            _root = safe;

            _hud = UIFactory.Rect("Hud", safe, Mid, Vector2.zero, Vector2.zero);
            UIFactory.Stretch(_hud);
            var gameRoot = _root;
            _root = _hud;
            BuildHud();
            BuildNeedsPanel();
            BuildBuildingPanel();
            _root = gameRoot;
            BuildResultModal();
            BuildSuggestModal();
            BuildSettingsModal();
            BuildHelpModal();
            BuildConfirmModal();
            BuildMapModal();
            BuildVictoryModal();
            BuildMenu();
        }

        // ------------------------------------------------------------------ HUD

        void BuildHud()
        {
            var tl = new Vector2(0, 1);
            _coins = Pill("Coins", "UI/Icons/icon_coin", tl, new Vector2(20, -16));
            _energy = Pill("Energy", "UI/Icons/icon_energy", tl, new Vector2(270, -16));
            _value = Pill("ResortValue", "UI/Icons/icon_resort_value", tl, new Vector2(520, -16));

            var plaque = UIFactory.WoodPill("TitlePlaque", _root, Top, new Vector2(170, -10), new Vector2(620, 118), 80f, out var pin);
            _waveTitle = UIFactory.FitText("WaveTitle", pin, "WAVE 1", 40, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.42f), new Vector2(1, 1), true);
            _waveSub = UIFactory.FitText("WaveSub", pin, "", 22, UIFactory.Cream, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0.46f));

            // live satisfaction bar (FR-09)
            _satBar = UIFactory.Centered("Satisfaction", _root, Top, new Vector2(150, -166), new Vector2(430, 72));
            var empty = UIFactory.Image("Empty", _satBar, Art.Get("UI/Kit/bar_empty"), new Vector2(0, 0.5f), Vector2.zero, new Vector2(430, 72));
            empty.preserveAspect = false;
            _satFill = UIFactory.Rect("FillMask", _satBar, new Vector2(0, 0.5f), Vector2.zero, new Vector2(430, 72));
            _satFill.gameObject.AddComponent<RectMask2D>();
            var full = UIFactory.Image("Full", _satFill, Art.Get("UI/Kit/bar_full"), new Vector2(0, 0.5f), Vector2.zero, new Vector2(430, 72));
            full.preserveAspect = false;
            _satFace = UIFactory.Icon("Face", _satBar, Art.Get("UI/Icons/icon_face_happy"), new Vector2(1, 0.5f), new Vector2(34, 0), 76);
            _satText = UIFactory.FitText("Pct", _satBar, "100%", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0.22f, 0.2f), new Vector2(0.95f, 0.85f), true);

            // top right: settings + map
            var tr = new Vector2(1, 1);
            UIFactory.IconButton("Settings", _root, "UI/Icons/icon_settings", "II", tr, new Vector2(-20, -14), new Vector2(96, 96), () => Open(_settingsModal));
            var map = UIFactory.IconButton("Map", _root, "UI/Icons/icon_map", "MAP", tr, new Vector2(-126, -14), new Vector2(96, 96), () => Open(_mapModal));
            _mapBadge = UIFactory.Icon("Badge", map.transform, Art.Get("UI/Icons/icon_warning"), tr, new Vector2(-6, -6), 42);

            // free workers (bottom left)
            var wp = UIFactory.WoodPill("FreeWorkers", _root, Vector2.zero, new Vector2(20, 20), new Vector2(330, 88), 80f, out var win);
            UIFactory.Icon("Icon", wp.transform, Art.Get("Characters/Worker/worker_wave"), new Vector2(0, 0.5f), new Vector2(42, 6), 92);
            _freeWorkers = UIFactory.FitText("Text", win, "", 28, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, true);

            // primary CTA (bottom right)
            _startWave = UIFactory.Button("StartWave", _root, "UI/Kit/btn_primary_orange", "START WAVE", 46,
                new Vector2(1, 0), new Vector2(-24, 20), new Vector2(370, 0), () => _c.StartWave());

            // tutorial hint
            var hp = UIFactory.WoodPill("Hint", _root, Top, new Vector2(150, -244), new Vector2(980, 92), 90f, out var hin);
            _hintPanel = hp.rectTransform;
            _hintPanel.pivot = new Vector2(0.5f, 0.5f);
            _hint = UIFactory.FitText("Text", hin, "", 26, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, true);

            // toast
            var toast = UIFactory.WoodPill("Toast", _root, Top, new Vector2(150, -340), new Vector2(980, 86), 90f, out var tin).rectTransform;
            toast.pivot = new Vector2(0.5f, 0.5f);
            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.blocksRaycasts = false;
            _toast = UIFactory.FitText("Text", tin, "", 27, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, true);
            _toastGroup.alpha = 0;
        }

        Text Pill(string name, string icon, Vector2 anchor, Vector2 pos)
        {
            var p = UIFactory.WoodPill(name, _root, anchor, pos, new Vector2(234, 80), 70f, out var inner);
            UIFactory.Icon("Icon", p.transform, Art.Get(icon), new Vector2(0, 0.5f), new Vector2(32, 2), 76);
            return UIFactory.FitText("Value", inner, "0", 34, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, true);
        }

        void BuildNeedsPanel()
        {
            var content = UIFactory.Frame("NeedsPanel", _root, new Vector2(0, 0.5f), new Vector2(16, 10), new Vector2(360, 600), out _needsPanel, 70f);
            _needsPanel.pivot = new Vector2(0, 0.5f);
            _needsPanel.GetComponent<Image>().raycastTarget = false;
            _needsHeader = UIFactory.Label("Header", content, "NEXT WAVE", 30, UIFactory.Brown, Top, new Vector2(0, -20), new Vector2(290, 44));
            for (int n = 0; n < Needs.Count; n++)
            {
                var row = UIFactory.Panel("Row_" + (NeedType)n, content, Top, new Vector2(0, -70), new Vector2(286, 84), RowColor);
                int need = n;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row;
                btn.onClick.AddListener(() => _c.SelectBuilding(need));
                _rowBg[n] = row;
                UIFactory.Icon("Icon", row.transform, Art.Get(Art.NeedIcon(n)), new Vector2(0, 0.5f), new Vector2(42, 0), 68);
                _rowName[n] = UIFactory.Text("Name", row.transform, "", 26, UIFactory.Brown, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(84, 15), new Vector2(160, 34));
                _rowName[n].rectTransform.pivot = new Vector2(0, 0.5f);
                _rowValue[n] = UIFactory.Text("Value", row.transform, "", 22, UIFactory.Brown, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(84, -17), new Vector2(170, 30));
                _rowValue[n].rectTransform.pivot = new Vector2(0, 0.5f);
                _rowWarn[n] = UIFactory.Icon("Warn", row.transform, Art.Get("UI/Icons/icon_warning"), new Vector2(1, 0.5f), new Vector2(-30, 0), 48);
            }
        }

        void BuildBuildingPanel()
        {
            var content = UIFactory.Frame("BuildingPanel", _root, Bottom, new Vector2(0, 10), new Vector2(1020, 340), out _bPanel, 78f);
            var left = new Vector2(0, 0.5f); var tl = new Vector2(0, 1); var bl = new Vector2(0, 0); var r = new Vector2(1, 0.5f);

            _bIcon = UIFactory.Icon("Icon", content, null, left, new Vector2(72, 10), 130);
            _bTitle = UIFactory.Text("Title", content, "", 38, UIFactory.Brown, TextAnchor.MiddleLeft, tl, new Vector2(160, -30), new Vector2(470, 56));
            _bTitle.rectTransform.pivot = new Vector2(0, 0.5f);
            _bInfo = UIFactory.Text("Info", content, "", 25, UIFactory.Brown, TextAnchor.MiddleLeft, tl, new Vector2(160, -80), new Vector2(470, 64));
            _bInfo.rectTransform.pivot = new Vector2(0, 0.5f);
            _bStatus = UIFactory.Text("Status", content, "", 30, UIFactory.Green, TextAnchor.MiddleLeft, bl, new Vector2(160, 50), new Vector2(470, 56));
            _bStatus.rectTransform.pivot = new Vector2(0, 0.5f);

            _bMinus = UIFactory.SignButton("Minus", content, false, bl, new Vector2(205, 48), 96, () => _c.MoveWorker(-1));
            _bWorkers = UIFactory.Label("Workers", content, "", 26, UIFactory.Brown, bl, new Vector2(320, 50), new Vector2(130, 76));
            _bPlus = UIFactory.SignButton("Plus", content, true, bl, new Vector2(435, 48), 96, () => _c.MoveWorker(+1));

            _bUpgrade = UIFactory.Button("Upgrade", content, "UI/Kit/btn_secondary_teal", "UPGRADE", 32, r, new Vector2(-145, 58), new Vector2(270, 0), () => _c.UpgradeSelected(), true);
            _bUpgradeLabel = _bUpgrade.GetComponentInChildren<Text>();
            _bBoost = UIFactory.Button("Boost", content, "UI/Kit/btn_secondary_teal", "BOOST", 32, r, new Vector2(-145, -58), new Vector2(270, 0), () => _c.BoostSelected(), true);
            _bBoostLabel = _bBoost.GetComponentInChildren<Text>();
            _bUnlock = UIFactory.Button("Unlock", content, "UI/Kit/btn_secondary_teal", "UNLOCK", 32, r, new Vector2(-140, 0), new Vector2(260, 0), () => _c.UnlockSelected(), true);
            _bUnlockLabel = _bUnlock.GetComponentInChildren<Text>();

            UIFactory.IconButton("Close", _bPanel, "UI/Icons/icon_close", "X", new Vector2(1, 1), new Vector2(-6, -6), new Vector2(84, 84), () => _c.SelectBuilding(-1), true);
            _bPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ modals

        RectTransform Modal(string name, Vector2 size, string title, out RectTransform content, float dim = 0.55f) =>
            Modal(name, size, title, out content, out _, dim);

        RectTransform Modal(string name, Vector2 size, string title, out RectTransform content, out Text ribbon, float dim = 0.55f)
        {
            ribbon = null;
            var bg = UIFactory.Rect(name, _root, Mid, Vector2.zero, Vector2.zero);
            UIFactory.Stretch(bg, -200);
            var img = bg.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, dim);
            content = UIFactory.Frame("Panel", bg, Mid, new Vector2(0, -20), size, out var outer);
            outer.pivot = Mid;
            if (!string.IsNullOrEmpty(title)) ribbon = UIFactory.Ribbon(outer, title, Mathf.Min(size.x - 160, 640));
            bg.gameObject.SetActive(false);
            return bg;
        }

        void Open(RectTransform modal)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(Sfx.Whoosh);
            modal.gameObject.SetActive(true);
            modal.SetAsLastSibling();
        }

        static void Close(RectTransform modal) => modal.gameObject.SetActive(false);

        void BuildResultModal()
        {
            _resultModal = Modal("ResultModal", new Vector2(1040, 820), "WAVE COMPLETE", out var p, out _rTitle);
            for (int i = 0; i < 3; i++)
                _rStars[i] = UIFactory.Icon("Star" + i, p, Art.Get("UI/Icons/icon_resort_value"), Top, new Vector2((i - 1) * 140, i == 1 ? -92 : -104), i == 1 ? 132 : 112);

            _rFace = UIFactory.Icon("Face", p, null, Top, new Vector2(-170, -200), 64);
            _rSat = UIFactory.Text("Sat", p, "", 34, UIFactory.Brown, TextAnchor.MiddleLeft, Top, new Vector2(-125, -200), new Vector2(360, 56));
            _rSat.rectTransform.pivot = new Vector2(0, 0.5f);

            string[] icons = { "UI/Icons/icon_coin", "UI/Icons/icon_energy", "UI/Icons/icon_resort_value" };
            for (int i = 0; i < 3; i++)
            {
                float gx = (i - 1) * 290;
                UIFactory.Icon("RewardIcon" + i, p, Art.Get(icons[i]), Top, new Vector2(gx - 70, -275), 58);
                _rReward[i] = UIFactory.Text("Reward" + i, p, "", 34, UIFactory.Brown, TextAnchor.MiddleLeft, Top, new Vector2(gx - 32, -275), new Vector2(180, 52));
                _rReward[i].rectTransform.pivot = new Vector2(0, 0.5f);
            }

            var line = UIFactory.Panel("Divider", p, Top, new Vector2(0, -318), new Vector2(820, 4), new Color(0.45f, 0.28f, 0.1f, 0.35f));
            line.raycastTarget = false;
            line.rectTransform.pivot = Mid;

            for (int n = 0; n < Needs.Count; n++)
            {
                float y = -352 - n * 42;
                _rLineIcon[n] = UIFactory.Icon("LineIcon" + n, p, null, Top, new Vector2(-330, y), 38);
                _rLineName[n] = UIFactory.Text("LineName" + n, p, "", 26, UIFactory.Brown, TextAnchor.MiddleLeft, Top, new Vector2(-298, y), new Vector2(230, 38));
                _rLineName[n].rectTransform.pivot = new Vector2(0, 0.5f);
                _rLineCount[n] = UIFactory.Label("LineCount" + n, p, "", 26, UIFactory.Brown, Top, new Vector2(-10, y), new Vector2(150, 38));
                _rLineState[n] = UIFactory.Text("LineState" + n, p, "", 26, UIFactory.Green, TextAnchor.MiddleLeft, Top, new Vector2(90, y), new Vector2(280, 38));
                _rLineState[n].rectTransform.pivot = new Vector2(0, 0.5f);
            }
            UIFactory.Button("Collect", p, "UI/Kit/btn_primary_orange", "COLLECT", 44, Bottom, new Vector2(0, 72), new Vector2(310, 0), () => _c.CollectRewards(), true);
        }

        void BuildSuggestModal()
        {
            _suggestModal = Modal("SuggestModal", new Vector2(920, 480), "UPGRADE TIP", out var p);
            _sText = UIFactory.Label("Text", p, "", 32, UIFactory.Brown, Top, new Vector2(0, -130), new Vector2(780, 170));
            _sUpgrade = UIFactory.Button("UpgradeNow", p, "UI/Kit/btn_primary_orange", "UPGRADE NOW", 34, Bottom, new Vector2(-170, 70), new Vector2(300, 0), () =>
            {
                if (_suggestNeed >= 0) _c.Upgrade(_suggestNeed);
                CloseSuggest();
            }, true);
            UIFactory.Button("Later", p, "UI/Kit/btn_secondary_teal", "LATER", 34, Bottom, new Vector2(170, 70), new Vector2(260, 0), CloseSuggest, true);
        }

        void BuildSettingsModal()
        {
            _settingsModal = Modal("SettingsModal", new Vector2(860, 840), "SETTINGS", out var p);
            string[] names = { "Music", "Sound effects", "Vibration" };
            for (int i = 0; i < 3; i++)
            {
                int which = i;
                float y = -90 - i * 100;
                var row = UIFactory.Panel("Row" + i, p, Top, new Vector2(0, y), new Vector2(720, 94), new Color(0.55f, 0.33f, 0.14f, 0.14f));
                row.rectTransform.pivot = Mid;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row;
                btn.onClick.AddListener(() => _c.ToggleSetting(which));
                row.gameObject.AddComponent<ButtonPop>();
                _setIcon[i] = UIFactory.Icon("Icon", row.transform, null, new Vector2(0, 0.5f), new Vector2(56, 0), 80);
                var label = UIFactory.Text("Label", row.transform, names[i], 34, UIFactory.Brown, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(112, 0), new Vector2(360, 60));
                label.rectTransform.pivot = new Vector2(0, 0.5f);
                _setPill[i] = UIFactory.Panel("State", row.transform, new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(140, 62), UIFactory.Green);
                _setPill[i].rectTransform.pivot = Mid;
                _setPill[i].raycastTarget = false;
                _setState[i] = UIFactory.Label("Text", _setPill[i].transform, "ON", 30, Color.white, Mid, Vector2.zero, new Vector2(130, 56), TextAnchor.MiddleCenter, true);
            }
            _setStats = UIFactory.Label("Stats", p, "", 25, UIFactory.Brown, Top, new Vector2(0, -382), new Vector2(720, 44));

            UIFactory.Button("Help", p, "UI/Kit/btn_secondary_teal", "HELP", 30, Bottom, new Vector2(-165, 205), new Vector2(290, 0), () => Open(_helpModal), true);
            UIFactory.Button("Reset", p, "UI/Kit/btn_secondary_teal", "RESET", 30, Bottom, new Vector2(165, 205), new Vector2(290, 0), () => Open(_confirmModal), true);
            UIFactory.Button("Resume", p, "UI/Kit/btn_primary_orange", "RESUME", 40, Bottom, new Vector2(0, 70), new Vector2(290, 0), () => Close(_settingsModal), true);
        }

        void BuildHelpModal()
        {
            _helpModal = Modal("HelpModal", new Vector2(1280, 880), "HOW TO PLAY", out var p);
            (string icon, string text)[] tips =
            {
                ("UI/Icons/icon_need_drinks", "Guests arrive in <b>waves</b> and ask for Rooms, Drinks, Food and more."),
                ("Characters/Worker/worker_front", "Tap a building and move <b>workers</b> with + and -. Busy places need more hands."),
                ("UI/Icons/icon_warning", "A red warning sign is a <b>bottleneck</b>: guests wait too long. Move a worker there!"),
                ("UI/Icons/icon_face_happy", "Fast service = high <b>satisfaction</b> = more coins and stars."),
                ("UI/Icons/icon_coin", "Spend <b>coins</b> on upgrades (faster, more slots) and on new areas."),
                ("UI/Icons/icon_energy", "<b>Energy</b> boosts one building for a few seconds. It is optional."),
                ("UI/Icons/icon_map", "After 8 waves buy a <b>second island</b> on the map. VIP guests pay double!"),
                ("UI/Icons/icon_trophy", "<b>Goal:</b> finish 10 waves on Coral Lagoon to become Resort Master."),
            };
            for (int i = 0; i < tips.Length; i++)
            {
                float y = -58 - i * 68;
                UIFactory.Icon("TipIcon" + i, p, Art.First(tips[i].icon, "UI/Icons/icon_resort_value"), Top, new Vector2(-520, y), 58);
                var t = UIFactory.Text("Tip" + i, p, tips[i].text, 28, UIFactory.Brown, TextAnchor.MiddleLeft, Top, new Vector2(-470, y), new Vector2(1010, 66));
                t.rectTransform.pivot = new Vector2(0, 0.5f);
            }
            UIFactory.Button("Ok", p, "UI/Kit/btn_primary_orange", "GOT IT", 40, Bottom, new Vector2(0, 68), new Vector2(280, 0), () => Close(_helpModal), true);
        }

        void BuildConfirmModal()
        {
            _confirmModal = Modal("ConfirmModal", new Vector2(880, 460), "START OVER?", out var p);
            UIFactory.Label("Text", p, "All islands, coins and upgrades will be lost.", 32, UIFactory.Brown, Top, new Vector2(0, -120), new Vector2(760, 120));
            UIFactory.Button("Yes", p, "UI/Kit/btn_secondary_teal", "RESET", 34, Bottom, new Vector2(-160, 68), new Vector2(260, 0), () =>
            {
                Close(_confirmModal); Close(_settingsModal);
                _c.ResetProgress();
            }, true);
            UIFactory.Button("No", p, "UI/Kit/btn_primary_orange", "CANCEL", 34, Bottom, new Vector2(160, 68), new Vector2(280, 0), () => Close(_confirmModal), true);
        }

        void BuildMapModal()
        {
            _mapModal = Modal("MapModal", new Vector2(1480, 900), "ISLANDS", out var p);
            int count = Mathf.Min(_mapStatus.Length, M.Cfg.islands.Length);
            float cardW = 620, gap = 50;
            float x0 = -(count - 1) * (cardW + gap) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                int island = i;
                var ic = M.Cfg.Island(i);
                var card = UIFactory.Panel("Card" + i, p, Top, new Vector2(x0 + i * (cardW + gap), -50), new Vector2(cardW, 560), new Color(0.55f, 0.33f, 0.14f, 0.16f));
                card.rectTransform.pivot = new Vector2(0.5f, 1);
                var thumb = Art.TryGet(ic.background);
                var img = UIFactory.Image("Thumb", card.transform, thumb != null ? thumb : Art.Get(ic.fallbackBackground), Top, new Vector2(0, -16), new Vector2(533, 300));
                img.rectTransform.pivot = new Vector2(0.5f, 1);
                if (thumb == null) img.color = ic.fallbackTint;
                UIFactory.Label("Name", card.transform, ic.name, 38, UIFactory.Brown, Top, new Vector2(0, -350), new Vector2(580, 52));
                _mapStatus[i] = UIFactory.Label("Status", card.transform, "", 24, UIFactory.Brown, Top, new Vector2(0, -394), new Vector2(580, 38));
                _mapButton[i] = UIFactory.Button("Go", card.transform, "UI/Kit/btn_secondary_teal", "TRAVEL", 32, Bottom, new Vector2(0, 66), new Vector2(260, 0), () => _c.IslandAction(island), true);
                _mapButtonLabel[i] = _mapButton[i].GetComponentInChildren<Text>();
            }
            _mapGoal = UIFactory.Text("Goal", p, "", 28, UIFactory.Brown, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(30, 70), new Vector2(900, 56));
            _mapGoal.rectTransform.pivot = new Vector2(0, 0.5f);
            UIFactory.Button("Close", p, "UI/Kit/btn_primary_orange", "CLOSE", 38, new Vector2(1, 0), new Vector2(-150, 70), new Vector2(260, 0), () => Close(_mapModal), true);
        }

        void BuildVictoryModal()
        {
            _victoryModal = Modal("VictoryModal", new Vector2(980, 780), "RESORT MASTER!", out var p, 0.65f);
            UIFactory.Icon("Trophy", p, Art.First("UI/Icons/icon_trophy", "UI/Icons/icon_resort_value"), Top, new Vector2(0, -150), 210);
            _victoryText = UIFactory.Label("Text", p, "", 30, UIFactory.Brown, Top, new Vector2(0, -385), new Vector2(820, 200));
            UIFactory.Button("Keep", p, "UI/Kit/btn_primary_orange", "CONTINUE", 40, Bottom, new Vector2(0, 76), new Vector2(320, 0), () => Close(_victoryModal), true);
        }

        void BuildMenu()
        {
            _menuModal = UIFactory.Rect("Menu", _root, Mid, Vector2.zero, Vector2.zero);
            UIFactory.Stretch(_menuModal, -200);
            var block = _menuModal.gameObject.AddComponent<Image>();
            block.color = new Color(0.13f, 0.56f, 0.98f);

            // sky fills the whole screen whatever its aspect
            var sky = Art.TryGet("Backgrounds/sky");
            if (sky != null)
            {
                var skyImg = UIFactory.Image("Sky", _menuModal, sky, Mid, Vector2.zero, new Vector2(1920, 1080));
                skyImg.preserveAspect = false;
                skyImg.rectTransform.pivot = Mid;
                var fit = skyImg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = sky.rect.width / sky.rect.height;
                skyImg.gameObject.AddComponent<Drift>().Amount = new Vector3(0.02f, 0, 6f);
            }

            var logo = Art.TryGet("UI/logo");
            if (logo != null)
            {
                var l = UIFactory.Icon("Logo", _menuModal, logo, Mid, new Vector2(0, 175), 1);
                l.rectTransform.sizeDelta = new Vector2(980, 411);
                l.gameObject.AddComponent<Drift>().Amount = new Vector3(0, 10f, 2.2f);
            }
            else UIFactory.Label("Logo", _menuModal, "ISLAND TYCOON", 130, new Color(1f, 0.75f, 0.3f), Mid, new Vector2(0, 200), new Vector2(1400, 170), TextAnchor.MiddleCenter, true);

            UIFactory.Button("Play", _menuModal, "UI/Kit/btn_primary_orange", "PLAY", 66, Mid, new Vector2(0, -175), new Vector2(440, 0), () => Close(_menuModal), true);
            UIFactory.IconButton("MenuSettings", _menuModal, "UI/Icons/icon_settings", "II", Mid, new Vector2(-350, -175), new Vector2(136, 136), () => Open(_settingsModal), true);
            UIFactory.IconButton("MenuHelp", _menuModal, "UI/Icons/icon_help", "?", Mid, new Vector2(350, -175), new Vector2(136, 136), () => Open(_helpModal), true);

            _menuStatsPill = UIFactory.WoodPill("StatsPill", _menuModal, Mid, new Vector2(0, -360), new Vector2(700, 90), 90f, out var sin).rectTransform;
            _menuStatsPill.pivot = Mid;
            _menuStats = UIFactory.FitText("Stats", sin, "", 28, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, true);
            _menuModal.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ public

        public bool OpenByName(string name)
        {
            RectTransform m = null;
            switch (name)
            {
                case "settings": m = _settingsModal; break;
                case "help": m = _helpModal; break;
                case "confirm": m = _confirmModal; break;
                case "map": m = _mapModal; break;
                case "victory": m = _victoryModal; break;
                case "menu": m = _menuModal; break;
            }
            if (m == null) return false;
            CloseAll();
            Open(m);
            return true;
        }

        public void CloseAll()
        {
            foreach (var m in new[] { _settingsModal, _helpModal, _confirmModal, _mapModal, _victoryModal, _menuModal, _suggestModal })
                if (m != null) Close(m);
        }

        public void ShowToast(string msg)
        {
            UIFactory.SetText(_toast, msg);
            _toastTime = 3.2f;
        }

        public void ShowSuggestion(int need)
        {
            _suggestNeed = need;
            if (need < 0) return;
            var nc = M.Cfg.Need(need);
            int cost = M.UpgradeCost(need);
            bool can = M.CanUpgrade(need);
            _sText.text = $"{nc.displayName} slowed your guests down.\nUpgrade <b>{nc.buildingName}</b> to level {M.B(need).level + 1} for {cost} coins?"
                          + (can ? "" : $"\n<size=26>(you have {M.S.coins} coins)</size>");
            _sUpgrade.interactable = can;
            Open(_suggestModal);
        }

        public void ShowVictory()
        {
            _victoryText.text = $"You built two thriving resorts!\nResort value {M.S.resortValue}   ·   {M.S.totalStars} stars\n\n<size=26>The guests keep coming. Can you get 3 stars on every wave?</size>";
            Open(_victoryModal);
        }

        void CloseSuggest()
        {
            Close(_suggestModal);
            _suggestNeed = -1;
        }

        // ------------------------------------------------------------------ refresh

        void Update()
        {
            if (_c == null || M == null) return;
            UIFactory.SetActive(_hud, !Active(_menuModal));
            var s = M.S;
            UIFactory.SetText(_coins, s.coins.ToString());
            UIFactory.SetText(_energy, s.energy.ToString());
            UIFactory.SetText(_value, s.resortValue.ToString());
            UIFactory.SetText(_waveTitle, "WAVE " + M.WaveNumber);
            UIFactory.SetText(_freeWorkers, $"Free workers: {M.FreeWorkers}");

            bool awaiting = M.Phase == GamePhase.AwaitingWave;
            bool active = M.Phase == GamePhase.WaveActive;
            bool result = M.Phase == GamePhase.WaveResult;
            string island = M.IC.name;

            if (_c.Paused && active) UIFactory.SetText(_waveSub, $"{island} · paused");
            else if (awaiting) UIFactory.SetText(_waveSub, $"{island} · {M.ForecastGuests()} guests are coming");
            else if (active)
            {
                int left = M.I.plans.Count - M.I.nextPlan;
                UIFactory.SetText(_waveSub, left > 0 ? $"{island} · guests arriving: {left} more" : $"{island} · last guests: {M.I.guests.Count}");
            }
            else UIFactory.SetText(_waveSub, $"{island} · wave complete!");

            UIFactory.SetActive(_satBar, active);
            if (active)
            {
                float sat = M.Satisfaction();
                _satFill.sizeDelta = new Vector2(520 * Mathf.Clamp01(sat / 100f), 88);
                UIFactory.SetText(_satText, Mathf.RoundToInt(sat) + "%");
                UIFactory.SetSprite(_satFace, Art.Get(Art.FaceIcon(sat)));
            }

            bool islandReady = false;
            for (int i = 1; i < M.S.islands.Count; i++) islandReady |= M.CanBuyIsland(i);
            UIFactory.SetActive(_mapBadge, islandReady);
            if (islandReady) _mapBadge.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.12f);

            UIFactory.SetActive(_startWave, awaiting);
            RefreshNeeds(awaiting, active);
            RefreshBuildingPanel();
            RefreshHint();

            UIFactory.SetActive(_resultModal, result && !ModalOpen);
            if (result) RefreshResult();
            if (Active(_settingsModal)) RefreshSettings();
            if (Active(_mapModal)) RefreshMap();
            if (Active(_menuModal))
            {
                UIFactory.SetActive(_menuStatsPill, s.resortValue > 0);
                UIFactory.SetText(_menuStats, $"Resort value {s.resortValue}   ·   {s.totalStars} stars");
            }

            if (_toastTime > 0)
            {
                _toastTime -= Time.unscaledDeltaTime;
                _toastGroup.alpha = Mathf.Clamp01(_toastTime / 0.4f);
            }
            else _toastGroup.alpha = 0;
        }

        void RefreshHint()
        {
            string hint = null;
            switch (M.S.tutorialStep)
            {
                case 0: hint = "Welcome! Tap a building to see who works there."; break;
                case 1: hint = "Use <b>+</b> and <b>-</b> to move workers to busy places, then press <b>START WAVE</b>."; break;
                case 2: hint = "Guests show what they want. A red warning sign means a bottleneck: send a worker!"; break;
                case 3: hint = "Wave done! Collect the rewards and spend coins on upgrades."; break;
            }
            bool show = hint != null && !ModalOpen && !Active(_resultModal) && !Active(_suggestModal);
            UIFactory.SetActive(_hintPanel, show);
            if (show) UIFactory.SetText(_hint, hint);
        }

        void RefreshNeeds(bool awaiting, bool active)
        {
            UIFactory.SetText(_needsHeader, awaiting ? "NEXT WAVE" : "GUEST NEEDS");
            int[] fc = awaiting ? M.Forecast() : null;
            int visible = 0;
            for (int n = 0; n < Needs.Count; n++)
            {
                bool show = M.IsNeedActive(n);
                UIFactory.SetActive(_rowBg[n], show);
                if (!show) continue;
                _rowBg[n].rectTransform.anchoredPosition = new Vector2(0, -50 - visible * 92);
                visible++;

                bool bottleneck = M.IsBottleneck(n);
                UIFactory.SetText(_rowName[n], M.Cfg.Need(n).displayName);
                string value;
                if (awaiting) value = $"<color=#B5651D>x{fc[n]} requests</color>";
                else if (active)
                {
                    var st = M.I.stats[n];
                    value = $"<color={(bottleneck ? "#C0392B" : "#2E7D32")}>{st.served}/{st.requested} · {M.QueueCount(n)} wait</color>";
                }
                else value = "";
                UIFactory.SetText(_rowValue[n], value);
                UIFactory.SetActive(_rowWarn[n], bottleneck);
                if (bottleneck) _rowWarn[n].transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 8f) * 0.1f);
                _rowBg[n].color = bottleneck ? new Color(0.9f, 0.3f, 0.25f, 0.35f)
                    : _c.Selected == n ? new Color(1f, 0.7f, 0.2f, 0.4f) : RowColor;
            }
            _needsPanel.sizeDelta = new Vector2(360, 128 + visible * 92);
        }

        void RefreshBuildingPanel()
        {
            int n = _c.Selected;
            bool show = n >= 0 && M.Exists(n) && M.Phase != GamePhase.WaveResult && !ModalOpen;
            UIFactory.SetActive(_bPanel, show);
            if (!show) return;

            var b = M.B(n);
            var nc = M.Cfg.Need(n);
            UIFactory.SetSprite(_bIcon, Art.Get(Art.NeedIcon(n)));

            bool unlocked = b.unlocked;
            UIFactory.SetActive(_bMinus, unlocked);
            UIFactory.SetActive(_bPlus, unlocked);
            UIFactory.SetActive(_bWorkers, unlocked);
            UIFactory.SetActive(_bUpgrade, unlocked);
            bool boostVisible = unlocked && M.Phase == GamePhase.WaveActive;
            UIFactory.SetActive(_bBoost, boostVisible);
            var up = (RectTransform)_bUpgrade.transform;
            up.anchoredPosition = new Vector2(up.anchoredPosition.x, boostVisible ? 58 : 0);
            UIFactory.SetActive(_bUnlock, !unlocked);
            UIFactory.SetActive(_bStatus, !unlocked);

            if (unlocked)
            {
                UIFactory.SetText(_bTitle, $"{nc.buildingName}  <color=#B5651D>Lv {b.level}</color>");
                string boost = b.boostLeft > 0 ? $"   <color=#1E8C3A>BOOST {Mathf.CeilToInt(b.boostLeft)}s</color>" : "";
                UIFactory.SetText(_bInfo, $"Speed x{M.Speed(n).ToString("0.0", Inv)}   ·   Slots {M.Capacity(n)}   ·   Queue {M.QueueCount(n)}{boost}");
                UIFactory.SetText(_bWorkers, $"<size=22>Workers</size>\n{b.workers}");
                _bMinus.interactable = b.workers > 0;
                _bPlus.interactable = M.FreeWorkers > 0 || M.AssignedWorkers - b.workers > 0;

                int cost = M.UpgradeCost(n);
                UIFactory.SetText(_bUpgradeLabel, cost < 0 ? "MAX LEVEL" : $"UPGRADE\n{cost} coins");
                _bUpgrade.interactable = M.CanUpgrade(n);
                UIFactory.SetText(_bBoostLabel, $"BOOST\n{M.Cfg.boostEnergyCost} energy");
                _bBoost.interactable = M.CanBoost(n);
            }
            else
            {
                var plot = M.Plot(n);
                UIFactory.SetText(_bTitle, $"{nc.buildingName}  <color=#8A7F70>locked</color>");
                UIFactory.SetText(_bInfo, $"Adds the {nc.displayName} need and +{M.IC.workersPerUnlock} worker.");
                bool avail = M.IsUnlockAvailable(n);
                _bStatus.color = avail ? UIFactory.Green : UIFactory.Red;
                UIFactory.SetText(_bStatus, avail ? "Area available!" : $"Opens after wave {plot.unlockAfterWave}");
                UIFactory.SetText(_bUnlockLabel, $"UNLOCK\n{plot.unlockCost} coins");
                _bUnlock.interactable = M.CanUnlock(n);
            }
        }

        void RefreshResult()
        {
            var r = M.I.result;
            UIFactory.SetText(_rTitle, $"WAVE {r.wave} COMPLETE");
            for (int i = 0; i < 3; i++) _rStars[i].color = i < r.stars ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.45f);
            UIFactory.SetSprite(_rFace, Art.Get(Art.FaceIcon(r.satisfaction)));
            UIFactory.SetText(_rSat, $"Satisfaction {Mathf.RoundToInt(r.satisfaction)}%");
            UIFactory.SetText(_rReward[0], "+" + r.coins);
            UIFactory.SetText(_rReward[1], "+" + r.energy);
            UIFactory.SetText(_rReward[2], "+" + r.resortValue);

            int row = 0;
            for (int n = 0; n < Needs.Count; n++)
            {
                var st = n < r.stats.Count ? r.stats[n] : null;
                bool show = st != null && st.requested > 0;
                if (!show) continue;
                bool good = st.AvgScore >= 0.8f;
                float y = -352 - row * 42;
                Place(_rLineIcon[n], y); Place(_rLineName[n], y); Place(_rLineCount[n], y); Place(_rLineState[n], y);
                UIFactory.SetSprite(_rLineIcon[n], Art.Get(Art.NeedIcon(n)));
                UIFactory.SetText(_rLineName[n], M.Cfg.Need(n).displayName);
                UIFactory.SetText(_rLineCount[n], $"{st.served}/{st.requested}");
                UIFactory.SetText(_rLineState[n], good ? "worked well" : "needs improvement");
                _rLineState[n].color = good ? UIFactory.Green : UIFactory.Red;
                row++;
            }
            for (int n = 0; n < Needs.Count; n++)
            {
                var st = n < r.stats.Count ? r.stats[n] : null;
                bool show = st != null && st.requested > 0;
                UIFactory.SetActive(_rLineIcon[n], show); UIFactory.SetActive(_rLineName[n], show);
                UIFactory.SetActive(_rLineCount[n], show); UIFactory.SetActive(_rLineState[n], show);
            }
        }

        static void Place(Graphic g, float y)
        {
            var p = g.rectTransform.anchoredPosition;
            if (!Mathf.Approximately(p.y, y)) g.rectTransform.anchoredPosition = new Vector2(p.x, y);
        }

        void RefreshSettings()
        {
            var st = M.S.settings;
            bool[] on = { st.music, st.sound, st.vibration };
            string[][] icons =
            {
                new[] { "UI/Icons/icon_music", "UI/Icons/icon_music_off" },
                new[] { "UI/Icons/icon_sound", "UI/Icons/icon_sound_off" },
                new[] { "UI/Icons/icon_energy", "UI/Icons/icon_energy" },
            };
            for (int i = 0; i < 3; i++)
            {
                UIFactory.SetSprite(_setIcon[i], Art.First(icons[i][on[i] ? 0 : 1], "UI/Icons/icon_resort_value"));
                _setIcon[i].color = on[i] || i < 2 ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                UIFactory.SetText(_setState[i], on[i] ? "ON" : "OFF");
                _setPill[i].color = on[i] ? UIFactory.Green : new Color(0.55f, 0.5f, 0.45f);
            }
            int waves = 0;
            foreach (var isl in M.S.islands) waves += isl.completedWaves;
            UIFactory.SetText(_setStats, $"Waves {waves}   ·   Stars {M.S.totalStars}   ·   Resort value {M.S.resortValue}");
        }

        void RefreshMap()
        {
            for (int i = 0; i < _mapStatus.Length && i < M.S.islands.Count; i++)
            {
                if (_mapStatus[i] == null) continue;
                var isl = M.S.islands[i];
                var ic = M.Cfg.Island(i);
                string status, label;
                bool interactable;
                if (i == M.S.current)
                {
                    status = $"You are here  ·  wave {isl.waveIndex + 1}";
                    label = "HERE"; interactable = false;
                }
                else if (isl.purchased)
                {
                    bool can = M.CanTravel(i);
                    status = can ? $"Wave {isl.waveIndex + 1}  ·  {isl.completedWaves} waves done" : "Finish the current wave first";
                    label = "TRAVEL"; interactable = can;
                }
                else if (M.IslandAvailable(i))
                {
                    status = $"For sale!  x{ic.coinMultiplier.ToString("0.#", Inv)} coins  ·  VIP guests";
                    label = $"BUY  {ic.unlockCost}"; interactable = M.CanBuyIsland(i) && M.Phase == GamePhase.AwaitingWave;
                }
                else
                {
                    status = $"Finish {ic.unlockAfterWaves} waves on {M.Cfg.Island(0).name}  ({M.S.islands[0].completedWaves}/{ic.unlockAfterWaves})";
                    label = "LOCKED"; interactable = false;
                }
                UIFactory.SetText(_mapStatus[i], status);
                UIFactory.SetText(_mapButtonLabel[i], label);
                _mapButton[i].interactable = interactable;
            }
            var win = M.Cfg.Island(M.Cfg.winIsland);
            UIFactory.SetText(_mapGoal, M.S.won ? "Goal complete: you are a Resort Master!"
                : $"Goal: finish {M.Cfg.winWaves} waves on {win.name}  ({M.GoalProgress}/{M.Cfg.winWaves})");
        }
    }
}
