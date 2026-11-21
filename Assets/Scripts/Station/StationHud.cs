using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepStation
{
    /// <summary>
    /// The only screen-space UI: a slim oxygen/power bar along the top (with emergency tank pips
    /// beside OXYGEN), a warning line under it when something is critical, and a one-line hint at
    /// the bottom. Nothing here is tappable.
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
            public RectTransform Root;
            public Image Fill;
            public Text Value;
            public Color Color;
        }

        Station station;
        StationView view;
        Bar oxygen, power;
        Image warningBackground;
        Text warningText, hintText;

        // Milestone toast: a short, calm pop-up under the top bar. Queued so none are lost.
        const float ToastSeconds = 2.8f;
        readonly Queue<string> toasts = new Queue<string>();
        CanvasGroup toastGroup;
        Text toastText;
        float toastAge = float.MaxValue;
        readonly List<Image> tankPips = new List<Image>();

        void Start()
        {
            station = GetComponent<Station>();
            view = GetComponent<StationView>();
            Build();
            station.Milestone += toasts.Enqueue;
        }

        void OnDestroy()
        {
            if (station != null) station.Milestone -= toasts.Enqueue;
        }

        void UpdateToast(float dt)
        {
            toastAge += dt;
            if (toastAge >= ToastSeconds && toasts.Count > 0)
            {
                toastText.text = toasts.Dequeue();
                toastAge = 0f;
            }
            bool showing = toastAge < ToastSeconds;
            toastGroup.gameObject.SetActive(showing);
            if (!showing) return;
            toastGroup.alpha = Mathf.Clamp01(Mathf.Min(toastAge / 0.25f, (ToastSeconds - toastAge) / 0.5f));
            // A small pop as it appears.
            float pop = 1f + 0.08f * Mathf.Sin(Mathf.Clamp01(toastAge / 0.3f) * Mathf.PI);
            toastGroup.transform.localScale = new Vector3(pop, pop, 1f);
        }

        void Update()
        {
            if (station == null) return;
            var t = station.tuning;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);

            UpdateBar(oxygen, station.Oxygen, t.maxOxygen, station.OxygenRate, t.lowWarning, pulse);
            UpdateBar(power, station.Power, t.maxPower, station.PowerRate, t.lowWarning, pulse);
            UpdateTankPips(pulse);
            UpdateToast(Time.unscaledDeltaTime);

            string warning = null;
            if (station.FaintedCount > 0)
                warning = $"CREW FAINTED: NO AIR  ·  waking in {Mathf.CeilToInt(station.WakeIn)}s";
            else if (station.SuffocationTime > 0f)
                warning = $"OUT OF AIR  ·  crew faint in {Mathf.CeilToInt(station.SuffocationLeft)}s";
            else if (station.VentingTank != null)
                warning = $"EMERGENCY O2 VENTING  ·  {station.FullTanks} tank{(station.FullTanks == 1 ? "" : "s")} left";
            else if (station.PowerSupply < 0.98f)
                warning = "BLACKOUT  ·  scrubber is stalling";
            else
            {
                foreach (var room in station.Rooms)
                    if (room.Broken && room.Alert == RoomAlert.Failing)
                    {
                        warning = $"{room.Name.ToUpperInvariant()} BROKEN  ·  send crew to repair";
                        break;
                    }
            }
            warningBackground.gameObject.SetActive(warning != null);
            if (warning != null)
            {
                warningText.text = warning;
                warningBackground.color = Color.Lerp(new Color(0.45f, 0.04f, 0.05f, 0.92f), new Color(0.75f, 0.1f, 0.1f, 0.92f), pulse);
            }

            if (view.Selected != null)
                hintText.text = $"Give {view.Selected.Name} a job: tap a room\n<size=26>{station.TraitText(view.Selected)}</size>";
            else if (station.FaintedCount > 0 || station.SuffocationTime > 0f)
                hintText.text = AirHint();
            else
                hintText.text = "Tap a crew member, then a room to give them a job";
        }

        /// <summary>While the air is out, name the fix: who's missing, or what's stopping the scrubber.</summary>
        string AirHint()
        {
            foreach (var room in station.Rooms)
            {
                if (room.Type != RoomType.Scrubber) continue;
                CrewMember holder = null;
                bool atPost = false;
                foreach (var crew in station.Crew)
                {
                    if (crew.Job != room) continue;
                    holder = crew;
                    if (crew.Room == room) atPost = true;
                }
                if (holder == null) return "Nobody's on the Oxygen Scrubber: give someone that job";
                if (!atPost) return $"{holder.Name} is resting: send them to the Scrubber early";
                if (room.Broken) return "The Oxygen Scrubber is broken: its crew will fix it";
            }
            if (station.PowerSupply < 0.98f) return "The scrubber needs power: put someone on the Generator";
            return station.FaintedCount > 0 ? "The crew will come round soon" : "Air is on its way";
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

        /// <summary>One small pip per emergency tank, filled to its level, next to the OXYGEN label.</summary>
        void UpdateTankPips(float pulse)
        {
            if (tankPips.Count != station.Tanks.Count)
            {
                foreach (var pip in tankPips) Destroy(pip.transform.parent.gameObject);
                tankPips.Clear();
                for (int i = 0; i < station.Tanks.Count; i++)
                {
                    var back = UI.Image("Tank", oxygen.Root, Sprites.RoundedSmall, new Color32(2, 8, 14, 255));
                    UI.Pin(back.rectTransform, new Vector2(0f, 1f), new Vector2(129f + i * 22f, -22f), new Vector2(14f, 26f));
                    var fill = UI.Image("Fill", back.rectTransform, Sprites.RoundedSmall, Teal);
                    UI.Stretch(fill.rectTransform);
                    tankPips.Add(fill);
                }
            }

            for (int i = 0; i < tankPips.Count; i++)
            {
                var tank = station.Tanks[i];
                var pip = tankPips[i];
                pip.enabled = tank.Fill > 0.01f;
                pip.rectTransform.anchorMax = new Vector2(1f, Mathf.Clamp01(tank.Fill));
                pip.color = station.VentingTank == tank ? Color.Lerp(Teal, Color.white, 0.5f * pulse)
                    : tank.IsFull ? Teal : new Color(Teal.r, Teal.g, Teal.b, 0.5f);
            }
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

            // Sits below the warning line so a good moment never hides a warning.
            var toast = UI.Image("Toast", safe, Sprites.RoundedSmall, new Color32(8, 44, 58, 235));
            UI.Pin(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -BarHeight - 12f - 18f - 68f - 16f - 32f), new Vector2(700f, 64f));
            toastText = UI.Text("Text", toast.rectTransform, 32, new Color32(190, 245, 250, 255), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Stretch(toastText.rectTransform);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            toast.gameObject.SetActive(false);
        }

        Bar BuildBar(RectTransform parent, string label, Color color, float xMin, float xMax)
        {
            var b = new Bar { Color = color };
            var r = UI.Place(UI.Rect(label, parent), new Vector2(xMin, 0f), new Vector2(xMax, 1f), new Vector2(24f, 16f), new Vector2(-24f, -12f));
            b.Root = r;

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
