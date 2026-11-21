using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DeepStation
{
    /// <summary>Procedural sprites so the prototype needs no imported art.</summary>
    public static class Sprites
    {
        // HUD (uGUI): 100 px per unit, matching the canvas reference pixels.
        static Sprite roundedSmall;
        public static Sprite RoundedSmall => Cache(ref roundedSmall, () => RoundedRect(12, 0f));

        // World: sizes below are in world units at scale 1.
        static Sprite worldRounded, worldRoundedSmall, worldOutline, circle, ring, glow, pixel, pixelLeft, pixelTop, pixelBottom;
        /// <summary>Sliced, 0.2 unit corners.</summary>
        public static Sprite WorldRounded => Cache(ref worldRounded, () => RoundedRect(20, 0f));
        /// <summary>Sliced, 0.08 unit corners.</summary>
        public static Sprite WorldRoundedSmall => Cache(ref worldRoundedSmall, () => RoundedRect(8, 0f));
        /// <summary>Sliced outline, 0.09 units thick.</summary>
        public static Sprite WorldOutline => Cache(ref worldOutline, () => RoundedRect(24, 9f));
        /// <summary>1 unit diameter.</summary>
        public static Sprite Circle => Cache(ref circle, () => Disc(0f));
        public static Sprite Ring => Cache(ref ring, () => Disc(10f));
        /// <summary>1 unit soft radial glow.</summary>
        public static Sprite Glow => Cache(ref glow, RadialGlow);
        /// <summary>1 unit white square, pivot centre.</summary>
        public static Sprite Pixel => Cache(ref pixel, () => Square(new Vector2(0.5f, 0.5f)));
        /// <summary>1 unit white square, pivot left edge (for meter fills).</summary>
        public static Sprite PixelLeft => Cache(ref pixelLeft, () => Square(new Vector2(0f, 0.5f)));
        /// <summary>1 unit white square, pivot top edge (for swinging legs).</summary>
        public static Sprite PixelTop => Cache(ref pixelTop, () => Square(new Vector2(0.5f, 1f)));
        /// <summary>1 unit white square, pivot bottom edge (for vertical fills).</summary>
        public static Sprite PixelBottom => Cache(ref pixelBottom, () => Square(new Vector2(0.5f, 0f)));

        // Explicit == null (not ??=) so Unity's destroyed-object check applies.
        static Sprite Cache(ref Sprite sprite, Func<Sprite> make)
        {
            if (sprite == null) sprite = make();
            return sprite;
        }

        /// <summary>9-sliced rounded rectangle at 100 px per unit; thickness 0 means filled.</summary>
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

        static Sprite Disc(float thickness)
        {
            const int size = 128;
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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite RadialGlow()
        {
            const int size = 128;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Clamp01(new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half);
                float a = (1f - r) * (1f - r);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite Square(Vector2 pivot)
        {
            const int size = 4;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), pivot, size, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Anti-aliased coverage from a signed distance (negative = inside).</summary>
        static float Coverage(float d, float thickness)
        {
            float outer = Mathf.Clamp01(0.5f - d);
            if (thickness <= 0f) return outer;
            return outer - Mathf.Clamp01(0.5f - (d + thickness));
        }

        /// <summary>1 x 1 unit vertical gradient through evenly spaced colours, listed top to bottom.</summary>
        public static Sprite VerticalGradient(params Color[] topToBottom)
        {
            const int h = 128;
            var tex = NewTexture(1, h);
            int segments = topToBottom.Length - 1;
            for (int y = 0; y < h; y++)
            {
                float t = (1f - y / (h - 1f)) * segments;
                int i = Mathf.Min(Mathf.FloorToInt(t), segments - 1);
                tex.SetPixel(0, y, Color.Lerp(topToBottom[i], topToBottom[i + 1], t - i));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), h, 0, SpriteMeshType.FullRect);
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
