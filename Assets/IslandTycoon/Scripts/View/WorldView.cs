using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IslandTycoon
{
    /// <summary>Positions on the current island, measured in background pixels (100 px per unit).</summary>
    public static class Layout
    {
        public static float BgW = 16.72f, BgH = 9.41f;
        static Vector2 _px = new Vector2(1672, 941);
        static IslandConfig _ic;
        static readonly Dictionary<int, PlotConfig> Plots = new Dictionary<int, PlotConfig>();

        public static void Configure(IslandConfig ic, Vector2 bgPixels)
        {
            _ic = ic;
            _px = bgPixels;
            BgW = bgPixels.x / 100f;
            BgH = bgPixels.y / 100f;
            Plots.Clear();
            foreach (var p in ic.plots) Plots[(int)p.need] = p;
        }

        public static Vector2 Px(float x, float y) => new Vector2((x - _px.x * 0.5f) / 100f, (_px.y * 0.5f - y) / 100f);
        public static Vector2 Px(Vector2 p) => Px(p.x, p.y);

        public static bool HasPlot(int need) => Plots.ContainsKey(need);

        /// <summary>Bottom corner of the iso plot: buildings stand on it.</summary>
        public static Vector2 PlotBase(int need)
        {
            var p = Plots[need];
            return Px(p.pixel.x, p.pixel.y + p.width * 0.25f);
        }

        public static float PlotWidth(int need) => Plots[need].width / 100f;
        public static Vector2 Spawn => Px(_ic.spawnPixel);

        public static Vector2 FreeWorkerSpot(int i) =>
            Px(_ic.freeWorkersPixel.x + (i % 4) * 45, _ic.freeWorkersPixel.y + (i / 4) * 30 + (i % 2) * 10);

        public static Vector2 Front(int need) => PlotBase(need) + new Vector2(0, -0.12f);
        public static Vector2 QueueDir(int need) => (Spawn - Front(need)).normalized;

        public static Vector2 QueueSlot(int need, int k)
        {
            var dir = QueueDir(need);
            var side = new Vector2(-dir.y, dir.x);
            return Front(need) + dir * (0.6f + 0.5f * k) + side * ((k % 2 == 0) ? 0.1f : -0.1f);
        }

        public static Vector2 ServiceSlot(int need, int k, int capacity)
        {
            var dir = QueueDir(need);
            var side = new Vector2(-dir.y, dir.x);
            return Front(need) + dir * 0.15f + side * ((k - (capacity - 1) * 0.5f) * 0.55f);
        }

        public static Vector2 WorkerSpot(int need, int i)
        {
            float w = PlotWidth(need);
            return PlotBase(need) + new Vector2(-w * 0.36f - i * 0.38f, 0.22f - i * 0.04f);
        }

        public static int Order(float y, int bias = 0) => 5000 - Mathf.RoundToInt(y * 100f) + bias;
    }

    /// <summary>Drag to pan, wheel / pinch to zoom; forwarded from anything on the island.</summary>
    public abstract class IslandTouch : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public GameController Controller;
        public void OnBeginDrag(PointerEventData e) => Controller.Rig.Begin(e);
        public void OnDrag(PointerEventData e) => Controller.Rig.Drag(e);
        public void OnEndDrag(PointerEventData e) => Controller.Rig.End(e);
        public void OnScroll(PointerEventData e) => Controller.Rig.Scroll(e);
    }

    /// <summary>Clickable building plot.</summary>
    public class PlotView : IslandTouch, IPointerClickHandler
    {
        public int Need;
        public SpriteRenderer Body, Warning, Sparkle, LockCoin, Badge;
        public TextMesh LevelText;
        public readonly List<Transform> Workers = new List<Transform>();
        public int ShownLevel = -1;
        public bool ShownUnlocked;
        public float Punch;
        public float Top = 2f;

        public void OnPointerClick(PointerEventData e) => Controller.SelectBuilding(Need);
    }

    /// <summary>Tap on empty island closes panels.</summary>
    public class BackgroundClick : IslandTouch, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e) => Controller.SelectBuilding(-1);
    }

    class GuestView
    {
        public Transform Root, Bob;
        public SpriteRenderer Body, Bubble, Icon, Crown;
        public int ShownNeed = -2;
    }

    /// <summary>Renders the current island. Holds no game rules.</summary>
    public class WorldView : MonoBehaviour
    {
        GameController _c;
        GameModel M => _c.Model;
        readonly Dictionary<int, PlotView> _plots = new Dictionary<int, PlotView>();
        readonly Dictionary<int, GuestView> _guests = new Dictionary<int, GuestView>();
        readonly List<Transform> _freeWorkers = new List<Transform>();
        Transform _content, _guestRoot, _boat;
        int _builtIsland = -1;

        const float GuestHeight = 1.25f;
        const float WalkSpeed = 4.2f;

        public void Build(GameController c)
        {
            _c = c;
            Rebuild();
        }

        /// <summary>(Re)creates everything for the island the player is on.</summary>
        public void Rebuild()
        {
            if (_content != null) Destroy(_content.gameObject);
            _plots.Clear();
            _guests.Clear();
            _freeWorkers.Clear();

            _content = new GameObject("Island_" + M.S.current).transform;
            _content.SetParent(transform, false);
            _builtIsland = M.S.current;

            var ic = M.IC;
            var bgSprite = Art.TryGet(ic.background);
            bool fallback = bgSprite == null;
            if (fallback) bgSprite = Art.Get(ic.fallbackBackground);
            var size = bgSprite != null ? bgSprite.rect.size : ic.backgroundPixels;
            Layout.Configure(ic, size);

            var bg = Art.MakeSprite("Background", _content, null, -10000);
            bg.sprite = bgSprite;
            if (fallback) bg.color = ic.fallbackTint;
            Art.FitCenter(bg, Layout.BgH);
            bg.gameObject.AddComponent<BoxCollider2D>();
            bg.gameObject.AddComponent<BackgroundClick>().Controller = _c;

            // Sea around the art, visible when the camera pans or zooms out. Nothing is flipped upside down:
            // left/right = horizontal mirror of the island; top/bottom = open-water strips generated from the
            // art's own edge colours (sea_top_/sea_bottom_<background>), mirrored sideways for the corners.
            string key = System.IO.Path.GetFileName(fallback ? ic.fallbackBackground : ic.background);
            var top = Art.TryGet("Backgrounds/sea_top_" + key);
            var bottom = Art.TryGet("Backgrounds/sea_bottom_" + key);
            float k = bg.transform.localScale.x;
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx != 0) AddSea(bgSprite, true, new Vector2(dx * Layout.BgW, 0), k, bg.color);
                if (top != null) AddSea(top, dx != 0, new Vector2(dx * Layout.BgW, (Layout.BgH + top.rect.height / 100f * k) * 0.5f), k, bg.color);
                if (bottom != null) AddSea(bottom, dx != 0, new Vector2(dx * Layout.BgW, -(Layout.BgH + bottom.rect.height / 100f * k) * 0.5f), k, bg.color);
            }
            _c.Rig.Reset();

            for (int n = 0; n < Needs.Count; n++)
                if (Layout.HasPlot(n) && M.Exists(n)) _plots[n] = BuildPlot(n);

            _guestRoot = new GameObject("Guests").transform;
            _guestRoot.SetParent(_content, false);

            var boatSprite = Art.TryGet("Characters/boat");
            _boat = null;
            if (boatSprite != null)
            {
                _boat = new GameObject("Boat").transform;
                _boat.SetParent(_content, false);
                var b = Art.MakeSprite("Body", _boat, null, Layout.Order(Layout.Spawn.y - 0.6f));
                b.sprite = boatSprite;
                Art.FitFeet(b, 1.3f);
            }
        }

        void AddSea(Sprite sprite, bool mirror, Vector2 center, float scale, Color tint)
        {
            var m = Art.MakeSprite("Sea", _content, null, -10001);
            m.sprite = sprite;
            m.flipX = mirror;
            m.color = tint;
            var b = sprite.bounds;
            m.transform.localScale = new Vector3(scale, scale, 1);
            m.transform.localPosition = (Vector3)center - new Vector3(b.center.x * scale * (mirror ? -1 : 1), b.center.y * scale, 0);
            m.gameObject.AddComponent<BoxCollider2D>();
            m.gameObject.AddComponent<BackgroundClick>().Controller = _c;
        }

        PlotView BuildPlot(int need)
        {
            var root = new GameObject("Plot_" + (NeedType)need);
            root.transform.SetParent(_content, false);
            root.transform.localPosition = Layout.PlotBase(need);
            var pv = root.AddComponent<PlotView>();
            pv.Need = need;
            pv.Controller = _c;
            int order = Layout.Order(Layout.PlotBase(need).y);

            pv.Body = Art.MakeSprite("Body", root.transform, null, order);
            pv.Warning = Art.MakeSprite("Warning", root.transform, "UI/Icons/icon_warning", 9000);
            pv.Sparkle = Art.MakeSprite("Boost", root.transform, "FX/fx_sparkle", 8999);
            pv.LockCoin = Art.MakeSprite("UnlockCoin", root.transform, "UI/Icons/icon_coin", 9000);
            Art.FitCenter(pv.LockCoin, 0.6f);
            Art.FitCenter(pv.Sparkle, 0.9f);

            pv.Badge = Art.MakeSprite("LevelBadge", root.transform, "UI/Kit/badge_round", order + 2);
            Art.FitCenter(pv.Badge, 0.75f);
            var tgo = new GameObject("LevelText");
            tgo.transform.SetParent(root.transform, false);
            pv.LevelText = tgo.AddComponent<TextMesh>();
            pv.LevelText.font = Art.Font;
            pv.LevelText.fontSize = 100;
            pv.LevelText.characterSize = 0.03f;
            pv.LevelText.anchor = TextAnchor.MiddleCenter;
            pv.LevelText.alignment = TextAlignment.Center;
            pv.LevelText.fontStyle = FontStyle.Bold;
            pv.LevelText.color = new Color(0.3f, 0.15f, 0.05f);
            var mr = tgo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Art.Font.material;
            mr.sortingOrder = order + 3;

            RefreshPlotArt(pv, true);
            return pv;
        }

        void RefreshPlotArt(PlotView pv, bool force)
        {
            var b = M.B(pv.Need);
            if (!force && pv.ShownLevel == b.level && pv.ShownUnlocked == b.unlocked) return;
            bool changed = pv.ShownLevel >= 0 && (pv.ShownLevel != b.level || pv.ShownUnlocked != b.unlocked);
            pv.ShownLevel = b.level;
            pv.ShownUnlocked = b.unlocked;

            var plot = M.Plot(pv.Need);
            pv.Body.sprite = Art.Building(M.Cfg, pv.Need, b.level, b.unlocked, plot != null && plot.jungleLock, out bool levelArt);
            float grow = levelArt ? 1f : 1f + 0.07f * (b.level - 1);
            float width = Layout.PlotWidth(pv.Need) * 1.08f * grow;
            Art.FitBottom(pv.Body, width, -0.06f);
            var old = pv.Body.GetComponent<BoxCollider2D>();
            if (old != null) Destroy(old);
            pv.Body.gameObject.AddComponent<BoxCollider2D>();

            pv.Top = pv.Body.sprite != null ? pv.Body.sprite.bounds.size.y * pv.Body.transform.localScale.y : 2f;
            pv.Sparkle.transform.localPosition = new Vector3(0.5f, pv.Top * 0.8f, 0);
            pv.Badge.transform.localPosition = new Vector3(width * 0.4f, 0.45f, 0);
            // the round face of badge_round sits above the sprite centre (flower below): measured offset
            pv.LevelText.transform.localPosition = pv.Badge.transform.localPosition + new Vector3(0.007f, 0.089f, 0);
            pv.LevelText.text = b.level.ToString();

            if (changed)
            {
                pv.Punch = 1f;
                _c.Fx("FX/fx_puff", (Vector2)pv.transform.position + new Vector2(0, pv.Top * 0.4f), 1.6f, 0.2f);
                _c.Fx("FX/fx_sparkle", (Vector2)pv.transform.position + new Vector2(0, pv.Top * 0.7f), 1.0f, 0.6f);
            }
        }

        void LateUpdate()
        {
            if (_c == null || M == null) return;
            if (_builtIsland != M.S.current) Rebuild();
            float t = Time.time;
            foreach (var pv in _plots.Values) SyncPlot(pv, t);
            SyncFreeWorkers();
            SyncGuests();
            if (_boat != null)
            {
                var p = Layout.Spawn + new Vector2(-0.9f, -0.55f);
                _boat.localPosition = p + new Vector2(0, Mathf.Sin(t * 1.6f) * 0.04f);
                _boat.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.1f) * 2f);
            }
        }

        void SyncPlot(PlotView pv, float t)
        {
            RefreshPlotArt(pv, false);
            var b = M.B(pv.Need);

            pv.Punch = Mathf.MoveTowards(pv.Punch, 0, Time.deltaTime * 2.5f);
            float p = 1f + Mathf.Sin(pv.Punch * Mathf.PI) * 0.12f;
            float sel = _c.Selected == pv.Need ? 1f + Mathf.Sin(t * 5f) * 0.015f : 1f;
            pv.transform.localScale = new Vector3(p * sel, p * sel, 1);
            pv.Body.color = b.unlocked || M.IsUnlockAvailable(pv.Need) ? Color.white : new Color(0.85f, 0.85f, 0.85f);

            bool bottleneck = M.IsBottleneck(pv.Need);
            pv.Warning.gameObject.SetActive(bottleneck);
            if (bottleneck && pv.Warning.sprite != null)
            {
                float k = 0.7f / Mathf.Max(0.01f, pv.Warning.sprite.bounds.size.y) * (1f + Mathf.Sin(t * 8f) * 0.1f);
                pv.Warning.transform.localScale = new Vector3(k, k, 1);
                pv.Warning.transform.localPosition = new Vector3(0, pv.Top + 0.3f, 0);
            }

            bool boost = b.boostLeft > 0;
            pv.Sparkle.gameObject.SetActive(boost);
            if (boost) pv.Sparkle.transform.localRotation = Quaternion.Euler(0, 0, t * 90f);

            bool lockCoin = M.IsUnlockAvailable(pv.Need);
            pv.LockCoin.gameObject.SetActive(lockCoin);
            if (lockCoin) pv.LockCoin.transform.localPosition = new Vector3(0, pv.Top * 0.75f + Mathf.Sin(t * 3f) * 0.08f, 0);

            pv.Badge.gameObject.SetActive(b.unlocked);
            pv.LevelText.gameObject.SetActive(b.unlocked);

            // assigned workers stand in front of their building
            int want = b.unlocked ? b.workers : 0;
            while (pv.Workers.Count < want)
            {
                var holder = new GameObject("Worker").transform;
                holder.SetParent(_content, false);
                holder.localPosition = Layout.WorkerSpot(pv.Need, pv.Workers.Count);
                var w = Art.MakeSprite("Body", holder, "Characters/Worker/worker_front", 0);
                Art.FitFeet(w, GuestHeight);
                pv.Workers.Add(holder);
                _c.Fx("FX/fx_puff", holder.position + Vector3.up * 0.4f, 0.7f, 0.1f);
            }
            while (pv.Workers.Count > want)
            {
                var h = pv.Workers[pv.Workers.Count - 1];
                pv.Workers.RemoveAt(pv.Workers.Count - 1);
                _c.Fx("FX/fx_puff", h.position + Vector3.up * 0.4f, 0.7f, 0.1f);
                Destroy(h.gameObject);
            }
            bool busy = M.InServiceCount(pv.Need) > 0;
            var spr = Art.Get(busy ? "Characters/Worker/worker_tray" : "Characters/Worker/worker_front");
            int baseOrder = Layout.Order(Layout.PlotBase(pv.Need).y);
            for (int i = 0; i < pv.Workers.Count; i++)
            {
                var h = pv.Workers[i];
                var spot = Layout.WorkerSpot(pv.Need, i);
                h.localPosition = spot + new Vector2(0, busy ? Mathf.Abs(Mathf.Sin(t * 9f + i)) * 0.05f : 0);
                var w = h.GetComponentInChildren<SpriteRenderer>();
                if (w.sprite != spr) { w.sprite = spr; Art.FitFeet(w, GuestHeight); }
                w.sortingOrder = baseOrder + 5 + i;
            }
        }

        void SyncFreeWorkers()
        {
            int want = Mathf.Max(0, M.FreeWorkers);
            while (_freeWorkers.Count < want)
            {
                var holder = new GameObject("FreeWorker").transform;
                holder.SetParent(_content, false);
                var spot = Layout.FreeWorkerSpot(_freeWorkers.Count);
                holder.localPosition = spot;
                var w = Art.MakeSprite("Body", holder, "Characters/Worker/worker_wave", Layout.Order(spot.y, 1));
                Art.FitFeet(w, GuestHeight);
                _freeWorkers.Add(holder);
                _c.Fx("FX/fx_puff", holder.position + Vector3.up * 0.4f, 0.7f, 0.1f);
            }
            while (_freeWorkers.Count > want)
            {
                var h = _freeWorkers[_freeWorkers.Count - 1];
                _freeWorkers.RemoveAt(_freeWorkers.Count - 1);
                Destroy(h.gameObject);
            }
            float t = Time.time;
            for (int i = 0; i < _freeWorkers.Count; i++)
                _freeWorkers[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2f + i) * 3f);
        }

        void SyncGuests()
        {
            var alive = new HashSet<int>();
            var queueIndex = new int[Needs.Count];
            var serviceIndex = new int[Needs.Count];
            float dt = Time.deltaTime;

            foreach (var g in M.I.guests)
            {
                alive.Add(g.id);
                if (!_guests.TryGetValue(g.id, out var v)) { v = MakeGuest(g); _guests[g.id] = v; }

                int need = g.CurrentNeed;
                Vector2 target;
                if (need < 0 || !Layout.HasPlot(need)) target = Layout.Spawn;
                else switch (g.stage)
                {
                    case GuestStage.Walking: target = Layout.QueueSlot(need, M.QueueCount(need)); break;
                    case GuestStage.Queued: target = Layout.QueueSlot(need, queueIndex[need]++); break;
                    case GuestStage.InService: target = Layout.ServiceSlot(need, serviceIndex[need]++, M.Capacity(need)); break;
                    default: target = Layout.Spawn; break;
                }

                var pos = (Vector2)v.Root.localPosition;
                var next = Vector2.MoveTowards(pos, target, WalkSpeed * dt);
                bool moving = (next - pos).sqrMagnitude > 0.000001f;
                if (moving && Mathf.Abs(next.x - pos.x) > 0.001f) v.Body.flipX = next.x < pos.x;
                v.Root.localPosition = next;
                v.Bob.localPosition = new Vector3(0, moving ? Mathf.Abs(Mathf.Sin(Time.time * 12f + g.id)) * 0.06f : 0, 0);
                int order = Layout.Order(next.y, 2);
                v.Body.sortingOrder = order;
                if (v.Crown != null) v.Crown.sortingOrder = order + 1;

                bool showBubble = need >= 0 && (g.stage == GuestStage.Walking || g.stage == GuestStage.Queued);
                v.Bubble.gameObject.SetActive(showBubble);
                if (showBubble)
                {
                    if (v.ShownNeed != need)
                    {
                        v.Icon.sprite = Art.Get(Art.NeedIcon(need));
                        Art.FitCenter(v.Icon, 0.44f);
                        v.Icon.transform.localPosition += new Vector3(0, 0.03f, 0);
                        v.ShownNeed = need;
                    }
                    float wait = g.stage == GuestStage.Queued ? Mathf.Clamp01(g.waited / M.Patience(g)) : 0;
                    v.Bubble.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), wait);
                }
                v.Body.color = g.angry ? new Color(1f, 0.75f, 0.75f) : Color.white;
            }

            List<int> dead = null;
            foreach (var kv in _guests)
                if (!alive.Contains(kv.Key)) { if (dead == null) dead = new List<int>(); dead.Add(kv.Key); }
            if (dead != null)
                foreach (var id in dead) { Destroy(_guests[id].Root.gameObject); _guests.Remove(id); }
        }

        GuestView MakeGuest(Guest g)
        {
            var v = new GuestView();
            v.Root = new GameObject("Guest_" + g.id).transform;
            v.Root.SetParent(_guestRoot, false);
            v.Root.localPosition = Layout.Spawn;
            v.Bob = new GameObject("Bob").transform;
            v.Bob.SetParent(v.Root, false);
            v.Body = Art.MakeSprite("Body", v.Bob, null, 0);
            v.Body.sprite = Art.GuestSprite(g.skin, g.vip);
            Art.FitFeet(v.Body, GuestHeight * (g.vip ? 1.05f : 1f));

            if (g.vip && Art.TryGet("Characters/VIP/vip_01") == null)
            {
                // no VIP art yet: mark VIPs with a small crown icon
                var crownRoot = new GameObject("Crown").transform;
                crownRoot.SetParent(v.Bob, false);
                crownRoot.localPosition = new Vector3(0, GuestHeight + 0.05f, 0);
                v.Crown = Art.MakeSprite("Icon", crownRoot, null, 0);
                v.Crown.sprite = Art.First("UI/Icons/icon_vip", "UI/Icons/icon_resort_value");
                Art.FitCenter(v.Crown, 0.3f);
            }

            var bubbleRoot = new GameObject("BubbleRoot").transform;
            bubbleRoot.SetParent(v.Root, false);
            bubbleRoot.localPosition = new Vector3(0.25f, GuestHeight + 0.3f, 0);
            v.Bubble = Art.MakeSprite("Bubble", bubbleRoot, "FX/bubble_thought", 9500);
            Art.FitCenter(v.Bubble, 0.7f);
            var iconRoot = new GameObject("IconRoot").transform;
            iconRoot.SetParent(bubbleRoot, false);
            v.Icon = Art.MakeSprite("Icon", iconRoot, Art.NeedIcon(Mathf.Max(0, g.CurrentNeed)), 9501);
            Art.FitCenter(v.Icon, 0.44f);
            return v;
        }

        public Vector3 GuestPosition(int id) =>
            _guests.TryGetValue(id, out var v) ? v.Root.position : (Vector3)Layout.Spawn;

        public Vector3 PlotTop(int need)
        {
            if (!_plots.TryGetValue(need, out var pv)) return Vector3.zero;
            return pv.transform.position + new Vector3(0, pv.Top * 0.6f, 0);
        }
    }
}
