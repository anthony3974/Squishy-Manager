using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DeepStation
{
    /// <summary>Procedural sprites so the prototype needs no imported art.</summary>
    public static class Sprites
    {
        static Sprite rounded, roundedSmall, outline, circle, ring;

        // Explicit == null (not ??=) so Unity's destroyed-object check applies.
        public static Sprite Rounded { get { if (rounded == null) rounded = RoundedRect(24, 0f); return rounded; } }
        public static Sprite RoundedSmall { get { if (roundedSmall == null) roundedSmall = RoundedRect(12, 0f); return roundedSmall; } }
        public static Sprite Outline { get { if (outline == null) outline = RoundedRect(24, 4f); return outline; } }
        public static Sprite Circle { get { if (circle == null) circle = Disc(128, 0f); return circle; } }
        public static Sprite Ring { get { if (ring == null) ring = Disc(128, 7f); return ring; } }

        /// <summary>9-sliced rounded rectangle; thickness 0 means filled, otherwise an outline.</summary>
        static Sprite RoundedRect(int radius, float thickness)
        {
            int size = radius * 2 + 2;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Coverage(d, thickness));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Sprite Disc(int size, float thickness)
        {
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude - (half - 1f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Coverage(d, thickness));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Anti-aliased coverage from a signed distance (negative = inside).</summary>
        static float Coverage(float d, float thickness)
        {
            float outer = Mathf.Clamp01(0.5f - d);
            if (thickness <= 0f) return outer;
            return outer - Mathf.Clamp01(0.5f - (d + thickness));
        }

        public static Sprite VerticalGradient(Color top, Color bottom)
        {
            const int h = 64;
            var tex = NewTexture(1, h);
            for (int y = 0; y < h; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (h - 1f)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Texture2D NewTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
        }
    }

    /// <summary>Small helpers for building uGUI from code.</summary>
    public static class UI
    {
        static Font font;
        public static Font Font { get { if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); return font; } }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt) => Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        /// <summary>Fixed-size rect anchored at a point of its parent.</summary>
        public static RectTransform Pin(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero)
                img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text Text(string name, Transform parent, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var t = Rect(name, parent).gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(GameObject target, UnityAction onClick)
        {
            var b = target.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            b.onClick.AddListener(onClick);
            return b;
        }
    }

    /// <summary>Keeps a RectTransform inside the phone's safe area (notches, rounded corners).</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        Vector2Int screen;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != applied || screen.x != Screen.width || screen.y != Screen.height)
                Apply();
        }

        void Apply()
        {
            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(applied.xMin / screen.x, applied.yMin / screen.y);
            rt.anchorMax = new Vector2(applied.xMax / screen.x, applied.yMax / screen.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
