using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepStation
{
    /// <summary>
    /// Builds and drives the whole portrait UI from code. Reads Station state every frame;
    /// the only things it writes back are crew orders, pause, and restart.
    /// </summary>
    [RequireComponent(typeof(Station))]
    public class StationUI : MonoBehaviour
    {
        static readonly Color TextColor = new Color32(222, 236, 246, 255);
        static readonly Color DimText = new Color32(124, 150, 172, 255);
        static readonly Color PanelColor = new Color32(8, 20, 34, 235);
        static readonly Color TrackColor = new Color32(3, 9, 16, 255);
        static readonly Color OxygenColor = new Color32(80, 205, 240, 255);
        static readonly Color PowerColor = new Color32(255, 198, 64, 255);
        static readonly Color QuartersColor = new Color32(176, 146, 255, 255);
        static readonly Color OkColor = new Color32(96, 224, 150, 255);
        static readonly Color WarnColor = new Color32(255, 168, 54, 255);
        static readonly Color FailColor = new Color32(255, 72, 72, 255);
        static readonly Color IdleColor = new Color32(92, 112, 132, 150);
        static readonly Color InkColor = new Color32(10, 18, 28, 255);

        // Layout in reference units (1080 x 1920 portrait).
        const float HudHeight = 370f;
        const float CrewPanelHeight = 380f;
        const float HintHeight = 84f;
        const float DepthGutter = 96f;
        const float SlotSize = 100f;
        const float TokenSize = 84f;

        class Meter
        {
            public Image Fill;
            public Text Value, Detail;
            public Color Color;
        }

        class RoomView
        {
            public Room Room;
            public Image Fill, Border;
            public Text Status, Rate;
            public Image[] Slots;
            public Color Accent, Tint;
        }

        class CrewView
        {
            public CrewMember Crew;
            public RectTransform Token;
            public Image SelectionRing, Badge;
            public Text BadgeText;
            public Image CardBorder, StaminaFill;
            public Text StaminaText, Status;
            public int SeenMoveId = -1;
            public Vector3 WalkFrom;
        }

        Station station;
        CrewMember selected;
        GameObject canvasRoot;
        int builtFrame;

        Meter oxygenMeter, powerMeter;
        Text clockText, pauseLabel, hintText;
        Image hintBackground;
        GameObject lostPanel;
        Text lostDetail;
        RectTransform tokenLayer;
        readonly List<RoomView> roomViews = new List<RoomView>();
        readonly List<CrewView> crewViews = new List<CrewView>();

        void Start()
        {
            station = GetComponent<Station>();
            Build();
        }

        // ------------------------------------------------------------------ input

        void Select(CrewMember crew)
        {
            if (station.IsLost) return;
            selected = selected == crew ? null : crew;
        }

        void OnRoomTapped(Room room)
        {
            if (station.IsLost) return;

            if (selected != null && selected.Room != room)
            {
                station.Send(selected, room);
                selected = null;
                return;
            }

            // No one selected (or tapping the selected crew's own room): tapping a room picks
            // its occupant, cycling through them, which is easier than hitting a small token.
            if (room.Assigned.Count == 0)
            {
                selected = null;
                return;
            }
            int index = selected != null ? room.Assigned.IndexOf(selected) + 1 : 0;
            selected = index < room.Assigned.Count ? room.Assigned[index] : null;
        }

        void Restart()
        {
            station.ResetStation();
            Rebuild();
        }

        void TogglePause()
        {
            if (!station.IsLost)
                station.Paused = !station.Paused;
        }

        // ------------------------------------------------------------------ per-frame

        void Update()
        {
            if (station == null || canvasRoot == null) return;

            if (station.IsLost) selected = null;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

            var t = station.tuning;
            UpdateMeter(oxygenMeter, station.Oxygen, t.maxOxygen, station.OxygenRate, t.lowWarning, pulse);
            UpdateMeter(powerMeter, station.Power, t.maxPower, station.PowerRate, t.lowWarning, pulse);
            clockText.text = $"DIVE  {FormatTime(station.Elapsed)}";
            pauseLabel.text = station.Paused ? "RESUME" : "PAUSE";

            foreach (var view in roomViews) UpdateRoom(view, pulse);
            foreach (var view in crewViews) UpdateCrew(view, pulse);
            UpdateHint(pulse);

            if (lostPanel.activeSelf != station.IsLost)
            {
                lostPanel.SetActive(station.IsLost);
                lostDetail.text = $"The crew ran out of air.\nYou kept the station running for {FormatTime(station.Elapsed)}.";
            }
        }

        void UpdateMeter(Meter m, float value, float max, float rate, float low, float pulse)
        {
            float fraction = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            m.Fill.rectTransform.anchorMax = new Vector2(fraction, 1f);
            m.Fill.enabled = fraction > 0.002f;
            m.Fill.color = fraction <= low ? Color.Lerp(m.Color, FailColor, pulse) : m.Color;
            m.Value.text = $"{value:0} / {max:0}";

            string sign = rate >= 0f ? "+" : "";
            string detail = $"{sign}{rate:0.0}/s";
            Color color = OkColor;
            if (rate < -0.01f)
            {
                if (value > 0.01f)
                {
                    float seconds = value / -rate;
                    detail += $"  ·  empty in {seconds:0}s";
                    color = seconds < 20f ? FailColor : WarnColor;
                }
                else
                {
                    detail += "  ·  EMPTY";
                    color = FailColor;
                }
            }
            else if (value >= max - 0.01f)
            {
                detail += "  ·  full";
            }
            m.Detail.text = detail;
            m.Detail.color = color;
        }

        void UpdateRoom(RoomView v, float pulse)
        {
            var room = v.Room;
            Color border, status;
            Color fill = v.Tint;
            switch (room.Alert)
            {
                case RoomAlert.Ok:
                    border = v.Accent;
                    status = TextColor;
                    break;
                case RoomAlert.Warning:
                    border = WarnColor;
                    status = WarnColor;
                    break;
                case RoomAlert.Failing:
                    border = FailColor;
                    border.a = 0.35f + 0.65f * pulse;
                    status = FailColor;
                    fill = Color.Lerp(v.Tint, new Color(0.45f, 0.05f, 0.07f, 0.95f), 0.25f + 0.5f * pulse);
                    break;
                default:
                    border = IdleColor;
                    status = DimText;
                    break;
            }
            v.Border.color = border;
            v.Fill.color = fill;
            v.Status.text = room.StatusText;
            v.Status.color = status;
            v.Rate.text = room.RateText;

            // Empty slots light up while a crew member is selected, as drop targets.
            bool targeting = selected != null && selected.Room != room;
            for (int i = 0; i < v.Slots.Length; i++)
            {
                var c = v.Accent;
                c.a = targeting ? 0.55f + 0.3f * pulse : 0.22f;
                v.Slots[i].color = c;
            }
        }

        void UpdateCrew(CrewView v, float pulse)
        {
            var crew = v.Crew;
            var t = station.tuning;
            bool tired = crew.Stamina < t.tiredStamina;

            // Stamina card.
            float s01 = Mathf.Clamp01(crew.Stamina / Station.MaxStamina);
            v.StaminaFill.rectTransform.anchorMax = new Vector2(s01, 1f);
            v.StaminaFill.enabled = s01 > 0.005f;
            v.StaminaFill.color = crew.IsExhausted ? FailColor : tired ? WarnColor : OkColor;
            v.StaminaText.text = $"STAMINA {crew.Stamina:0}";

            string status;
            Color statusColor;
            bool resting = crew.Room.Type == RoomType.Quarters;
            if (crew.IsMoving)
            {
                status = $"Heading to {crew.Room.Name}";
                statusColor = DimText;
            }
            else if (resting)
            {
                bool full = crew.Stamina >= Station.MaxStamina - 0.5f;
                status = full ? "Fully rested. Ready to work." : $"Resting in {crew.Room.Name}";
                statusColor = full ? OkColor : TextColor;
            }
            else if (crew.IsExhausted)
            {
                status = $"EXHAUSTED in {crew.Room.Name}. Half speed, needs rest.";
                statusColor = FailColor;
            }
            else if (tired)
            {
                status = $"Tired in {crew.Room.Name}. Rest soon.";
                statusColor = WarnColor;
            }
            else
            {
                status = $"Working in {crew.Room.Name}";
                statusColor = TextColor;
            }
            v.Status.text = status;
            v.Status.color = statusColor;

            bool isSelected = selected == crew;
            Color cardBorder = IdleColor;
            if (isSelected) cardBorder = crew.Color;
            else if (crew.IsExhausted && !resting) cardBorder = new Color(FailColor.r, FailColor.g, FailColor.b, 0.35f + 0.65f * pulse);
            v.CardBorder.color = cardBorder;

            // Token badge: "!" when tired or exhausted on the job.
            bool showBadge = !resting && !crew.IsMoving && tired;
            v.Badge.gameObject.SetActive(showBadge);
            if (showBadge) v.Badge.color = crew.IsExhausted ? FailColor : WarnColor;

            v.SelectionRing.enabled = isSelected;
            if (isSelected)
                v.SelectionRing.rectTransform.localScale = Vector3.one * (1f + 0.08f * pulse);

            MoveToken(v);
        }

        void MoveToken(CrewView v)
        {
            var crew = v.Crew;
            var room = roomViews.Find(r => r.Room == crew.Room);
            int slot = Mathf.Clamp(crew.Room.Assigned.IndexOf(crew), 0, room.Slots.Length - 1);
            Vector3 target = room.Slots[slot].rectTransform.position;

            // For the first few frames the canvas scaler is still settling, so just snap.
            if (Time.frameCount - builtFrame < 3)
            {
                v.Token.position = target;
                v.SeenMoveId = crew.MoveId;
                return;
            }

            if (v.SeenMoveId != crew.MoveId)
            {
                v.SeenMoveId = crew.MoveId;
                v.WalkFrom = v.Token.position;
            }

            if (crew.IsMoving)
            {
                float total = Mathf.Max(0.01f, station.tuning.travelSeconds);
                float progress = Mathf.Clamp01(1f - crew.TravelLeft / total);
                v.Token.position = Vector3.Lerp(v.WalkFrom, target, Mathf.SmoothStep(0f, 1f, progress));
            }
            else
            {
                // Glide rather than jump when slots reshuffle.
                v.Token.position = Vector3.Lerp(v.Token.position, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }
        }

        void UpdateHint(float pulse)
        {
            string text;
            Color background = new Color(0f, 0f, 0f, 0f);
            Color color = DimText;

            if (station.SuffocationTime > 0f && !station.IsLost)
            {
                text = $"OUT OF AIR: crew lost in {Mathf.CeilToInt(station.SuffocationLeft)}s. Staff a scrubber!";
                background = Color.Lerp(new Color(0.5f, 0.05f, 0.07f, 0.95f), FailColor, pulse * 0.6f);
                color = Color.white;
            }
            else if (station.PowerSupply < 0.98f)
            {
                text = "BLACKOUT: not enough power. Staff a generator.";
                background = new Color(0.45f, 0.26f, 0.02f, 0.95f);
                color = Color.white;
            }
            else if (station.Paused)
            {
                text = "Paused. You can still give orders.";
                color = TextColor;
            }
            else if (selected != null)
            {
                text = $"Send {selected.Name} where? Tap a room. A full room swaps.";
                color = selected.Color;
            }
            else
            {
                text = "Tap a crew member, then tap a room.";
            }

            hintText.text = text;
            hintText.color = color;
            hintBackground.color = background;
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        // ------------------------------------------------------------------ building

        void Rebuild()
        {
            if (canvasRoot != null) Destroy(canvasRoot);
            selected = null;
            roomViews.Clear();
            crewViews.Clear();
            Build();
        }

        void Build()
        {
            builtFrame = Time.frameCount;

            var canvasGo = new GameObject("Station UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasRoot = canvasGo;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = (RectTransform)canvasGo.transform;

            // Background doubles as the "tap empty space to deselect" target.
            var background = UI.Image("Background", root,
                Sprites.VerticalGradient(new Color32(14, 46, 72, 255), new Color32(2, 5, 12, 255)), Color.white, raycast: true);
            UI.Stretch(background.rectTransform);
            UI.Button(background.gameObject, () => selected = null);

            var safe = UI.Stretch(UI.Rect("Safe Area", root));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildHud(safe);
            BuildStation(safe);
            BuildCrewPanel(safe);
            BuildLostPanel(root);
        }

        void BuildHud(RectTransform safe)
        {
            var hud = UI.Place(UI.Rect("HUD", safe), new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -HudHeight), Vector2.zero);
            var panel = UI.Image("Panel", hud, Sprites.Rounded, PanelColor);
            UI.Place(panel.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 10f), new Vector2(-24f, -24f));
            var p = panel.rectTransform;

            var title = UI.Text("Title", p, 32, DimText, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = "DEEP-SEA STATION";
            UI.Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(32f, -92f), new Vector2(-420f, -20f));

            clockText = UI.Text("Clock", p, 32, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            UI.Place(clockText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(380f, -92f), new Vector2(-236f, -20f));

            var pause = UI.Image("Pause", p, Sprites.RoundedSmall, new Color(1f, 1f, 1f, 0.12f), raycast: true);
            UI.Pin(pause.rectTransform, Vector2.one, new Vector2(-24f - 92f, -20f - 36f), new Vector2(184f, 72f));
            UI.Button(pause.gameObject, TogglePause);
            pauseLabel = UI.Text("Label", pause.rectTransform, 28, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Stretch(pauseLabel.rectTransform);

            float low = station.tuning.lowWarning;
            oxygenMeter = BuildMeter(p, "OXYGEN", OxygenColor, -108f, low);
            powerMeter = BuildMeter(p, "POWER", PowerColor, -222f, low);
        }

        Meter BuildMeter(RectTransform parent, string label, Color color, float top, float low)
        {
            var m = new Meter { Color = color };
            var r = UI.Place(UI.Rect(label, parent), new Vector2(0f, 1f), Vector2.one, new Vector2(32f, top - 106f), new Vector2(-32f, top));

            var name = UI.Text("Label", r, 30, color, TextAnchor.MiddleLeft, FontStyle.Bold);
            name.text = label;
            UI.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0.4f, 1f), new Vector2(0f, -50f), Vector2.zero);

            m.Detail = UI.Text("Detail", r, 30, TextColor, TextAnchor.MiddleRight);
            UI.Place(m.Detail.rectTransform, new Vector2(0.25f, 1f), Vector2.one, new Vector2(0f, -50f), Vector2.zero);

            var track = UI.Image("Track", r, Sprites.RoundedSmall, TrackColor);
            UI.Place(track.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 48f));

            m.Fill = UI.Image("Fill", track.rectTransform, Sprites.RoundedSmall, color);
            UI.Stretch(m.Fill.rectTransform);

            var lowLine = UI.Image("Low Line", track.rectTransform, null, new Color(1f, 1f, 1f, 0.35f));
            UI.Place(lowLine.rectTransform, new Vector2(low, 0f), new Vector2(low, 1f), new Vector2(-2f, 0f), new Vector2(2f, 0f));

            m.Value = UI.Text("Value", track.rectTransform, 28, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Stretch(m.Value.rectTransform);
            m.Value.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return m;
        }

        void BuildStation(RectTransform safe)
        {
            var area = UI.Place(UI.Rect("Station", safe), Vector2.zero, Vector2.one,
                new Vector2(24f, CrewPanelHeight + HintHeight), new Vector2(-24f, -HudHeight));

            var grid = UI.Place(UI.Rect("Rooms", area), Vector2.zero, Vector2.one, new Vector2(DepthGutter, 0f), Vector2.zero);

            // The shaft between the two columns, purely decorative.
            var shaft = UI.Image("Shaft", grid, null, new Color32(16, 40, 62, 255));
            UI.Place(shaft.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-8f, 0f), new Vector2(8f, 0f));

            for (int floor = 0; floor < Station.FloorCount; floor++)
            {
                float yMax = 1f - floor / (float)Station.FloorCount;
                float yMin = 1f - (floor + 1) / (float)Station.FloorCount;
                var depth = UI.Text($"Depth {floor}", area, 26, DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
                depth.text = $"-{(floor + 1) * 40}\nm";
                UI.Place(depth.rectTransform, new Vector2(0f, yMin), new Vector2(0f, yMax), Vector2.zero, new Vector2(DepthGutter - 12f, 0f));
            }

            foreach (var room in station.Rooms)
                roomViews.Add(BuildRoom(grid, room));

            tokenLayer = UI.Stretch(UI.Rect("Crew Tokens", area));
            foreach (var crew in station.Crew)
                crewViews.Add(BuildToken(crew));
        }

        RoomView BuildRoom(RectTransform grid, Room room)
        {
            var accent = room.Type == RoomType.Generator ? PowerColor
                : room.Type == RoomType.Scrubber ? OxygenColor
                : QuartersColor;
            var v = new RoomView
            {
                Room = room,
                Accent = accent,
                Tint = Color.Lerp(new Color32(9, 20, 33, 245), accent, 0.1f)
            };

            float xMin = room.Column / (float)Station.ColumnCount;
            float xMax = (room.Column + 1) / (float)Station.ColumnCount;
            float yMax = 1f - room.Floor / (float)Station.FloorCount;
            float yMin = 1f - (room.Floor + 1) / (float)Station.FloorCount;
            bool left = room.Column == 0;
            var rect = UI.Place(UI.Rect(room.Name, grid), new Vector2(xMin, yMin), new Vector2(xMax, yMax),
                new Vector2(left ? 0f : 14f, 10f), new Vector2(left ? -14f : 0f, -10f));

            v.Fill = UI.Image("Fill", rect, Sprites.Rounded, v.Tint, raycast: true);
            UI.Stretch(v.Fill.rectTransform);
            UI.Button(v.Fill.gameObject, () => OnRoomTapped(room));

            v.Border = UI.Image("Border", rect, Sprites.Outline, accent);
            UI.Stretch(v.Border.rectTransform);

            var title = UI.Text("Title", rect, 34, accent, TextAnchor.UpperLeft, FontStyle.Bold);
            title.text = room.Name.ToUpperInvariant();
            UI.Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -66f), new Vector2(-16f, -22f));

            v.Status = UI.Text("Status", rect, 27, TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            UI.Place(v.Status.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -140f), new Vector2(-16f, -70f));

            v.Rate = UI.Text("Rate", rect, 24, DimText, TextAnchor.UpperLeft);
            UI.Place(v.Rate.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -200f), new Vector2(-16f, -140f));

            v.Slots = new Image[room.Capacity];
            for (int i = 0; i < room.Capacity; i++)
            {
                v.Slots[i] = UI.Image($"Slot {i}", rect, Sprites.Ring, accent);
                UI.Pin(v.Slots[i].rectTransform, new Vector2(1f, 0f),
                    new Vector2(-20f - SlotSize * 0.5f - i * (SlotSize + 12f), 20f + SlotSize * 0.5f),
                    new Vector2(SlotSize, SlotSize));
            }
            return v;
        }

        CrewView BuildToken(CrewMember crew)
        {
            var v = new CrewView { Crew = crew };
            v.Token = UI.Pin(UI.Rect(crew.Name, tokenLayer), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TokenSize, TokenSize));

            v.SelectionRing = UI.Image("Selection", v.Token, Sprites.Ring, Color.white);
            UI.Pin(v.SelectionRing.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TokenSize + 40f, TokenSize + 40f));

            var body = UI.Image("Body", v.Token, Sprites.Circle, crew.Color, raycast: true);
            UI.Stretch(body.rectTransform);
            UI.Button(body.gameObject, () => Select(crew));

            var initial = UI.Text("Initial", body.rectTransform, 40, InkColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            initial.text = crew.Name.Substring(0, 1);
            UI.Stretch(initial.rectTransform);

            v.Badge = UI.Image("Badge", v.Token, Sprites.Circle, WarnColor);
            UI.Pin(v.Badge.rectTransform, Vector2.one, new Vector2(-6f, -6f), new Vector2(38f, 38f));
            v.BadgeText = UI.Text("Mark", v.Badge.rectTransform, 28, InkColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            v.BadgeText.text = "!";
            UI.Stretch(v.BadgeText.rectTransform);
            return v;
        }

        void BuildCrewPanel(RectTransform safe)
        {
            hintBackground = UI.Image("Hint", safe, Sprites.RoundedSmall, Color.clear);
            UI.Place(hintBackground.rectTransform, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(24f, CrewPanelHeight + 8f), new Vector2(-24f, CrewPanelHeight + HintHeight - 8f));
            hintText = UI.Text("Text", hintBackground.rectTransform, 30, DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Place(hintText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, 0f));

            var panel = UI.Place(UI.Rect("Crew Panel", safe), Vector2.zero, new Vector2(1f, 0f),
                new Vector2(16f, 24f), new Vector2(-16f, CrewPanelHeight));

            for (int i = 0; i < crewViews.Count; i++)
            {
                var v = crewViews[i];
                var crew = v.Crew;
                float n = crewViews.Count;
                var card = UI.Place(UI.Rect(crew.Name + " Card", panel), new Vector2(i / n, 0f), new Vector2((i + 1) / n, 1f),
                    new Vector2(8f, 0f), new Vector2(-8f, 0f));

                var fill = UI.Image("Fill", card, Sprites.Rounded, PanelColor, raycast: true);
                UI.Stretch(fill.rectTransform);
                UI.Button(fill.gameObject, () => Select(crew));

                v.CardBorder = UI.Image("Border", card, Sprites.Outline, IdleColor);
                UI.Stretch(v.CardBorder.rectTransform);

                var avatar = UI.Image("Avatar", card, Sprites.Circle, crew.Color);
                UI.Pin(avatar.rectTransform, new Vector2(0f, 1f), new Vector2(22f + 38f, -22f - 38f), new Vector2(76f, 76f));
                var initial = UI.Text("Initial", avatar.rectTransform, 36, InkColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                initial.text = crew.Name.Substring(0, 1);
                UI.Stretch(initial.rectTransform);

                var name = UI.Text("Name", card, 36, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                name.text = crew.Name;
                UI.Place(name.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(112f, -98f), new Vector2(-12f, -22f));

                var track = UI.Image("Stamina", card, Sprites.RoundedSmall, TrackColor);
                UI.Place(track.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -160f), new Vector2(-20f, -116f));
                v.StaminaFill = UI.Image("Fill", track.rectTransform, Sprites.RoundedSmall, OkColor);
                UI.Stretch(v.StaminaFill.rectTransform);
                v.StaminaText = UI.Text("Value", track.rectTransform, 22, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Stretch(v.StaminaText.rectTransform);
                v.StaminaText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

                v.Status = UI.Text("Status", card, 27, TextColor, TextAnchor.UpperLeft);
                UI.Place(v.Status.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 16f), new Vector2(-16f, -176f));
            }
        }

        void BuildLostPanel(RectTransform root)
        {
            var dim = UI.Image("Station Lost", root, null, new Color(0f, 0.01f, 0.03f, 0.88f), raycast: true);
            UI.Stretch(dim.rectTransform);
            lostPanel = dim.gameObject;

            var title = UI.Text("Title", dim.rectTransform, 84, FailColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.text = "STATION LOST";
            UI.Place(title.rectTransform, new Vector2(0f, 0.58f), new Vector2(1f, 0.68f), new Vector2(40f, 0f), new Vector2(-40f, 0f));

            lostDetail = UI.Text("Detail", dim.rectTransform, 38, TextColor, TextAnchor.MiddleCenter);
            UI.Place(lostDetail.rectTransform, new Vector2(0f, 0.46f), new Vector2(1f, 0.58f), new Vector2(60f, 0f), new Vector2(-60f, 0f));

            var button = UI.Image("Dive Again", dim.rectTransform, Sprites.Rounded, OxygenColor, raycast: true);
            UI.Pin(button.rectTransform, new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(520f, 140f));
            UI.Button(button.gameObject, Restart);
            var label = UI.Text("Label", button.rectTransform, 44, InkColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.text = "DIVE AGAIN";
            UI.Stretch(label.rectTransform);

            lostPanel.SetActive(false);
        }
    }
}
