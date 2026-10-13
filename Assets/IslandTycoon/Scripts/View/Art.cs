using System.Collections.Generic;
using UnityEngine;

namespace IslandTycoon
{
    /// <summary>Loads sprites from Resources/IslandTycoon/Sprites with a cache and graceful fallbacks.</summary>
    public static class Art
    {
        const string Root = "IslandTycoon/Sprites/";
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static Material _spriteMat;
        static Font _font;

        /// <summary>Returns null without a warning when the sprite does not exist (optional art).</summary>
        public static Sprite TryGet(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Cache.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(Root + path);
            if (s == null)
            {
                // texture imported without the Sprite type: build one at runtime
                var tex = Resources.Load<Texture2D>(Root + path);
                if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            Cache[path] = s;
            return s;
        }

        public static Sprite Get(string path)
        {
            var s = TryGet(path);
            if (s == null) Debug.LogWarning("[IslandTycoon] Missing sprite: " + path);
            return s;
        }

        /// <summary>First existing sprite of the list.</summary>
        public static Sprite First(params string[] paths)
        {
            foreach (var p in paths) { var s = TryGet(p); if (s != null) return s; }
            return null;
        }

        public static string NeedIcon(int need)
        {
            switch ((NeedType)need)
            {
                case NeedType.Rooms: return "UI/Icons/icon_need_rooms";
                case NeedType.Drinks: return "UI/Icons/icon_need_drinks";
                case NeedType.Food: return "UI/Icons/icon_need_food";
                case NeedType.Relax: return "UI/Icons/icon_need_relax";
                case NeedType.Spa: return TryGet("UI/Icons/icon_need_spa") != null ? "UI/Icons/icon_need_spa" : "FX/fx_sparkle";
                default: return "UI/Icons/icon_need_oceanview";
            }
        }

        /// <summary>Building art for a level. hasLevelArt = false means the level 1 picture is reused.</summary>
        public static Sprite Building(GameConfig cfg, int need, int level, bool unlocked, bool jungleLock, out bool hasLevelArt)
        {
            hasLevelArt = true;
            if (!unlocked) return Get(jungleLock ? "Buildings/plot_locked_jungle" : "Buildings/plot_locked_fence");
            string key = cfg.Need(need).spriteKey;
            if (string.IsNullOrEmpty(key)) key = ((NeedType)need).ToString().ToLower();
            var s = TryGet($"Buildings/{key}_lv{Mathf.Clamp(level, 1, 3)}");
            if (s != null) return s;
            hasLevelArt = false;
            return First($"Buildings/{key}_lv1", "Buildings/pool_lv1");
        }

        public static Sprite GuestSprite(int skin, bool vip)
        {
            if (vip)
            {
                var v = TryGet("Characters/VIP/vip_0" + (Mathf.Abs(skin) % 4 + 1));
                if (v != null) return v;
            }
            return Get("Characters/Guests/guest_0" + (Mathf.Abs(skin) % 6 + 1));
        }

        public static string FaceIcon(float satisfaction) =>
            satisfaction >= 75 ? "UI/Icons/icon_face_happy" : satisfaction >= 45 ? "UI/Icons/icon_face_neutral" : "UI/Icons/icon_face_sad";

        /// <summary>Unlit sprite material: works in URP 2D (no lights needed) and Built-in.</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (_spriteMat != null) return _spriteMat;
                var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh != null) _spriteMat = new Material(sh);
                return _spriteMat;
            }
        }

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("IslandTycoon/Fonts/FredokaOne");
                if (_font != null) return _font;
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (_font == null) try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                return _font;
            }
        }

        public static SpriteRenderer MakeSprite(string name, Transform parent, string path, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = string.IsNullOrEmpty(path) ? null : Get(path);
            if (SpriteMaterial != null) sr.sharedMaterial = SpriteMaterial;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Scales a sprite to a world width and puts its bottom edge at local y = 0, independent of the pivot.</summary>
        public static void FitBottom(SpriteRenderer sr, float worldWidth, float yOffset = 0)
        {
            if (sr.sprite == null) return;
            var b = sr.sprite.bounds;
            float k = worldWidth / Mathf.Max(0.01f, b.size.x);
            sr.transform.localScale = new Vector3(k, k, 1);
            sr.transform.localPosition = new Vector3(-b.center.x * k, (-b.min.y) * k + yOffset, 0);
        }

        /// <summary>Scales a sprite so its height is worldHeight and its feet are at local y = 0.</summary>
        public static void FitFeet(SpriteRenderer sr, float worldHeight)
        {
            if (sr.sprite == null) return;
            var b = sr.sprite.bounds;
            float k = worldHeight / Mathf.Max(0.01f, b.size.y);
            sr.transform.localScale = new Vector3(k, k, 1);
            sr.transform.localPosition = new Vector3(-b.center.x * k, -b.min.y * k, 0);
        }

        /// <summary>Scales a sprite to a world height, centred on the parent.</summary>
        public static void FitCenter(SpriteRenderer sr, float worldHeight)
        {
            if (sr.sprite == null) return;
            var b = sr.sprite.bounds;
            float k = worldHeight / Mathf.Max(0.01f, b.size.y);
            sr.transform.localScale = new Vector3(k, k, 1);
            sr.transform.localPosition = new Vector3(-b.center.x * k, -b.center.y * k, 0);
        }
    }
}
