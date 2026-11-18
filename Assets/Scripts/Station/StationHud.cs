using UnityEngine;
using UnityEngine.UI;

namespace DeepStation
{
    /// <summary>
    /// The only screen-space UI: a slim oxygen/power bar along the top, a warning line under it
    /// when something is critical, and a one-line hint at the bottom. Nothing here is tappable.
    /// </summary>
    [RequireComponent(typeof(Station), typeof(StationView))]
    public class StationHud : MonoBehaviour
    {
        static readonly Color Teal = new Color32(84, 214, 228, 255);
        static readonly Color PowerColor = new Color32(255, 190, 70, 255);
        static readonly Color WarningRed = new Color32(255, 60, 60, 255);
        static readonly Color TextColor = new Color32(222, 234, 244, 255);
        static readonly Color DimText = new Color32(150, 172, 190, 255);

        const float BarHeight = 112f;

        class Bar
        {
            public Image Fill;
            public Text Value;
            public Color Color;
        }

        Station station;
        StationView view;
        Bar oxygen, power;
        Image warningBackground;
        Text warningText, hintText, lostText;
        GameObject lostOverlay;

        void Start()
        {
            station = GetComponent<Station>();
            view = GetComponent<StationView>();
            Build();
        }

        void Update()
        {
            if (station == null) return;
            var t = station.tuning;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);

            UpdateBar(oxygen, station.Oxygen, t.maxOxygen, station.OxygenRate, t.lowWarning, pulse);
            UpdateBar(power, station.Power, t.maxPower, station.PowerRate, t.lowWarning, pulse);

            string warning = null;
            if (!station.IsLost && station.SuffocationTime > 0f)
                warning = $"OUT OF AIR  ·  crew lost in {Mathf.CeilToInt(station.SuffocationLeft)}s";
            else if (!station.IsLost && station.PowerSupply < 0.98f)
                warning = "BLACKOUT  ·  scrubber is stalling";
            warningBackground.gameObject.SetActive(warning != null);
            if (warning != null)
            {
                warningText.text = warning;
                warningBackground.color = Color.Lerp(new Color(0.45f, 0.04f, 0.05f, 0.92f), new Color(0.75f, 0.1f, 0.1f, 0.92f), pulse);
            }

            if (station.IsLost)
                hintText.text = "";
            else if (view.Selected != null)
                hintText.text = $"Send {view.Selected.Name}: tap a room";
            else
                hintText.text = "Tap a crew member, then tap a room";

            lostOverlay.SetActive(station.IsLost);
            if (station.IsLost)
                lostText.text = $"STATION LOST\n<size=40>Held on for {Format(station.Elapsed)}. New dive in {Mathf.CeilToInt(Mathf.Max(0f, station.RestartIn))}…</size>";
        }

        void UpdateBar(Bar bar, float value, float max, float rate, float low, float pulse)
        {
            float fraction = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            bar.Fill.rectTransform.anchorMax = new Vector2(fraction, 1f);
            bar.Fill.enabled = fraction > 0.002f;
            bar.Fill.color = fraction <= low ? Color.Lerp(bar.Color, WarningRed, pulse) : bar.Color;

            string rateText = $"{(rate >= 0f ? "+" : "")}{rate:0.0}/s";
            if (rate < -0.01f && value > 0.01f)
                rateText += $"  {value / -rate:0}s left";
            string rateColor = rate < -0.01f && fraction <= low ? "#FF6A6A" : "#9DB0C2";
            bar.Value.text = $"{value:0} <color={rateColor}>{rateText}</color>";
        }

        static string Format(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        // ------------------------------------------------------------------ building

        void Build()
        {
            var go = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            var root = (RectTransform)go.transform;

            var safe = UI.Stretch(UI.Rect("Safe Area", root));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var bar = UI.Image("Top Bar", safe, Sprites.RoundedSmall, new Color32(4, 14, 26, 215));
            UI.Place(bar.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -BarHeight - 12f), new Vector2(-16f, -12f));
            oxygen = BuildBar(bar.rectTransform, "OXYGEN", Teal, 0f, 0.5f);
            power = BuildBar(bar.rectTransform, "POWER", PowerColor, 0.5f, 1f);

            warningBackground = UI.Image("Warning", safe, Sprites.RoundedSmall, WarningRed);
            UI.Pin(warningBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -BarHeight - 12f - 18f - 34f), new Vector2(820f, 68f));
            warningText = UI.Text("Text", warningBackground.rectTransform, 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Stretch(warningText.rectTransform);
            warningBackground.gameObject.SetActive(false);

            hintText = UI.Text("Hint", safe, 32, DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Place(hintText.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, 24f), new Vector2(-24f, 104f));
            hintText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            var overlay = UI.Image("Station Lost", root, null, new Color(0f, 0.01f, 0.03f, 0.7f));
            UI.Stretch(overlay.rectTransform);
            lostOverlay = overlay.gameObject;
            lostText = UI.Text("Text", overlay.rectTransform, 80, WarningRed, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Place(lostText.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, 0f), new Vector2(-40f, 0f));
            lostOverlay.SetActive(false);
        }

        Bar BuildBar(RectTransform parent, string label, Color color, float xMin, float xMax)
        {
            var b = new Bar { Color = color };
            var r = UI.Place(UI.Rect(label, parent), new Vector2(xMin, 0f), new Vector2(xMax, 1f), new Vector2(24f, 16f), new Vector2(-24f, -12f));

            var name = UI.Text("Label", r, 26, color, TextAnchor.MiddleLeft, FontStyle.Bold);
            name.text = label;
            UI.Place(name.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -44f), Vector2.zero);

            b.Value = UI.Text("Value", r, 26, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            UI.Place(b.Value.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -44f), Vector2.zero);

            var track = UI.Image("Track", r, Sprites.RoundedSmall, new Color32(2, 8, 14, 255));
            UI.Place(track.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 30f));
            b.Fill = UI.Image("Fill", track.rectTransform, Sprites.RoundedSmall, color);
            UI.Stretch(b.Fill.rectTransform);
            return b;
        }
    }
}
