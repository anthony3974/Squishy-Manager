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
        static readonly Color RepairColor = C(236, 236, 228);
        static readonly Color SmokeColor = C(150, 152, 158);
        static readonly Color SparkColor = C(255, 236, 170);
        static readonly Color ToolColor = C(190, 200, 210);

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
            public Color MeterColor;

            // Breakdown effects (machines only).
            public Transform Machine;
            public Vector2 SmokeOrigin;
            public float SmokeClock;
            public readonly List<SpriteRenderer> Smoke = new List<SpriteRenderer>();
            public readonly List<Spark> Sparks = new List<Spark>();
        }

        class Spark
        {
            public SpriteRenderer Renderer;
            public int Cycle = int.MinValue;
            public Vector2 Offset, Velocity;
        }

        class TankView
        {
            public EmergencyTank Tank;
            public SpriteRenderer Fill, Glow;
            public float FillWidth, FillHeight;
            public Vector2 Valve;
            public float Clock;
            public readonly List<SpriteRenderer> Bubbles = new List<SpriteRenderer>();
        }

        class CrewView
        {
            public CrewMember Crew;
            public Transform Root, Figure, LegFront, LegBack;
            public SpriteRenderer Body, Visor, Selection, Alert;
            public GameObject Cooldown;
            public Transform CooldownFill;
            public float Phase;

            // Standing job: a small badge with the job room's symbol.
            public Transform JobBadge;
            public SpriteRenderer JobRing;
            public GameObject GeneratorGlyph, ScrubberGlyph, QuartersGlyph;

            // Fainted: circling stars and a wake-up bar.
            public GameObject Faint;
            public Transform[] FaintStars;
            public Transform WakeBar, WakeFill;

            // Thought bubble: a rounded cloud with two trailing dots, and its text on the label canvas.
            public Transform Bubble;
            public SpriteRenderer BubbleBack;
            public SpriteRenderer[] BubbleDots;
            public Text BubbleText;
            public float ThoughtAge, ThoughtLife, BubbleWidth;
            public string LastThought;

            public Text NameTag;
        }

        /// <summary>A short burst of sparks and a rising "Fixed!" over a just-repaired machine.</summary>
        class RepairPopup
        {
            public Vector2 Origin;
            public float Age;
            public readonly List<SpriteRenderer> Sparks = new List<SpriteRenderer>();
            public readonly List<Vector2> Velocities = new List<Vector2>();
            public Text Text;
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
        readonly List<TankView> tankViews = new List<TankView>();
        readonly List<Critter> critters = new List<Critter>();
        readonly List<RepairPopup> popups = new List<RepairPopup>();
        readonly System.Random rng = new System.Random();
        /// <summary>Seconds until the next thought bubble. The first comes early so the crew feel alive at once.</summary>
        float thoughtTimer = 5f;

        static readonly Color BubbleColor = C(232, 238, 242);
        static readonly Color BubbleTextColor = C(22, 32, 44);
        const float ThoughtSeconds = 4.5f;

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
            station.RepairFinished += OnRepairFinished;
        }

        void OnDestroy()
        {
            if (station == null) return;
            station.Rebuilt -= Rebuild;
            station.RepairFinished -= OnRepairFinished;
        }

        void Rebuild()
        {
            Selected = null;
            if (stationRoot != null) Destroy(stationRoot.gameObject);
            roomViews.Clear();
            crewViews.Clear();
            tankViews.Clear();
            popups.Clear();
            BuildStation();
        }

        // ------------------------------------------------------------------ input

        /// <summary>Called by StationCamera for a tap (not a drag) at a world position.</summary>
        public void HandleTap(Vector2 world)
        {
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

        /// <summary>
        /// Would tapping this room give the selected crew member an order? True for any room
        /// they aren't in, including their own job while they're resting (pulls them out of bed).
        /// Tapping the room they're in cycles through its occupants instead.
        /// </summary>
        bool IsOrder(Room room) => Selected != null && Selected.Room != room;

        void OnRoomTapped(Room room)
        {
            if (IsOrder(room))
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

            float t = Time.time;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 7f);

            foreach (var c in critters) UpdateCritter(c, t);
            foreach (var v in roomViews) UpdateRoom(v, t, pulse);
            UpdateThoughts(Time.deltaTime);
            foreach (var v in crewViews) UpdateCrew(v, t, pulse);
            foreach (var v in tankViews) UpdateTank(v, pulse);
            UpdatePopups(Time.deltaTime);
        }

        // ------------------------------------------------------------------ thoughts

        /// <summary>
        /// One bubble at a time. While something is wrong, thoughts come more often and usually
        /// hint at it; otherwise it's small talk every half minute or so.
        /// </summary>
        void UpdateThoughts(float dt)
        {
            foreach (var v in crewViews)
                if (v.ThoughtLife > 0f) v.ThoughtAge += dt;

            thoughtTimer -= dt;
            if (thoughtTimer > 0f) return;
            foreach (var v in crewViews)
                if (v.ThoughtLife > 0f && v.ThoughtAge < v.ThoughtLife)
                {
                    thoughtTimer = 1.5f;
                    return;
                }

            var hints = new List<CrewView>();
            var awake = new List<CrewView>();
            foreach (var v in crewViews)
            {
                if (v.Crew.IsFainted) continue;
                awake.Add(v);
                var hint = CrewThoughts.Hint(station, v.Crew);
                if (hint != null && hint != v.LastThought) hints.Add(v);
            }
            if (awake.Count == 0)
            {
                thoughtTimer = 3f;
                return;
            }

            if (hints.Count > 0 && rng.NextDouble() < 0.75)
            {
                var v = hints[rng.Next(hints.Count)];
                ShowThought(v, CrewThoughts.Hint(station, v.Crew));
                thoughtTimer = Range(10f, 16f);
            }
            else
            {
                var v = awake[rng.Next(awake.Count)];
                ShowThought(v, CrewThoughts.Flavor(v.Crew, rng));
                thoughtTimer = Range(18f, 30f);
            }
        }

        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        void ShowThought(CrewView v, string text)
        {
            v.BubbleText.text = text;
            v.LastThought = text;
            v.ThoughtAge = 0f;
            v.ThoughtLife = ThoughtSeconds;
            v.BubbleWidth = Mathf.Max(0.5f, v.BubbleText.preferredWidth * LabelScale + 0.3f);
            v.BubbleBack.size = new Vector2(v.BubbleWidth, 0.4f);
        }

        void UpdateThoughtBubble(CrewView v, bool lying, bool fainted)
        {
            bool showing = v.ThoughtLife > 0f && v.ThoughtAge < v.ThoughtLife && !fainted;
            v.Bubble.gameObject.SetActive(showing);
            v.BubbleText.enabled = showing;
            if (!showing) return;

            // Fade in quickly, hold, fade out.
            float alpha = Mathf.Clamp01(Mathf.Min(v.ThoughtAge / 0.25f, (v.ThoughtLife - v.ThoughtAge) / 0.5f));
            float lift = 0.06f * Mathf.Clamp01(v.ThoughtAge / 0.25f);
            // Keep the cloud on screen for crew standing near the station's outer walls.
            float edge = StationLayout.ViewWidth * 0.5f - 0.15f - v.BubbleWidth * 0.5f;
            float centerX = Mathf.Clamp(v.Crew.Position.x + 0.12f, -edge, edge);
            Vector2 offset = new Vector2(centerX - v.Crew.Position.x, (lying ? 1.5f : 1.9f) + lift);
            v.Bubble.localPosition = new Vector3(0f, offset.y - 1.9f, 0f);
            v.BubbleBack.transform.localPosition = new Vector3(offset.x, 1.9f, 0f);
            v.BubbleBack.color = WithAlpha(BubbleColor, 0.95f * alpha);
            foreach (var dot in v.BubbleDots) dot.color = WithAlpha(BubbleColor, 0.9f * alpha);
            v.BubbleText.color = WithAlpha(BubbleTextColor, alpha);
            v.BubbleText.rectTransform.anchoredPosition = (v.Crew.Position + offset) / LabelScale;
        }

        // ------------------------------------------------------------------ rewards

        void OnRepairFinished(Room room)
        {
            RoomView view = null;
            foreach (var v in roomViews) if (v.Room == room) view = v;
            if (view == null) return;

            var popup = new RepairPopup { Origin = view.SmokeOrigin };
            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f + Range(-0.2f, 0.2f);
                popup.Velocities.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f + 0.3f) * Range(0.6f, 1f));
                popup.Sparks.Add(Shape(stationRoot, "Sparkle", Sprites.Circle, i % 2 == 0 ? SparkColor : Teal, popup.Origin,
                    new Vector2(0.09f, 0.09f), MeterOrder + 2));
            }
            popup.Text = Label("Fixed", popup.Origin, new Vector2(2f, 0.4f), 26, SparkColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            popup.Text.text = "Fixed!";
            popup.Text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            popups.Add(popup);

            // Whoever did the work is pleased with it.
            foreach (var v in crewViews)
                if (v.Crew.Room == room && v.Crew.RepairedRoom == room && v.Crew.RepairCooldown > 0f && !v.Crew.IsFainted)
                {
                    ShowThought(v, CrewThoughts.Repaired(rng));
                    thoughtTimer = Mathf.Max(thoughtTimer, 6f);
                    break;
                }
        }

        void UpdatePopups(float dt)
        {
            const float life = 1.6f;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.Age += dt;
                if (p.Age >= life)
                {
                    foreach (var s in p.Sparks) Destroy(s.gameObject);
                    Destroy(p.Text.gameObject);
                    popups.RemoveAt(i);
                    continue;
                }

                // Sparks fly out fast and slow down; the text floats up and fades in the second half.
                float burst = 1f - Mathf.Pow(1f - Mathf.Clamp01(p.Age / 0.7f), 3f);
                float fade = 1f - Mathf.Clamp01(p.Age / 0.9f);
                for (int k = 0; k < p.Sparks.Count; k++)
                {
                    p.Sparks[k].transform.position = p.Origin + p.Velocities[k] * burst;
                    p.Sparks[k].color = WithAlpha(p.Sparks[k].color, fade);
                    float size = 0.09f * (1f - 0.5f * burst);
                    p.Sparks[k].transform.localScale = new Vector3(size, size, 1f);
                }
                float textAlpha = 1f - Mathf.Clamp01((p.Age - life * 0.5f) / (life * 0.5f));
                p.Text.rectTransform.anchoredPosition = (p.Origin + new Vector2(0f, 0.5f + 0.5f * p.Age / life)) / LabelScale;
                p.Text.color = WithAlpha(SparkColor, textAlpha);
            }
        }

        void UpdateRoom(RoomView v, float time, float pulse)
        {
            var room = v.Room;
            float dt = Time.deltaTime;

            bool failing = room.Alert == RoomAlert.Failing;
            bool warning = room.Alert == RoomAlert.Warning;
            v.Alarm.enabled = failing || warning;
            if (failing) v.Alarm.color = WithAlpha(WarningRed, 0.3f + 0.7f * pulse);
            else if (warning) v.Alarm.color = WithAlpha(Caution, 0.75f);

            v.Target.enabled = IsOrder(room);
            if (v.Target.enabled) v.Target.color = WithAlpha(Color.white, 0.2f + 0.35f * pulse);

            // While broken the meter tracks repair progress instead of output.
            float fill = Mathf.Clamp01(room.Meter);
            v.MeterFill.enabled = fill > 0.001f;
            v.MeterFill.color = WithAlpha(room.Broken ? RepairColor : v.MeterColor, 0.85f);
            var scale = v.MeterFill.transform.localScale;
            v.MeterFill.transform.localScale = new Vector3(v.MeterWidth * fill, scale.y, 1f);

            v.Status.text = failing || warning ? room.StatusText : "";
            v.Status.color = failing ? WarningRed : Caution;
            v.Rate.text = room.RateText;

            if (v.Rotor != null)
            {
                v.RotorAngle -= 600f * room.Activity * dt;
                v.Rotor.localRotation = Quaternion.Euler(0f, 0f, v.RotorAngle);
                v.Lamp.color = failing || room.Broken ? WithAlpha(WarningRed, 0.4f + 0.6f * pulse)
                    : room.Rattling ? WithAlpha(Caution, 0.5f + 0.5f * pulse)
                    : room.Activity > 0f ? PowerColor : C(70, 56, 40);
            }

            if (v.Bubbles.Count > 0)
            {
                v.BubbleClock += dt * (0.15f + 0.9f * room.Activity);
                for (int i = 0; i < v.Bubbles.Count; i++)
                {
                    float p = Mathf.Repeat(v.BubbleClock + i / (float)v.Bubbles.Count, 1f);
                    float wobble = Mathf.Sin((v.BubbleClock + i) * 9f) * 0.08f;
                    v.Bubbles[i].transform.position = v.TankBottom + new Vector2(wobble + (i % 3 - 1) * 0.12f, p * v.TankHeight);
                    v.Bubbles[i].color = WithAlpha(C(210, 250, 255), room.Activity > 0.01f ? 0.85f * Mathf.Sin(p * Mathf.PI) : 0f);
                }
            }

            if (v.Machine != null) UpdateBreakdownEffects(v, time, dt);
        }

        /// <summary>Rattling machines shake and puff a little smoke; broken ones smoke and spark.</summary>
        void UpdateBreakdownEffects(RoomView v, float time, float dt)
        {
            var room = v.Room;
            bool repairing = room.Broken && room.Repairers > 0;

            float shake = room.Rattling ? 0.035f : repairing ? 0.012f : 0f;
            v.Machine.localPosition = shake > 0f
                ? new Vector3(Mathf.Sin(time * 53f) * shake, Mathf.Sin(time * 41f + 1f) * shake * 0.6f, 0f)
                : Vector3.zero;

            float smoke = room.Broken ? (repairing ? 0.6f : 1f) : room.Rattling ? 0.45f : 0f;
            v.SmokeClock += dt * 0.45f;
            for (int i = 0; i < v.Smoke.Count; i++)
            {
                var puff = v.Smoke[i];
                float p = Mathf.Repeat(v.SmokeClock + i / (float)v.Smoke.Count, 1f);
                float drift = Mathf.Sin((v.SmokeClock + i) * 3f) * 0.12f + (i % 2 == 0 ? -0.18f : 0.18f) * p;
                puff.transform.position = v.SmokeOrigin + new Vector2(drift, p * 0.85f);
                float size = 0.16f + 0.34f * p;
                puff.transform.localScale = new Vector3(size, size, 1f);
                puff.color = WithAlpha(SmokeColor, smoke * 0.5f * Mathf.Sin(p * Mathf.PI));
            }

            // Sparks: short-lived bright dots that jump out of the machine. Busier while being repaired.
            float rate = repairing ? 3.2f : room.Broken ? 1.1f : 0f;
            for (int i = 0; i < v.Sparks.Count; i++)
            {
                var spark = v.Sparks[i];
                if (rate <= 0f)
                {
                    spark.Renderer.enabled = false;
                    continue;
                }
                float phase = time * rate + i * 0.37f;
                int cycle = Mathf.FloorToInt(phase);
                float life = (phase - cycle) / 0.3f;
                if (cycle != spark.Cycle)
                {
                    spark.Cycle = cycle;
                    spark.Offset = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.1f));
                    spark.Velocity = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.2f, 0.7f));
                }
                spark.Renderer.enabled = life < 1f;
                if (life >= 1f) continue;
                spark.Renderer.transform.position = v.SmokeOrigin + spark.Offset + spark.Velocity * life * 0.3f
                    + new Vector2(0f, -0.4f * life * life * 0.3f);
                spark.Renderer.color = WithAlpha(SparkColor, 1f - life);
            }
        }

        void UpdateTank(TankView v, float pulse)
        {
            var tank = v.Tank;
            bool venting = station.VentingTank == tank;
            bool refilling = station.RefillingTank == tank;

            v.Fill.enabled = tank.Fill > 0.01f;
            v.Fill.transform.localScale = new Vector3(v.FillWidth, v.FillHeight * Mathf.Clamp01(tank.Fill), 1f);
            v.Fill.color = venting ? Color.Lerp(Teal, Color.white, 0.4f * pulse)
                : WithAlpha(Teal, tank.IsFull ? 0.9f : 0.55f);

            float slow = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f);
            v.Glow.color = WithAlpha(Teal, venting ? 0.35f + 0.35f * pulse : refilling ? 0.08f + 0.1f * slow : 0f);

            v.Clock += Time.deltaTime * 1.6f;
            for (int i = 0; i < v.Bubbles.Count; i++)
            {
                float p = Mathf.Repeat(v.Clock + i / (float)v.Bubbles.Count, 1f);
                float wobble = Mathf.Sin((v.Clock + i) * 8f) * 0.06f;
                v.Bubbles[i].transform.position = v.Valve + new Vector2(wobble, p * 0.55f);
                v.Bubbles[i].color = WithAlpha(C(210, 250, 255), venting ? 0.9f * Mathf.Sin(p * Mathf.PI) : 0f);
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
            // Fainted crew lie where they dropped: on the bunk if resting, on the floor otherwise.
            bool fainted = crew.IsFainted;
            bool faintedInBed = fainted && crew.IsResting;
            // Off duty and fully rested: up out of bed, idling beside it.
            bool offDutyUp = crew.IsResting && !fainted && crew.Job == crew.Room && crew.Stamina >= Station.MaxStamina - 0.01f;
            // At a machine but nothing to do: broken and waiting, or a scrubber with no power.
            bool stalled = crew.Room.Type == RoomType.Scrubber && station.PowerSupply < 0.02f;
            bool lying = fainted || (crew.IsResting && !offDutyUp);

            if (fainted)
            {
                facing = 1f;
                rotation = 90f;
                figurePos = new Vector3(0.5f, faintedInBed ? 0.58f : 0.2f, 0f);
                breathe = 1f + 0.02f * Mathf.Sin(t * 1.2f);
                showVisor = false;
            }
            else if (crew.IsMoving && crew.Climbing)
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
            else if (offDutyUp)
            {
                Idle(t, ref facing, ref figurePos, ref breathe, ref legSwing);
            }
            else if (crew.IsResting)
            {
                // Lying on the bunk, head on the pillow.
                facing = 1f;
                rotation = 90f;
                figurePos = new Vector3(0.5f, 0.58f, 0f);
                breathe = 1f + 0.035f * Mathf.Sin(t * 2f);
            }
            else if (crew.IsRepairing)
            {
                // Repairing: quick, hard hammering at the machine.
                float speed = crew.IsExhausted ? 7f : 14f;
                float hit = Mathf.Abs(Mathf.Sin(t * speed));
                figurePos.y = hit * 0.025f;
                rotation = -facing * (8f + 10f * hit);
                legSwing = Mathf.Sin(t * speed) * 3f;
            }
            else if (crew.Room.Broken || stalled)
            {
                // Waiting by a broken machine for their repair cooldown, or for power.
                Idle(t, ref facing, ref figurePos, ref breathe, ref legSwing);
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

            // The cooldown only matters at a machine, so the wrench shows in the room they just
            // fixed or at a broken machine they're waiting to repair, not everywhere they go.
            bool cooling = crew.RepairCooldown > 0f && !crew.IsMoving && !fainted
                && (crew.Room == crew.RepairedRoom || crew.Room.Broken);
            v.Cooldown.SetActive(cooling);
            if (cooling)
            {
                float left = Mathf.Clamp01(crew.RepairCooldown / Mathf.Max(0.01f, station.tuning.repairCooldown));
                v.CooldownFill.localScale = new Vector3(0.4f * left, v.CooldownFill.localScale.y, 1f);
            }

            bool tired = crew.Stamina < station.tuning.tiredStamina;
            bool showAlert = tired && !crew.IsMoving && !crew.IsResting && !fainted;
            v.Alert.enabled = showAlert;
            if (showAlert)
                v.Alert.color = crew.IsExhausted ? WithAlpha(WarningRed, 0.4f + 0.6f * pulse) : Caution;

            // Fainted: little stars circle the head and a bar fills as they come round.
            v.Faint.SetActive(fainted);
            if (fainted)
            {
                Vector2 head = new Vector2(-0.38f, faintedInBed ? 0.6f : 0.22f);
                for (int i = 0; i < v.FaintStars.Length; i++)
                {
                    float a = time * 2.6f + i * Mathf.PI * 2f / v.FaintStars.Length;
                    v.FaintStars[i].localPosition = head + new Vector2(Mathf.Cos(a) * 0.17f, 0.3f + Mathf.Sin(a) * 0.05f);
                }
                float awake = 1f - Mathf.Clamp01(crew.FaintTime / Mathf.Max(0.01f, station.tuning.faintSeconds));
                v.WakeBar.localPosition = new Vector3(0.05f, faintedInBed ? 1.08f : 0.72f, 0f);
                v.WakeFill.localScale = new Vector3(0.4f * awake, v.WakeFill.localScale.y, 1f);
            }

            // Job badge: shown unless fainted, so a resting crew member still says where they'll go back to.
            v.JobBadge.gameObject.SetActive(!fainted);
            var job = crew.Job.Type;
            v.GeneratorGlyph.SetActive(job == RoomType.Generator);
            v.ScrubberGlyph.SetActive(job == RoomType.Scrubber);
            v.QuartersGlyph.SetActive(job == RoomType.Quarters);
            v.JobRing.color = JobColor(job);
            // Over the lying body while resting, over the helmet otherwise.
            v.JobBadge.localPosition = lying ? new Vector3(0.05f, 1.02f, 0f) : new Vector3(0f, 1.4f, 0f);

            UpdateThoughtBubble(v, lying, fainted);

            v.NameTag.enabled = selected;
            if (selected) v.NameTag.rectTransform.anchoredPosition = (crew.Position + new Vector2(0f, -0.26f)) / LabelScale;
        }

        /// <summary>
        /// Not working: stand and breathe, tap a foot, glance the other way, stretch. A 7 s loop,
        /// offset per crew member so they don't move in sync.
        /// </summary>
        static void Idle(float t, ref float facing, ref Vector3 figurePos, ref float breathe, ref float legSwing)
        {
            float cycle = Mathf.Repeat(t, 7f);
            breathe = 1f + 0.02f * Mathf.Sin(t * 2.2f);
            if (cycle < 2.4f)
            {
                legSwing = Mathf.Max(0f, Mathf.Sin(t * 7f)) * 8f;
            }
            else if (cycle < 4.2f)
            {
                facing = -facing;
            }
            else if (cycle > 5f && cycle < 6.3f)
            {
                float s = Mathf.Sin((cycle - 5f) / 1.3f * Mathf.PI);
                breathe += 0.09f * s;
                figurePos.y = 0.04f * s;
            }
        }

        static Color JobColor(RoomType type) =>
            type == RoomType.Generator ? PowerColor : type == RoomType.Scrubber ? Teal : RestColor;

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

            BuildTanks(floorOfStation - 0.12f);

            foreach (var room in station.Rooms)
                roomViews.Add(BuildRoom(room));
            foreach (var crew in station.Crew)
                crewViews.Add(BuildCrew(crew));
        }

        /// <summary>A rack of emergency oxygen canisters hanging under the bottom of the station.</summary>
        void BuildTanks(float stationBottom)
        {
            int count = station.Tanks.Count;
            if (count == 0) return;

            const float spacing = 0.46f, height = 0.8f;
            float top = stationBottom - 0.14f;
            float cy = top - height * 0.5f;
            var g = Group(stationRoot, "Emergency Tanks", Vector2.zero);
            Shape(g, "Rack", Sprites.Pixel, HullDark, new Vector2(StationLayout.LadderX, stationBottom - 0.06f),
                new Vector2(count * spacing + 0.2f, 0.12f), HullOrder);

            for (int i = 0; i < count; i++)
            {
                float x = StationLayout.LadderX + (i - (count - 1) * 0.5f) * spacing;
                var v = new TankView { Tank = station.Tanks[i], FillWidth = 0.2f, FillHeight = height - 0.18f };
                v.Glow = Shape(g, "Glow", Sprites.Glow, Color.clear, new Vector2(x, cy), new Vector2(1.1f, 1.6f), SpillOrder);
                Shape(g, "Hanger", Sprites.Pixel, HullDark, new Vector2(x, top + 0.04f), new Vector2(0.06f, 0.12f), HullOrder);
                Shape(g, "Shell", Sprites.WorldRoundedSmall, C(36, 70, 84), new Vector2(x, cy), new Vector2(0.34f, height), MachineOrder);
                Shape(g, "Inside", Sprites.WorldRoundedSmall, C(8, 22, 30), new Vector2(x, cy), new Vector2(0.24f, height - 0.14f), MachineOrder + 1);
                v.Fill = Shape(g, "Fill", Sprites.PixelBottom, Teal, new Vector2(x, cy - v.FillHeight * 0.5f),
                    new Vector2(v.FillWidth, v.FillHeight), MachineOrder + 2);
                Shape(g, "Strap", Sprites.Pixel, HullDark, new Vector2(x, top - 0.2f), new Vector2(0.38f, 0.05f), MachineOrder + 3);
                Shape(g, "Strap", Sprites.Pixel, HullDark, new Vector2(x, top - height + 0.2f), new Vector2(0.38f, 0.05f), MachineOrder + 3);
                v.Valve = new Vector2(x + 0.1f, top - 0.06f);
                Shape(g, "Valve", Sprites.Circle, C(104, 116, 126), v.Valve, new Vector2(0.1f, 0.1f), MachineOrder + 3);
                for (int k = 0; k < 4; k++)
                    v.Bubbles.Add(Shape(g, "Bubble", Sprites.Circle, Color.clear, v.Valve, new Vector2(0.08f, 0.08f), MachineOrder + 4));
                tankViews.Add(v);
            }

            var label = Label("Tank Label", new Vector2(StationLayout.LadderX, top - height - 0.24f), new Vector2(3f, 0.3f),
                19, DimLabel, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.text = "EMERGENCY O2";
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
                    v.Machine = Group(g, "Machine", Vector2.zero);
                    BuildGenerator(v, v.Machine, c.x, floorY);
                    v.SmokeOrigin = new Vector2(c.x, floorY + 1.3f);
                    break;
                case RoomType.Scrubber:
                    meterColor = Teal;
                    v.Machine = Group(g, "Machine", Vector2.zero);
                    BuildScrubber(v, v.Machine, c.x, floorY, b.yMax);
                    v.SmokeOrigin = new Vector2(c.x, floorY + 1.45f);
                    break;
                default:
                    meterColor = RestColor;
                    BuildQuarters(room, g, c.x, floorY);
                    break;
            }
            v.MeterColor = meterColor;

            if (v.Machine != null)
            {
                for (int i = 0; i < 6; i++)
                    v.Smoke.Add(Shape(g, "Smoke", Sprites.Circle, Color.clear, v.SmokeOrigin, new Vector2(0.2f, 0.2f), MachineOrder + 5));
                for (int i = 0; i < 5; i++)
                {
                    var spark = Shape(g, "Spark", Sprites.Circle, SparkColor, v.SmokeOrigin, new Vector2(0.07f, 0.07f), MachineOrder + 6);
                    spark.enabled = false;
                    v.Sparks.Add(new Spark { Renderer = spark });
                }
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

            v.Alert = Shape(v.Root, "Tired", Sprites.Circle, Caution, new Vector2(0.27f, 1.4f), new Vector2(0.16f, 0.16f), CrewOrder + 6);
            v.Alert.enabled = false;

            BuildJobBadge(v);
            BuildFaint(v);
            BuildThoughtBubble(v);

            v.NameTag = Label("Name Tag", crew.Position, new Vector2(1.6f, 0.3f), 20, LabelColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            v.NameTag.text = crew.Name;
            v.NameTag.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            v.NameTag.enabled = false;

            // Repair cooldown: a little wrench and a bar that empties.
            var cooldown = Group(v.Root, "Repair Cooldown", new Vector2(0f, 1.16f));
            Shape(cooldown, "Wrench Handle", Sprites.Pixel, ToolColor, new Vector2(-0.27f, -0.02f), new Vector2(0.05f, 0.2f), CrewOrder + 6, 45f);
            Shape(cooldown, "Wrench Head", Sprites.Circle, ToolColor, new Vector2(-0.34f, 0.05f), new Vector2(0.13f, 0.13f), CrewOrder + 6);
            Shape(cooldown, "Wrench Jaw", Sprites.Pixel, C(4, 10, 18), new Vector2(-0.37f, 0.08f), new Vector2(0.05f, 0.08f), CrewOrder + 7, 45f);
            Shape(cooldown, "Track", Sprites.Pixel, C(4, 10, 18, 220), new Vector2(0.04f, 0f), new Vector2(0.42f, 0.08f), CrewOrder + 6);
            v.CooldownFill = Shape(cooldown, "Fill", Sprites.PixelLeft, ToolColor, new Vector2(-0.16f, 0f), new Vector2(0.4f, 0.05f), CrewOrder + 7).transform;
            v.Cooldown = cooldown.gameObject;
            v.Cooldown.SetActive(false);
            return v;
        }

        /// <summary>A pale cloud above the head with two small dots trailing down to it, like a comic thought.</summary>
        void BuildThoughtBubble(CrewView v)
        {
            const int order = MeterOrder + 2;
            v.Bubble = Group(v.Root, "Thought", Vector2.zero);
            v.BubbleBack = Shape(v.Bubble, "Cloud", Sprites.WorldRounded, BubbleColor, new Vector2(0.12f, 1.9f), new Vector2(1f, 0.4f), order);
            v.BubbleDots = new[]
            {
                Shape(v.Bubble, "Dot", Sprites.Circle, BubbleColor, new Vector2(0.06f, 1.62f), new Vector2(0.1f, 0.1f), order),
                Shape(v.Bubble, "Dot", Sprites.Circle, BubbleColor, new Vector2(0.02f, 1.5f), new Vector2(0.06f, 0.06f), order),
            };
            v.Bubble.gameObject.SetActive(false);

            v.BubbleText = Label("Thought", Vector2.zero, new Vector2(4f, 0.3f), 20, BubbleTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            v.BubbleText.enabled = false;
        }

        /// <summary>Circling stars over the head, and a bar that fills as a fainted crew member comes round.</summary>
        void BuildFaint(CrewView v)
        {
            const int order = CrewOrder + 6;
            var faint = Group(v.Root, "Fainted", Vector2.zero);
            v.FaintStars = new Transform[3];
            for (int i = 0; i < v.FaintStars.Length; i++)
                v.FaintStars[i] = Shape(faint, "Star", Sprites.Circle, SparkColor, Vector2.zero, new Vector2(0.08f, 0.08f), order).transform;

            v.WakeBar = Group(faint, "Wake Bar", Vector2.zero);
            Shape(v.WakeBar, "Track", Sprites.Pixel, C(4, 10, 18, 220), Vector2.zero, new Vector2(0.44f, 0.08f), order);
            v.WakeFill = Shape(v.WakeBar, "Fill", Sprites.PixelLeft, RestColor, new Vector2(-0.2f, 0f), new Vector2(0.4f, 0.05f), order + 1).transform;
            v.Faint = faint.gameObject;
            v.Faint.SetActive(false);
        }

        /// <summary>
        /// A small dark disc with a coloured rim and the job room's symbol, echoing the room's own
        /// machine: generator rotor, scrubber bubbles, a moon for Quarters.
        /// </summary>
        void BuildJobBadge(CrewView v)
        {
            const int order = CrewOrder + 6;
            var badge = Group(v.Root, "Job Badge", new Vector2(0f, 1.4f));
            v.JobBadge = badge;
            v.JobRing = Shape(badge, "Rim", Sprites.Circle, Color.white, Vector2.zero, new Vector2(0.32f, 0.32f), order);
            var back = C(8, 16, 26);
            Shape(badge, "Back", Sprites.Circle, back, Vector2.zero, new Vector2(0.26f, 0.26f), order + 1);

            var gen = Group(badge, "Generator", Vector2.zero);
            Shape(gen, "Blade", Sprites.Pixel, PowerColor, Vector2.zero, new Vector2(0.18f, 0.045f), order + 2, 45f);
            Shape(gen, "Blade", Sprites.Pixel, PowerColor, Vector2.zero, new Vector2(0.18f, 0.045f), order + 2, -45f);
            Shape(gen, "Hub", Sprites.Circle, C(64, 64, 70), Vector2.zero, new Vector2(0.06f, 0.06f), order + 3);
            v.GeneratorGlyph = gen.gameObject;

            var scrub = Group(badge, "Scrubber", Vector2.zero);
            Shape(scrub, "Bubble", Sprites.Circle, Teal, new Vector2(-0.04f, -0.04f), new Vector2(0.09f, 0.09f), order + 2);
            Shape(scrub, "Bubble", Sprites.Circle, Teal, new Vector2(0.05f, 0.01f), new Vector2(0.065f, 0.065f), order + 2);
            Shape(scrub, "Bubble", Sprites.Circle, Teal, new Vector2(-0.01f, 0.07f), new Vector2(0.05f, 0.05f), order + 2);
            v.ScrubberGlyph = scrub.gameObject;

            var rest = Group(badge, "Quarters", Vector2.zero);
            Shape(rest, "Moon", Sprites.Circle, RestColor, new Vector2(-0.01f, 0f), new Vector2(0.15f, 0.15f), order + 2);
            Shape(rest, "Shadow", Sprites.Circle, back, new Vector2(0.04f, 0.035f), new Vector2(0.13f, 0.13f), order + 3);
            v.QuartersGlyph = rest.gameObject;
        }
    }
}
