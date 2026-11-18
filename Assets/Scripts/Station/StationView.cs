using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepStation
{
    /// <summary>
    /// Draws the side-on cutaway: ocean, bioluminescent critters, the station's rooms and
    /// ladder shaft, and the walking crew. Also owns crew selection and tap handling.
    /// Everything is flat procedural sprites; nothing is imported.
    /// </summary>
    [RequireComponent(typeof(Station))]
    public class StationView : MonoBehaviour
    {
        // Palette: deep blue and teal outside, warm amber inside, red only for warnings.
        static readonly Color OceanTop = C(30, 112, 140);
        static readonly Color OceanUpper = C(14, 70, 102);
        static readonly Color OceanMid = C(7, 38, 70);
        static readonly Color OceanLow = C(4, 18, 42);
        static readonly Color OceanDeep = C(2, 6, 16);
        static readonly Color Hull = C(46, 60, 74);
        static readonly Color HullDark = C(24, 32, 42);
        static readonly Color SlabColor = C(30, 38, 48);
        static readonly Color WallTop = C(128, 88, 48);
        static readonly Color WallBottom = C(60, 41, 27);
        static readonly Color Amber = C(255, 182, 90);
        static readonly Color LampColor = C(255, 224, 168);
        static readonly Color Teal = C(84, 214, 228);
        static readonly Color PowerColor = C(255, 190, 70);
        static readonly Color RestColor = C(214, 224, 236);
        static readonly Color WarningRed = C(255, 60, 60);
        static readonly Color Caution = C(255, 156, 46);
        static readonly Color GlassColor = C(10, 50, 70);
        static readonly Color LabelColor = C(206, 220, 232);
        static readonly Color DimLabel = C(130, 150, 168);

        // Sorting orders, back to front.
        const int OceanOrder = -100, RayOrder = -95, CritterOrder = -90, TetherOrder = -60, SpillOrder = -50,
            ShaftOrder = -30, HullOrder = 0, WallOrder = 1, GlowOrder = 2, FrameOrder = 3, GlassOrder = 4,
            LampOrder = 5, MachineOrder = 6, SlabOrder = 9, CrewOrder = 10, TargetOrder = 19, AlarmOrder = 20,
            MeterOrder = 25, LabelOrder = 30;

        const float LabelScale = 0.01f;

        static Color C(byte r, byte g, byte b, byte a = 255) => new Color32(r, g, b, a);
        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        class RoomView
        {
            public Room Room;
            public SpriteRenderer Alarm, Target, MeterFill, Lamp;
            public float MeterWidth;
            public Text Status, Rate;
            public Transform Rotor;
            public float RotorAngle, BubbleClock;
            public readonly List<SpriteRenderer> Bubbles = new List<SpriteRenderer>();
            public Vector2 TankBottom;
            public float TankHeight;
        }

        class CrewView
        {
            public CrewMember Crew;
            public Transform Root, Figure, LegFront, LegBack;
            public SpriteRenderer Body, Visor, Selection, Alert;
            public float Phase;
        }

        class Critter
        {
            public Transform Transform;
            public SpriteRenderer[] Renderers;
            public float[] BaseAlpha;
            public Transform[] Tentacles;
            public Vector2 Home;
            public float Phase, Rise;
        }

        Station station;
        Transform stationRoot;
        Transform labelRoot;
        Sprite wallGradient;
        readonly List<RoomView> roomViews = new List<RoomView>();
        readonly List<CrewView> crewViews = new List<CrewView>();
        readonly List<Critter> critters = new List<Critter>();

        public CrewMember Selected { get; private set; }

        static Material spriteMaterial;
        static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader != null) spriteMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
                return spriteMaterial;
            }
        }

        void Start()
        {
            station = GetComponent<Station>();
            wallGradient = Sprites.VerticalGradient(WallTop, WallBottom);
            BuildOcean();
            BuildStation();
            station.Rebuilt += Rebuild;
        }

        void OnDestroy()
        {
            if (station != null) station.Rebuilt -= Rebuild;
        }

        void Rebuild()
        {
            Selected = null;
            if (stationRoot != null) Destroy(stationRoot.gameObject);
            roomViews.Clear();
            crewViews.Clear();
            BuildStation();
        }

        // ------------------------------------------------------------------ input

        /// <summary>Called by StationCamera for a tap (not a drag) at a world position.</summary>
        public void HandleTap(Vector2 world)
        {
            if (station.IsLost) return;

            CrewView nearest = null;
            float best = 0.65f;
            foreach (var v in crewViews)
            {
                float d = Vector2.Distance(world, v.Body.transform.position);
                if (d < best)
                {
                    best = d;
                    nearest = v;
                }
            }
            if (nearest != null)
            {
                Selected = Selected == nearest.Crew ? null : nearest.Crew;
                return;
            }

            foreach (var room in station.Rooms)
            {
                var hit = room.Bounds;
                hit.xMin -= 0.15f;
                hit.xMax += 0.15f;
                hit.yMax += 1f; // include the meter and label above the room
                if (hit.Contains(world))
                {
                    OnRoomTapped(room);
                    return;
                }
            }

            Selected = null;
        }

        void OnRoomTapped(Room room)
        {
            if (Selected != null && Selected.Room != room)
            {
                station.Send(Selected, room);
                Selected = null;
                return;
            }

            // Nobody selected (or tapping the selected crew's own room): pick its occupants
            // in turn, which is easier than hitting a small figure.
            if (room.Assigned.Count == 0)
            {
                Selected = null;
                return;
            }
            int index = Selected != null ? room.Assigned.IndexOf(Selected) + 1 : 0;
            Selected = index < room.Assigned.Count ? room.Assigned[index] : null;
        }

        // ------------------------------------------------------------------ per-frame

        void LateUpdate()
        {
            if (station == null) return;
            if (station.IsLost) Selected = null;

            float t = Time.time;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 7f);

            foreach (var c in critters) UpdateCritter(c, t);
            foreach (var v in roomViews) UpdateRoom(v, pulse);
            foreach (var v in crewViews) UpdateCrew(v, t, pulse);
        }

        void UpdateRoom(RoomView v, float pulse)
        {
            var room = v.Room;
            float dt = Time.deltaTime;

            bool failing = room.Alert == RoomAlert.Failing;
            bool warning = room.Alert == RoomAlert.Warning;
            v.Alarm.enabled = failing || warning;
            if (failing) v.Alarm.color = WithAlpha(WarningRed, 0.3f + 0.7f * pulse);
            else if (warning) v.Alarm.color = WithAlpha(Caution, 0.75f);

            v.Target.enabled = Selected != null && Selected.Room != room;
            if (v.Target.enabled) v.Target.color = WithAlpha(Color.white, 0.2f + 0.35f * pulse);

            float fill = Mathf.Clamp01(room.Meter);
            v.MeterFill.enabled = fill > 0.001f;
            var scale = v.MeterFill.transform.localScale;
            v.MeterFill.transform.localScale = new Vector3(v.MeterWidth * fill, scale.y, 1f);

            v.Status.text = failing || warning ? room.StatusText : "";
            v.Status.color = failing ? WarningRed : Caution;
            v.Rate.text = room.RateText;

            if (v.Rotor != null)
            {
                v.RotorAngle -= 600f * room.Meter * dt;
                v.Rotor.localRotation = Quaternion.Euler(0f, 0f, v.RotorAngle);
                v.Lamp.color = failing ? WithAlpha(WarningRed, 0.4f + 0.6f * pulse)
                    : room.Meter > 0f ? PowerColor : C(70, 56, 40);
            }

            if (v.Bubbles.Count > 0)
            {
                v.BubbleClock += dt * (0.15f + 0.9f * room.Meter);
                for (int i = 0; i < v.Bubbles.Count; i++)
                {
                    float p = Mathf.Repeat(v.BubbleClock + i / (float)v.Bubbles.Count, 1f);
                    float wobble = Mathf.Sin((v.BubbleClock + i) * 9f) * 0.08f;
                    v.Bubbles[i].transform.position = v.TankBottom + new Vector2(wobble + (i % 3 - 1) * 0.12f, p * v.TankHeight);
                    v.Bubbles[i].color = WithAlpha(C(210, 250, 255), room.Meter > 0.01f ? 0.85f * Mathf.Sin(p * Mathf.PI) : 0f);
                }
            }
        }

        void UpdateCrew(CrewView v, float time, float pulse)
        {
            var crew = v.Crew;
            float t = time + v.Phase;
            v.Root.position = crew.Position;

            float facing = crew.FacingRight ? 1f : -1f;
            Vector3 figurePos = Vector3.zero;
            float rotation = 0f;
            float legSwing = 0f;
            float breathe = 1f;
            bool showVisor = true;

            if (crew.IsMoving && crew.Climbing)
            {
                // Back to the camera, hand over hand.
                showVisor = false;
                figurePos.y = Mathf.Sin(t * 10f) * 0.04f;
                legSwing = Mathf.Sin(t * 10f) * 18f;
            }
            else if (crew.IsMoving)
            {
                legSwing = Mathf.Sin(t * 12f) * 30f;
                figurePos.y = Mathf.Abs(Mathf.Sin(t * 12f)) * 0.05f;
            }
            else if (crew.IsResting)
            {
                // Lying on the bunk, head on the pillow.
                facing = 1f;
                rotation = 90f;
                figurePos = new Vector3(0.5f, 0.58f, 0f);
                breathe = 1f + 0.035f * Mathf.Sin(t * 2f);
            }
            else
            {
                // Working: bob and lean into the machine. Exhausted crew slump and slow down.
                float speed = crew.IsExhausted ? 3f : 7f;
                figurePos.y = Mathf.Abs(Mathf.Sin(t * speed)) * (crew.IsExhausted ? 0.015f : 0.035f);
                float lean = crew.IsExhausted ? 12f : 4f + 4f * Mathf.Sin(t * speed);
                rotation = -facing * lean;
                legSwing = crew.IsExhausted ? 0f : Mathf.Sin(t * speed) * 4f;
            }

            v.Figure.localPosition = figurePos;
            v.Figure.localRotation = Quaternion.Euler(0f, 0f, rotation);
            v.Figure.localScale = new Vector3(facing, breathe, 1f);
            v.LegFront.localRotation = Quaternion.Euler(0f, 0f, legSwing);
            v.LegBack.localRotation = Quaternion.Euler(0f, 0f, -legSwing);
            v.Visor.enabled = showVisor;

            bool selected = Selected == crew;
            v.Selection.enabled = selected;
            if (selected)
            {
                v.Selection.color = WithAlpha(Color.white, 0.55f + 0.45f * pulse);
                v.Selection.transform.localScale = new Vector3(1.05f + 0.1f * pulse, 0.34f + 0.03f * pulse, 1f);
            }

            bool tired = crew.Stamina < station.tuning.tiredStamina;
            bool showAlert = tired && !crew.IsMoving && !crew.IsResting;
            v.Alert.enabled = showAlert;
            if (showAlert)
                v.Alert.color = crew.IsExhausted ? WithAlpha(WarningRed, 0.4f + 0.6f * pulse) : Caution;
        }

        void UpdateCritter(Critter c, float t)
        {
            c.Home.y += c.Rise * Time.deltaTime;
            if (c.Home.y > StationLayout.SurfaceY - 1f) c.Home.y = StationLayout.DeepY + 1f;

            var pos = c.Home + new Vector2(Mathf.Sin(t * 0.23f + c.Phase) * 0.7f, Mathf.Sin(t * 0.5f + c.Phase * 2f) * 0.25f);
            c.Transform.position = pos;

            // Brighter the deeper they are, with a slow individual pulse.
            float depth = Mathf.InverseLerp(StationLayout.SurfaceY - 4f, StationLayout.DeepY + 10f, pos.y);
            float glow = Mathf.Lerp(0.25f, 1f, depth) * (0.45f + 0.55f * Mathf.Pow(Mathf.Sin(t * 0.9f + c.Phase), 2f));
            for (int i = 0; i < c.Renderers.Length; i++)
                c.Renderers[i].color = WithAlpha(c.Renderers[i].color, c.BaseAlpha[i] * glow);

            if (c.Tentacles != null)
                for (int i = 0; i < c.Tentacles.Length; i++)
                    c.Tentacles[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.6f + c.Phase + i) * 12f);
        }

        // ------------------------------------------------------------------ building

        static SpriteRenderer Shape(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size, int order, float rotation = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            if (rotation != 0f) go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

            var sr = go.AddComponent<SpriteRenderer>();
            if (SpriteMaterial != null) sr.sharedMaterial = SpriteMaterial;
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (sprite.border != Vector4.zero)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
            }
            else
            {
                var b = sprite.bounds.size;
                go.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1f);
            }
            return sr;
        }

        static Transform Group(Transform parent, string name, Vector2 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        void BuildOcean()
        {
            var ocean = Group(transform, "Ocean", Vector2.zero);
            var rng = new System.Random(7);
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            float top = StationLayout.SurfaceY + 6f;
            float bottom = StationLayout.DeepY - 6f;
            Shape(ocean, "Water", Sprites.VerticalGradient(OceanTop, OceanUpper, OceanMid, OceanLow, OceanDeep, OceanDeep),
                Color.white, new Vector2(0f, (top + bottom) * 0.5f), new Vector2(60f, top - bottom), OceanOrder);
            Shape(ocean, "Sky", Sprites.Pixel, C(10, 22, 34), new Vector2(0f, StationLayout.SurfaceY + 3f), new Vector2(60f, 6f), OceanOrder + 1);
            Shape(ocean, "Surface", Sprites.Pixel, C(150, 220, 232, 140), new Vector2(0f, StationLayout.SurfaceY), new Vector2(60f, 0.08f), RayOrder);

            for (int i = 0; i < 7; i++)
                Shape(ocean, "Light Ray", Sprites.Pixel, C(170, 230, 240, 10),
                    new Vector2(R(-6f, 6f), StationLayout.SurfaceY - 11f), new Vector2(R(0.5f, 1.5f), 24f), RayOrder, R(-14f, 14f));

            Color[] glowColors = { C(90, 240, 220), C(120, 200, 255), C(160, 150, 255) };

            for (int i = 0; i < 46; i++)
            {
                var c = new Critter
                {
                    Home = new Vector2(R(-7f, 7f), R(StationLayout.DeepY + 1f, StationLayout.SurfaceY - 2f)),
                    Phase = R(0f, 10f),
                    Rise = R(0.02f, 0.08f)
                };
                c.Transform = Group(ocean, "Critter", c.Home);
                float size = R(0.07f, 0.24f);
                var color = glowColors[rng.Next(glowColors.Length)];
                var halo = Shape(c.Transform, "Halo", Sprites.Glow, WithAlpha(color, 0.5f), Vector2.zero, Vector2.one * size * 3f, CritterOrder);
                var core = Shape(c.Transform, "Core", Sprites.Circle, WithAlpha(color, 0.9f), Vector2.zero, Vector2.one * size * 0.35f, CritterOrder + 1);
                c.Renderers = new[] { halo, core };
                c.BaseAlpha = new[] { 0.5f, 0.9f };
                critters.Add(c);
            }

            // A few jellyfish: a soft dome with swaying tentacles.
            for (int i = 0; i < 6; i++)
            {
                var c = new Critter
                {
                    Home = new Vector2(R(-6f, 6f), R(StationLayout.DeepY + 4f, 4f)),
                    Phase = R(0f, 10f),
                    Rise = R(0.04f, 0.1f)
                };
                c.Transform = Group(ocean, "Jelly", c.Home);
                var color = glowColors[rng.Next(glowColors.Length)];
                float s = R(0.35f, 0.6f);
                var renderers = new List<SpriteRenderer>
                {
                    Shape(c.Transform, "Halo", Sprites.Glow, WithAlpha(color, 0.3f), Vector2.zero, Vector2.one * s * 3f, CritterOrder),
                    Shape(c.Transform, "Dome", Sprites.Circle, WithAlpha(color, 0.55f), Vector2.zero, new Vector2(s, s * 0.6f), CritterOrder + 1)
                };
                c.Tentacles = new Transform[3];
                for (int k = 0; k < 3; k++)
                {
                    var pivot = Group(c.Transform, "Tentacle", new Vector2((k - 1) * s * 0.25f, -s * 0.1f));
                    renderers.Add(Shape(pivot, "Strand", Sprites.PixelTop, WithAlpha(color, 0.4f), Vector2.zero, new Vector2(0.03f, s * 1.1f), CritterOrder + 1));
                    c.Tentacles[k] = pivot;
                }
                c.Renderers = renderers.ToArray();
                c.BaseAlpha = new float[c.Renderers.Length];
                for (int k = 0; k < c.Renderers.Length; k++) c.BaseAlpha[k] = c.Renderers[k].color.a;
                critters.Add(c);
            }
        }

        void BuildStation()
        {
            stationRoot = Group(transform, "Station", Vector2.zero);
            BuildLabelCanvas();

            float roof = StationLayout.FloorBaseY(0) + StationLayout.RoomHeight + 0.12f;
            float floorOfStation = StationLayout.FloorBaseY(station.FloorCount - 1) - 0.12f;
            float shaftMid = (roof + floorOfStation) * 0.5f;
            float shaftHeight = roof - floorOfStation;

            // Tether up to the surface, and the ladder shaft linking the floors.
            Shape(stationRoot, "Tether", Sprites.Pixel, HullDark, new Vector2(StationLayout.LadderX, (roof + StationLayout.SurfaceY) * 0.5f),
                new Vector2(0.14f, StationLayout.SurfaceY - roof), TetherOrder);
            Shape(stationRoot, "Shaft", Sprites.WorldRounded, Hull, new Vector2(StationLayout.LadderX, shaftMid),
                new Vector2(StationLayout.ShaftWidth + 0.12f, shaftHeight + 0.24f), ShaftOrder);
            Shape(stationRoot, "Shaft Inside", Sprites.Pixel, C(20, 30, 40), new Vector2(StationLayout.LadderX, shaftMid),
                new Vector2(StationLayout.ShaftWidth - 0.2f, shaftHeight - 0.1f), ShaftOrder + 1);
            foreach (float side in new[] { -0.24f, 0.24f })
                Shape(stationRoot, "Rail", Sprites.Pixel, C(104, 116, 126), new Vector2(StationLayout.LadderX + side, shaftMid),
                    new Vector2(0.06f, shaftHeight - 0.1f), ShaftOrder + 2);
            for (float y = floorOfStation + 0.25f; y < roof - 0.1f; y += 0.32f)
                Shape(stationRoot, "Rung", Sprites.Pixel, C(104, 116, 126), new Vector2(StationLayout.LadderX, y), new Vector2(0.48f, 0.05f), ShaftOrder + 2);

            for (int floor = 0; floor < station.FloorCount; floor++)
            {
                float baseY = StationLayout.FloorBaseY(floor);
                Shape(stationRoot, "Shaft Light", Sprites.Glow, WithAlpha(Amber, 0.16f), new Vector2(StationLayout.LadderX, baseY + 1.3f),
                    new Vector2(1.8f, 2.2f), ShaftOrder + 3);
                Shape(stationRoot, "Landing", Sprites.Pixel, SlabColor, new Vector2(StationLayout.LadderX, baseY + StationLayout.SlabHeight * 0.5f),
                    new Vector2(StationLayout.ShaftWidth, StationLayout.SlabHeight), SlabOrder);
            }

            foreach (var room in station.Rooms)
                roomViews.Add(BuildRoom(room));
            foreach (var crew in station.Crew)
                crewViews.Add(BuildCrew(crew));
        }

        void BuildLabelCanvas()
        {
            var go = new GameObject("Room Labels", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(stationRoot, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = LabelOrder;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(100f, 100f);
            rt.localScale = Vector3.one * LabelScale;
            rt.localPosition = Vector3.zero;
            labelRoot = rt;
        }

        Text Label(string name, Vector2 worldCenter, Vector2 worldSize, int size, Color color, TextAnchor align, FontStyle style)
        {
            var text = UI.Text(name, labelRoot, size, color, align, style);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = worldSize / LabelScale;
            rt.anchoredPosition = worldCenter / LabelScale;
            return text;
        }

        RoomView BuildRoom(Room room)
        {
            var v = new RoomView { Room = room };
            var b = room.Bounds;
            Vector2 c = b.center;
            float w = b.width, h = b.height;
            float floorY = b.y + StationLayout.SlabHeight;
            var g = Group(stationRoot, room.Name, Vector2.zero);

            // Shell: warm light spilling into the water, hull, lit back wall, ceiling lamp.
            Shape(g, "Light Spill", Sprites.Glow, WithAlpha(Amber, 0.3f), c + new Vector2(0f, 0.2f), new Vector2(w * 2f, h * 2.3f), SpillOrder);
            Shape(g, "Hull", Sprites.WorldRounded, Hull, c, new Vector2(w + 0.24f, h + 0.24f), HullOrder);
            Shape(g, "Back Wall", wallGradient, Color.white, c, new Vector2(w, h), WallOrder);
            Shape(g, "Lamp Glow", Sprites.Glow, WithAlpha(Amber, 0.35f), new Vector2(c.x, b.yMax - 0.35f), new Vector2(w * 0.9f, 1.1f), GlowOrder);
            Shape(g, "Lamp", Sprites.Pixel, LampColor, new Vector2(c.x, b.yMax - 0.05f), new Vector2(w * 0.45f, 0.07f), LampOrder);

            foreach (float side in new[] { -1f, 1f })
            {
                var p = new Vector2(c.x + side * 1.15f, b.y + 1.95f);
                Shape(g, "Window Glow", Sprites.Glow, WithAlpha(Amber, 0.3f), p, new Vector2(1.3f, 1.3f), GlowOrder);
                Shape(g, "Window Frame", Sprites.Circle, C(74, 86, 100), p, new Vector2(0.66f, 0.66f), FrameOrder);
                Shape(g, "Window", Sprites.Circle, GlassColor, p, new Vector2(0.52f, 0.52f), GlassOrder);
                Shape(g, "Glint", Sprites.Circle, C(120, 200, 220, 110), p + new Vector2(-0.1f, 0.1f), new Vector2(0.13f, 0.13f), GlassOrder + 1);
            }

            // Doorway into the ladder shaft.
            float doorX = room.Column == 0 ? b.xMax : b.xMin;
            Shape(g, "Doorway", Sprites.Pixel, C(34, 26, 20), new Vector2(doorX, floorY + 0.6f), new Vector2(0.28f, 1.2f), GlowOrder);
            Shape(g, "Floor", Sprites.Pixel, SlabColor, new Vector2(c.x, b.y + StationLayout.SlabHeight * 0.5f),
                new Vector2(w + 0.24f, StationLayout.SlabHeight), SlabOrder);

            Color meterColor;
            switch (room.Type)
            {
                case RoomType.Generator:
                    meterColor = PowerColor;
                    BuildGenerator(v, g, c.x, floorY);
                    break;
                case RoomType.Scrubber:
                    meterColor = Teal;
                    BuildScrubber(v, g, c.x, floorY, b.yMax);
                    break;
                default:
                    meterColor = RestColor;
                    BuildQuarters(room, g, c.x, floorY);
                    break;
            }

            v.Target = Shape(g, "Target Outline", Sprites.WorldOutline, Color.white, c, new Vector2(w + 0.46f, h + 0.46f), TargetOrder);
            v.Alarm = Shape(g, "Alarm Outline", Sprites.WorldOutline, WarningRed, c, new Vector2(w + 0.5f, h + 0.5f), AlarmOrder);
            v.Target.enabled = v.Alarm.enabled = false;

            // Meter and label above the room.
            float meterY = b.yMax + 0.36f;
            v.MeterWidth = w * 0.92f;
            float left = c.x - v.MeterWidth * 0.5f;
            Shape(g, "Meter Track", Sprites.WorldRoundedSmall, C(4, 10, 18, 235), new Vector2(c.x, meterY), new Vector2(v.MeterWidth + 0.08f, 0.3f), MeterOrder);
            v.MeterFill = Shape(g, "Meter Fill", Sprites.PixelLeft, WithAlpha(meterColor, 0.85f), new Vector2(left, meterY), new Vector2(v.MeterWidth, 0.22f), MeterOrder + 1);
            v.Rate = Label("Rate", new Vector2(c.x, meterY), new Vector2(v.MeterWidth, 0.3f), 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            v.Rate.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);

            var name = Label("Name", new Vector2(c.x, meterY + 0.36f), new Vector2(w, 0.32f), 24, LabelColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            name.text = room.Name.ToUpperInvariant();
            v.Status = Label("Status", new Vector2(c.x, meterY + 0.36f), new Vector2(w, 0.32f), 24, WarningRed, TextAnchor.MiddleRight, FontStyle.Bold);
            return v;
        }

        void BuildGenerator(RoomView v, Transform g, float cx, float floorY)
        {
            Shape(g, "Cable", Sprites.Pixel, C(36, 36, 40), new Vector2(cx + 0.42f, floorY + 1.7f), new Vector2(0.08f, 1.1f), MachineOrder);
            Shape(g, "Housing", Sprites.WorldRounded, C(82, 72, 58), new Vector2(cx, floorY + 0.64f), new Vector2(1.3f, 1.28f), MachineOrder);
            Shape(g, "Face", Sprites.Circle, C(26, 28, 32), new Vector2(cx, floorY + 0.68f), new Vector2(0.98f, 0.98f), MachineOrder + 1);
            v.Rotor = Group(g, "Rotor", new Vector2(cx, floorY + 0.68f));
            Shape(v.Rotor, "Blade", Sprites.Pixel, PowerColor, Vector2.zero, new Vector2(0.84f, 0.13f), MachineOrder + 2);
            Shape(v.Rotor, "Blade", Sprites.Pixel, PowerColor, Vector2.zero, new Vector2(0.84f, 0.13f), MachineOrder + 2, 90f);
            Shape(v.Rotor, "Hub", Sprites.Circle, C(64, 64, 70), Vector2.zero, new Vector2(0.22f, 0.22f), MachineOrder + 3);
            v.Lamp = Shape(g, "Status Lamp", Sprites.Circle, PowerColor, new Vector2(cx, floorY + 1.42f), new Vector2(0.18f, 0.18f), MachineOrder + 1);
        }

        void BuildScrubber(RoomView v, Transform g, float cx, float floorY, float ceiling)
        {
            float tankHeight = 1.4f;
            foreach (float side in new[] { -0.26f, 0.26f })
                Shape(g, "Pipe", Sprites.Pixel, C(60, 76, 88), new Vector2(cx + side, (floorY + tankHeight + ceiling) * 0.5f),
                    new Vector2(0.12f, ceiling - floorY - tankHeight), MachineOrder);
            Shape(g, "Tank", Sprites.WorldRounded, C(40, 92, 106), new Vector2(cx, floorY + tankHeight * 0.5f), new Vector2(0.96f, tankHeight), MachineOrder);
            Shape(g, "Tank Window", Sprites.WorldRoundedSmall, C(70, 190, 204, 120), new Vector2(cx, floorY + tankHeight * 0.52f),
                new Vector2(0.6f, tankHeight - 0.34f), MachineOrder + 1);
            v.TankBottom = new Vector2(cx, floorY + 0.22f);
            v.TankHeight = tankHeight - 0.44f;
            for (int i = 0; i < 7; i++)
                v.Bubbles.Add(Shape(g, "Bubble", Sprites.Circle, Color.clear, v.TankBottom, new Vector2(0.09f, 0.09f), MachineOrder + 2));
        }

        void BuildQuarters(Room room, Transform g, float cx, float floorY)
        {
            foreach (var slot in room.Slots)
            {
                float x = slot.x;
                Shape(g, "Headboard", Sprites.Pixel, C(70, 52, 40), new Vector2(x - 0.62f, floorY + 0.32f), new Vector2(0.08f, 0.64f), MachineOrder);
                Shape(g, "Bed", Sprites.Pixel, C(88, 66, 50), new Vector2(x, floorY + 0.16f), new Vector2(1.26f, 0.32f), MachineOrder);
                Shape(g, "Mattress", Sprites.WorldRoundedSmall, C(196, 186, 168), new Vector2(x, floorY + 0.37f), new Vector2(1.16f, 0.14f), MachineOrder + 1);
                Shape(g, "Pillow", Sprites.WorldRoundedSmall, C(232, 228, 218), new Vector2(x - 0.42f, floorY + 0.47f), new Vector2(0.3f, 0.12f), MachineOrder + 2);
            }
            Shape(g, "Bedside Table", Sprites.Pixel, C(70, 52, 40), new Vector2(cx, floorY + 0.22f), new Vector2(0.3f, 0.44f), MachineOrder);
            Shape(g, "Bedside Lamp", Sprites.Glow, WithAlpha(Amber, 0.6f), new Vector2(cx, floorY + 0.56f), new Vector2(0.45f, 0.45f), MachineOrder + 1);
        }

        CrewView BuildCrew(CrewMember crew)
        {
            var v = new CrewView { Crew = crew, Phase = Random.Range(0f, 10f) };
            v.Root = Group(stationRoot, crew.Name, crew.Position);
            Color suit = crew.Color;
            Color dark = Color.Lerp(suit, Color.black, 0.45f);

            v.Selection = Shape(v.Root, "Selection", Sprites.Ring, Color.white, new Vector2(0f, 0.02f), new Vector2(1.05f, 0.34f), CrewOrder);
            v.Selection.enabled = false;

            v.Figure = Group(v.Root, "Figure", Vector2.zero);
            v.LegBack = Shape(v.Figure, "Leg Back", Sprites.PixelTop, dark, new Vector2(-0.07f, 0.3f), new Vector2(0.11f, 0.3f), CrewOrder + 1).transform;
            Shape(v.Figure, "Pack", Sprites.WorldRoundedSmall, dark, new Vector2(-0.2f, 0.54f), new Vector2(0.14f, 0.34f), CrewOrder + 1);
            v.Body = Shape(v.Figure, "Body", Sprites.WorldRoundedSmall, suit, new Vector2(0f, 0.5f), new Vector2(0.38f, 0.44f), CrewOrder + 2);
            v.LegFront = Shape(v.Figure, "Leg Front", Sprites.PixelTop, Color.Lerp(suit, Color.black, 0.3f), new Vector2(0.07f, 0.3f), new Vector2(0.11f, 0.3f), CrewOrder + 3).transform;
            Shape(v.Figure, "Helmet", Sprites.Circle, C(228, 236, 242), new Vector2(0.02f, 0.88f), new Vector2(0.36f, 0.36f), CrewOrder + 4);
            v.Visor = Shape(v.Figure, "Visor", Sprites.WorldRoundedSmall, C(24, 72, 94), new Vector2(0.09f, 0.89f), new Vector2(0.2f, 0.14f), CrewOrder + 5);

            v.Alert = Shape(v.Root, "Tired", Sprites.Circle, Caution, new Vector2(0f, 1.3f), new Vector2(0.2f, 0.2f), CrewOrder + 6);
            v.Alert.enabled = false;
            return v;
        }
    }
}
