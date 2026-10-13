using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IslandTycoon
{
    /// <summary>
    /// Island camera: drag to pan, mouse wheel or two-finger pinch to zoom.
    /// Extra room at the top lets the player look under the HUD.
    /// </summary>
    public class CameraRig
    {
        const float SideMargin = 1.2f, BottomMargin = 1.2f, TopMargin = 2.6f;
        readonly Camera _cam;
        readonly Dictionary<int, Vector2> _pointers = new Dictionary<int, Vector2>();
        Vector2 _center;
        float _size = -1;

        public CameraRig(Camera cam) { _cam = cam; }

        float Aspect => Mathf.Max(0.1f, _cam.aspect);
        /// <summary>Zoom that fills the screen with the island art.</summary>
        float CoverSize => Mathf.Min(Layout.BgH * 0.5f, Layout.BgW * 0.5f / Aspect);
        float MaxSize => Mathf.Max(CoverSize, Mathf.Min((Layout.BgH + TopMargin + BottomMargin) * 0.5f,
                                                          (Layout.BgW + 2 * SideMargin) * 0.5f / Aspect));
        float MinSize => CoverSize * 0.55f;

        public void Reset()
        {
            _size = CoverSize;
            _center = Vector2.zero;
            _pointers.Clear();
            Apply();
        }

        /// <summary>Test hook: move up by dy units and zoom out by factor.</summary>
        public void DebugMove(float dy, float zoom) { _center.y += dy; _size *= zoom; Apply(); }

        public void Update()
        {
            if (_size < 0) Reset();
            Apply();
        }

        void Apply()
        {
            _size = Mathf.Clamp(_size, MinSize, MaxSize);
            float h = _size, w = _size * Aspect;
            float minX = -Layout.BgW * 0.5f - SideMargin + w, maxX = Layout.BgW * 0.5f + SideMargin - w;
            float minY = -Layout.BgH * 0.5f - BottomMargin + h, maxY = Layout.BgH * 0.5f + TopMargin - h;
            _center.x = minX <= maxX ? Mathf.Clamp(_center.x, minX, maxX) : 0;
            _center.y = minY <= maxY ? Mathf.Clamp(_center.y, minY, maxY) : (minY + maxY) * 0.5f;
            _cam.orthographicSize = _size;
            _cam.transform.position = new Vector3(_center.x, _center.y, -10);
        }

        float UnitsPerPixel => 2f * _size / Mathf.Max(1, Screen.height);

        public void Begin(PointerEventData e) => _pointers[e.pointerId] = e.position;

        public void End(PointerEventData e) => _pointers.Remove(e.pointerId);

        public void Drag(PointerEventData e)
        {
            if (_pointers.Count >= 2 && _pointers.ContainsKey(e.pointerId))
            {
                // pinch: compare the distance between this finger and the other one
                Vector2 other = Vector2.zero;
                foreach (var kv in _pointers) if (kv.Key != e.pointerId) { other = kv.Value; break; }
                float before = Vector2.Distance(_pointers[e.pointerId], other);
                float after = Vector2.Distance(e.position, other);
                if (before > 10f && after > 10f) ZoomAt(before / after, (e.position + other) * 0.5f);
            }
            else
            {
                _center -= e.delta * UnitsPerPixel;
            }
            _pointers[e.pointerId] = e.position;
            Apply();
        }

        public void Scroll(PointerEventData e)
        {
            float f = e.scrollDelta.y > 0 ? 0.9f : e.scrollDelta.y < 0 ? 1.1f : 1f;
            ZoomAt(f, e.position);
            Apply();
        }

        /// <summary>Zooms keeping the world point under the screen point fixed.</summary>
        void ZoomAt(float factor, Vector2 screen)
        {
            var before = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            _size = Mathf.Clamp(_size * factor, MinSize, MaxSize);
            _cam.orthographicSize = _size;
            var after = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            _center += (Vector2)(before - after);
        }
    }
}
