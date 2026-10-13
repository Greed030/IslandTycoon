using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>Small helpers that build uGUI elements from code.</summary>
    public static class UIFactory
    {
        public static readonly Color Brown = new Color(0.30f, 0.17f, 0.07f);
        public static readonly Color DarkPill = new Color(0.16f, 0.10f, 0.05f, 0.80f);
        public static readonly Color Cream = new Color(1f, 0.97f, 0.88f);
        public static readonly Color Wood = new Color(0.55f, 0.33f, 0.14f);
        public static readonly Color WoodDark = new Color(0.36f, 0.20f, 0.07f);
        public static readonly Color Paper = new Color(1f, 0.95f, 0.83f);
        public static readonly Color Green = new Color(0.12f, 0.55f, 0.23f);
        public static readonly Color Red = new Color(0.75f, 0.22f, 0.17f);

        static Sprite _rounded;

        /// <summary>Procedural rounded rectangle (9-sliced) for pills, rows and frames.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 64, r = 24;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (size - r)));
                    float dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (size - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _rounded;
            }
        }

        public static Canvas Canvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>RectTransform whose pivot equals its anchor, so pos is measured from that edge/corner.</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>RectTransform positioned by its centre relative to the parent's anchor point.</summary>
        public static RectTransform Centered(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size) =>
            Rect(name, parent, anchor, pos, size, new Vector2(0.5f, 0.5f));

        public static void Stretch(RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size, bool raycast = false)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = sprite != null;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Image centred on a point.</summary>
        public static Image Icon(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, float size)
        {
            var img = Image(name, parent, sprite, anchor, pos, new Vector2(size, size));
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        /// <summary>
        /// Crisp wooden frame with a paper interior, built from the rounded sprite so it stays
        /// sharp at any size. Uses UI/Kit/panel_big (9-sliced) instead when that art exists.
        /// Returns the content area inside the frame.
        /// </summary>
        public static RectTransform Frame(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, out RectTransform outer, float border = 115f)
        {
            outer = Rect(name, parent, anchor, pos, size);
            var art = Art.TryGet("UI/Kit/panel_big");
            var img = outer.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            RectTransform content;
            if (art != null && art.border.sqrMagnitude > 0)
            {
                // the frame art has 300 px corners; scale them down to the requested border size
                float k = Mathf.Max(1f, art.border.w / Mathf.Min(border, Mathf.Min(size.y * 0.45f, size.x * 0.3f)));
                img.sprite = art;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = k;
                content = Rect("Content", outer, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Stretch(content, 170f / k + 14f);
                return content;
            }
            img.sprite = Rounded;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = WoodDark;
            var shadow = outer.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.35f);
            shadow.effectDistance = new Vector2(0, -10);
            AddLayer(outer, "Wood", 7, Wood);
            AddLayer(outer, "Edge", 20, WoodDark);
            AddLayer(outer, "Paper", 24, Paper);
            content = Rect("Content", outer, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(content, 44);
            return content;
        }

        static void AddLayer(RectTransform parent, string name, float inset, Color color)
        {
            var r = Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(r, inset);
            var i = r.gameObject.AddComponent<Image>();
            i.sprite = Rounded; i.type = UnityEngine.UI.Image.Type.Sliced; i.color = color; i.raycastTarget = false;
        }

        /// <summary>
        /// Wooden capsule from the UI kit (same art as the progress bar). The left cap carries a flower
        /// about flowerPx wide; inner is the dark area where text goes.
        /// </summary>
        public static Image WoodPill(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float flowerPx, out RectTransform inner)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var art = Art.TryGet("UI/Kit/bar_empty");
            inner = Rect("Inner", rt, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(inner);
            if (art != null && art.border.sqrMagnitude > 0)
            {
                float k = Mathf.Max(1f, Mathf.Max((art.border.y + art.border.w) / (size.y * 0.9f), art.border.x / flowerPx));
                img.sprite = art;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = k;
                // dark interior of the art: from the end of the flower cap to the right rim
                // measured dark interior of bar_empty: x 187..901, y 60..118 (of 956 x 162)
                inner.offsetMin = new Vector2(187f / k + 6, 44f / k);
                inner.offsetMax = new Vector2(-55f / k - 6, -60f / k);
            }
            else
            {
                img.sprite = Rounded;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.color = new Color(0.36f, 0.20f, 0.07f, 0.92f);
                inner.offsetMin = new Vector2(16, 8);
                inner.offsetMax = new Vector2(-16, -8);
            }
            return img;
        }

        /// <summary>Orange title ribbon sitting on the top edge of a frame.</summary>
        public static Text Ribbon(Transform frameOuter, string title, float width)
        {
            var r = Centered("Ribbon", frameOuter, new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(width, 96));
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = Rounded; img.type = UnityEngine.UI.Image.Type.Sliced; img.color = WoodDark; img.raycastTarget = false;
            var inner = Rect("Inner", r, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(inner, 7);
            var ii = inner.gameObject.AddComponent<Image>();
            ii.sprite = Rounded; ii.type = UnityEngine.UI.Image.Type.Sliced; ii.color = new Color(0.95f, 0.52f, 0.13f); ii.raycastTarget = false;
            return Label("Title", r, title, 46, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(width - 40, 80), TextAnchor.MiddleCenter, true);
        }

        public static Text Text(string name, Transform parent, string text, int size, Color color, TextAnchor align,
            Vector2 anchor, Vector2 pos, Vector2 box, bool outline = false)
        {
            var rt = Rect(name, parent, anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Art.Font;
            t.fontSize = size;
            t.fontStyle = Art.Font != null && Art.Font.name.Contains("Fredoka") ? FontStyle.Normal : FontStyle.Bold;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.lineSpacing = 1.05f;
            // shrink instead of spilling out of the box
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = size;
            t.resizeTextMinSize = Mathf.Max(14, Mathf.RoundToInt(size * 0.55f));
            if (outline)
            {
                // soft warm drop shadow instead of a hard black outline
                var o = rt.gameObject.AddComponent<Shadow>();
                o.effectColor = new Color(0.32f, 0.16f, 0.04f, 0.55f);
                o.effectDistance = new Vector2(0, -3f);
            }
            return t;
        }

        /// <summary>Text box centred on a point: the most predictable placement.</summary>
        public static Text Label(string name, Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box,
            TextAnchor align = TextAnchor.MiddleCenter, bool outline = false)
        {
            var t = Text(name, parent, text, size, color, align, anchor, pos, box, outline);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return t;
        }

        /// <summary>Text area inside each kit button (fractions of the sprite: left, bottom, right, top), measured from the art.</summary>
        static Vector4 GlassArea(string spritePath)
        {
            // each area is symmetric around the measured centre of the glass, so labels sit dead centre
            if (spritePath.Contains("btn_primary")) return new Vector4(0.25f, 0.30f, 0.786f, 0.724f);   // glass centre (0.518, 0.512)
            if (spritePath.Contains("btn_secondary")) return new Vector4(0.25f, 0.28f, 0.778f, 0.71f);  // glass centre (0.514, 0.495)
            if (spritePath.Contains("btn_square")) return new Vector4(0.40f, 0.40f, 0.72f, 0.72f);      // wood centre (0.56, 0.56)
            return new Vector4(0.08f, 0.15f, 0.92f, 0.85f);
        }

        /// <summary>Text that fills a rect and shrinks to fit (never spills out).</summary>
        public static Text FitText(string name, Transform parent, string text, int maxSize, Color color, TextAnchor align,
            Vector2 anchorMin, Vector2 anchorMax, bool shadow = false, int minSize = 14)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = Art.Font;
            t.fontStyle = Art.Font != null && Art.Font.name.Contains("Fredoka") ? FontStyle.Normal : FontStyle.Bold;
            t.fontSize = maxSize;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = maxSize;
            t.resizeTextMinSize = minSize;
            t.lineSpacing = 0.95f;
            if (shadow)
            {
                var o = go.AddComponent<Shadow>();
                o.effectColor = new Color(0.32f, 0.16f, 0.04f, 0.55f);
                o.effectDistance = new Vector2(0, -2.5f);
            }
            return t;
        }

        /// <summary>
        /// Kit button. Only the width is used: the height follows the art so it is never stretched.
        /// The label always stays inside the glass part of the button.
        /// </summary>
        public static Button Button(string name, Transform parent, string spritePath, string label, int fontSize,
            Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, bool centered = false)
        {
            var sprite = Art.Get(spritePath);
            if (sprite != null) size.y = size.x * sprite.rect.height / sprite.rect.width;
            var rt = Rect(name, parent, anchor, pos, size, centered ? new Vector2(0.5f, 0.5f) : (Vector2?)null);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.9f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            b.colors = cb;
            b.onClick.AddListener(() => onClick?.Invoke());
            rt.gameObject.AddComponent<ButtonPop>();
            if (!string.IsNullOrEmpty(label))
            {
                var g = GlassArea(spritePath);
                FitText("Label", rt, label, fontSize, Color.white, TextAnchor.MiddleCenter, new Vector2(g.x, g.y), new Vector2(g.z, g.w), true);
            }
            return b;
        }

        /// <summary>Square wooden button with a drawn plus or minus sign, centred on the wood.</summary>
        public static Button SignButton(string name, Transform parent, bool plus, Vector2 anchor, Vector2 pos, float width, Action onClick)
        {
            var b = Button(name, parent, "UI/Kit/btn_square", null, 0, anchor, pos, new Vector2(width, 0), onClick, true);
            var g = GlassArea("btn_square");
            var area = Rect("Sign", b.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            area.anchorMin = new Vector2(g.x, g.y);
            area.anchorMax = new Vector2(g.z, g.w);
            area.offsetMin = area.offsetMax = Vector2.zero;
            float len = width * 0.34f, thick = width * 0.1f;
            for (int i = 0; i < (plus ? 2 : 1); i++)
            {
                var bar = Centered("Bar" + i, area, new Vector2(0.5f, 0.5f), Vector2.zero, i == 0 ? new Vector2(len, thick) : new Vector2(thick, len));
                var img = bar.gameObject.AddComponent<Image>();
                img.sprite = Rounded; img.type = UnityEngine.UI.Image.Type.Sliced; img.pixelsPerUnitMultiplier = 4f;
                img.color = Color.white; img.raycastTarget = false;
                var sh = bar.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0.32f, 0.16f, 0.04f, 0.6f);
                sh.effectDistance = new Vector2(0, -2.5f);
            }
            return b;
        }

        /// <summary>Round icon button (the icon art has its own frame), or a square wooden button with text.</summary>
        public static Button IconButton(string name, Transform parent, string iconPath, string fallback,
            Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, bool centered = false)
        {
            var icon = Art.TryGet(iconPath);
            if (icon == null) return Button(name, parent, "UI/Kit/btn_square", fallback, 34, anchor, pos, size, onClick, centered);
            var rt = Rect(name, parent, anchor, pos, size, centered ? new Vector2(0.5f, 0.5f) : (Vector2?)null);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
            b.colors = cb;
            b.onClick.AddListener(() => onClick?.Invoke());
            rt.gameObject.AddComponent<ButtonPop>();
            return b;
        }

        public static void SetText(Text t, string s) { if (t != null && t.text != s) t.text = s; }

        public static void SetActive(Component c, bool on) { if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on); }

        public static void SetSprite(Image img, Sprite s) { if (img != null && img.sprite != s) img.sprite = s; }
    }

    /// <summary>Gentle idle motion: x = scale pulse, y = vertical bob (px), z = period (s).</summary>
    public class Drift : MonoBehaviour
    {
        public Vector3 Amount;
        Vector2 _base;
        RectTransform _rt;
        void Start() { _rt = (RectTransform)transform; _base = _rt.anchoredPosition; }
        void Update()
        {
            if (_rt == null) return;
            float t = Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / Mathf.Max(0.1f, Amount.z));
            _rt.anchoredPosition = _base + new Vector2(0, t * Amount.y);
            float k = 1f + (t * 0.5f + 0.5f) * Amount.x;
            _rt.localScale = new Vector3(k, k, 1);
        }
    }

    /// <summary>Keeps UI inside the safe area (notches, rounded corners).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect _last;
        RectTransform _rt;
        void Awake() { _rt = (RectTransform)transform; Apply(); }
        void Update() { if (_last != Screen.safeArea) Apply(); }
        void Apply()
        {
            var sa = Screen.safeArea;
            _last = sa;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _rt.anchorMin = new Vector2(sa.x / Screen.width, sa.y / Screen.height);
            _rt.anchorMax = new Vector2((sa.x + sa.width) / Screen.width, (sa.y + sa.height) / Screen.height);
            _rt.offsetMin = _rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Tiny press animation and click sound for instant feedback (NFR-08, NFR-10).</summary>
    public class ButtonPop : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float _target = 1f;
        public void OnPointerDown(PointerEventData e)
        {
            _target = 0.92f;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(Sfx.Click);
        }
        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;
        void Update()
        {
            float s = Mathf.MoveTowards(transform.localScale.x, _target, Time.unscaledDeltaTime * 3f);
            transform.localScale = new Vector3(s, s, 1);
        }
    }
}
