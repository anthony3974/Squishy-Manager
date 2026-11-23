using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DeepStation
{
    /// <summary>
    /// The build sheet. Tapping an empty spot (or "dig down" below the station) slides this up
    /// from the bottom with one row per room type, its cost, and what it does. The station is
    /// paused while it's open. Tap outside the sheet, Cancel, or Back to close.
    /// </summary>
    [RequireComponent(typeof(Station))]
    public class BuildMenu : MonoBehaviour
    {
        static readonly Color Teal = new Color32(84, 214, 228, 255);
        static readonly Color PowerColor = new Color32(255, 190, 70, 255);
        static readonly Color RestColor = new Color32(214, 224, 236, 255);
        static readonly Color TextColor = new Color32(222, 234, 244, 255);
        static readonly Color DimText = new Color32(150, 172, 190, 255);
        static readonly Color SheetColor = new Color32(6, 18, 30, 248);
        static readonly Color RowColor = new Color32(16, 38, 56, 255);
        static readonly Color IconBack = new Color32(8, 16, 26, 255);

        const float SheetHeight = 930f;
        const float SlideSeconds = 0.22f;

        class Option
        {
            public RoomType Type;
            public Button Button;
            public CanvasGroup Group;
            public Text Description, Cost;
        }

        Station station;
        GameObject root;
        RectTransform sheet;
        Image backdrop;
        Text title, subtitle;
        Option[] options;
        float openAge;
        float savedTimeScale = 1f;

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>The spot the menu is open for.</summary>
        public int Floor { get; private set; }
        public int Column { get; private set; }

        void Start()
        {
            station = GetComponent<Station>();
            EnsureEventSystem();
            Build();
            root.SetActive(false);
            station.Rebuilt += Close;
        }

        void OnDestroy()
        {
            if (IsOpen) Time.timeScale = savedTimeScale;
            if (station != null) station.Rebuilt -= Close;
        }

        public void Open(int floor, int column)
        {
            if (root == null || !station.CanBuildAt(floor, column)) return;
            Floor = floor;
            Column = column;
            if (!IsOpen)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                openAge = 0f;
            }
            root.SetActive(true);
            Refresh();
            Animate();
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            Time.timeScale = savedTimeScale;
        }

        void Choose(RoomType type)
        {
            if (!station.CanAfford(Floor)) return;
            // Unpause first so the new room's first tick isn't stuck at zero time.
            Close();
            station.Build(type, Floor, Column);
        }

        void Update()
        {
            if (!IsOpen) return;
            openAge += Time.unscaledDeltaTime;
            Animate();

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close(); // Android Back
        }

        void Animate()
        {
            float t = Mathf.Clamp01(openAge / SlideSeconds);
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(-SheetHeight - 40f, 16f, ease));
            backdrop.color = new Color(0f, 0.02f, 0.05f, 0.6f * ease);
        }

        void Refresh()
        {
            bool dig = station.IsNewFloor(Floor);
            float cost = station.BuildCost(Floor);
            bool affordable = station.CanAfford(Floor);
            var t = station.tuning;

            title.text = dig ? $"Dig out floor {Floor + 1}" : $"Build on floor {Floor + 1}";
            string costLine = dig
                ? $"{cost:0} power ({t.roomPowerCost:0} room + {t.newFloorPowerCost:0} to dig)"
                : $"{cost:0} power";
            string hazardNote = Floor >= 4 ? "  ·  <color=#8AE6F2>creatures, heat & leaks</color>"
                : Floor == 3 ? "  ·  <color=#FF9C2E>heat vents & leaks</color>"
                : Floor == 2 ? "  ·  <color=#54D6E4>pressure leaks</color>"
                : "";
            subtitle.text = $"{costLine}  ·  crew build (~{Mathf.Max(1f, t.buildSeconds):0}s)\n<color=#{ColorUtility.ToHtmlStringRGB(affordable ? DimText : PowerColor)}>You have {station.Power:0} power</color>{hazardNote}";

            foreach (var o in options)
            {
                o.Button.interactable = affordable;
                o.Group.alpha = affordable ? 1f : 0.45f;
                o.Cost.text = affordable ? $"{cost:0}\n<size=22>power</size>" : $"need\n<size=22>{cost:0} power</size>";
                o.Description.text = Describe(o.Type);
            }
        }

        string Describe(RoomType type)
        {
            var t = station.tuning;
            switch (type)
            {
                case RoomType.Generator:
                    return $"Makes power: +{t.generatorPower:0.0}/s per crew";
                case RoomType.Scrubber:
                    return $"Makes air: +{t.scrubberOxygen:0.0} O2/s per crew\nuses {t.scrubberPower:0.0} power/s each";
                default:
                    return "2 more beds, so tired crew\ndon't wait for a bed";
            }
        }

        // ------------------------------------------------------------------ building

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            // The UI module sets up its default pointer and navigation actions when enabled.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void Build()
        {
            root = new GameObject("Build Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            var rootRect = (RectTransform)root.transform;

            // Tapping anywhere outside the sheet closes it.
            backdrop = UI.Image("Backdrop", rootRect, null, new Color(0f, 0f, 0f, 0.6f), raycast: true);
            UI.Stretch(backdrop.rectTransform);
            UI.Button(backdrop.gameObject, Close);

            var safe = UI.Stretch(UI.Rect("Safe Area", rootRect));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var sheetImage = UI.Image("Sheet", safe, Sprites.RoundedSmall, SheetColor, raycast: true);
            sheet = sheetImage.rectTransform;
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0f);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.offsetMin = new Vector2(16f, 16f);
            sheet.offsetMax = new Vector2(-16f, 16f + SheetHeight);

            title = UI.Text("Title", sheet, 46, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -96f), new Vector2(-40f, -32f));

            subtitle = UI.Text("Subtitle", sheet, 28, DimText, TextAnchor.UpperLeft, FontStyle.Bold);
            UI.Place(subtitle.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -186f), new Vector2(-40f, -104f));

            options = new[]
            {
                BuildOption(RoomType.Generator, 0),
                BuildOption(RoomType.Scrubber, 1),
                BuildOption(RoomType.Quarters, 2),
            };

            var cancel = UI.Image("Cancel", sheet, Sprites.RoundedSmall, new Color32(24, 40, 54, 255), raycast: true);
            UI.Place(cancel.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(28f, 28f), new Vector2(-28f, 128f));
            UI.Button(cancel.gameObject, Close);
            var cancelText = UI.Text("Text", cancel.rectTransform, 34, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Stretch(cancelText.rectTransform);
            cancelText.text = "Cancel";
        }

        Option BuildOption(RoomType type, int index)
        {
            const float rowHeight = 172f, gap = 18f, firstTop = 208f;
            float top = firstTop + index * (rowHeight + gap);

            var o = new Option { Type = type };
            var row = UI.Image(Station.TypeName(type), sheet, Sprites.RoundedSmall, RowColor, raycast: true);
            UI.Place(row.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(28f, -top - rowHeight), new Vector2(-28f, -top));
            o.Button = UI.Button(row.gameObject, () => Choose(type));
            o.Group = row.gameObject.AddComponent<CanvasGroup>();

            BuildIcon(type, row.rectTransform);

            var name = UI.Text("Name", row.rectTransform, 38, TextColor, TextAnchor.LowerLeft, FontStyle.Bold);
            UI.Place(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(176f, rowHeight * 0.5f + 8f), new Vector2(-170f, -18f));
            name.text = Station.TypeName(type);

            o.Description = UI.Text("Description", row.rectTransform, 26, DimText, TextAnchor.UpperLeft, FontStyle.Bold);
            UI.Place(o.Description.rectTransform, Vector2.zero, Vector2.one, new Vector2(176f, 12f), new Vector2(-170f, -rowHeight * 0.5f + 2f));

            o.Cost = UI.Text("Cost", row.rectTransform, 40, PowerColor, TextAnchor.MiddleRight, FontStyle.Bold);
            UI.Place(o.Cost.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-160f, 0f), new Vector2(-32f, 0f));
            return o;
        }

        /// <summary>The same symbols as the crew job badges: rotor, bubbles, moon.</summary>
        static void BuildIcon(RoomType type, RectTransform row)
        {
            Color rim = type == RoomType.Generator ? PowerColor : type == RoomType.Scrubber ? Teal : RestColor;
            var ring = UI.Image("Icon", row, Sprites.Circle, rim);
            UI.Pin(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(112f, 112f));
            var back = UI.Image("Back", ring.rectTransform, Sprites.Circle, IconBack);
            UI.Pin(back.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
            var icon = back.rectTransform;

            switch (type)
            {
                case RoomType.Generator:
                    foreach (float angle in new[] { 45f, -45f })
                    {
                        var blade = UI.Image("Blade", icon, Sprites.Pixel, PowerColor);
                        UI.Pin(blade.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 15f));
                        blade.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    }
                    var hub = UI.Image("Hub", icon, Sprites.Circle, new Color32(64, 64, 70, 255));
                    UI.Pin(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
                    break;

                case RoomType.Scrubber:
                    Bubble(icon, new Vector2(-13f, -13f), 30f);
                    Bubble(icon, new Vector2(16f, 3f), 22f);
                    Bubble(icon, new Vector2(-3f, 23f), 16f);
                    break;

                default:
                    var moon = UI.Image("Moon", icon, Sprites.Circle, RestColor);
                    UI.Pin(moon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-3f, 0f), new Vector2(50f, 50f));
                    var shadow = UI.Image("Shadow", icon, Sprites.Circle, IconBack);
                    UI.Pin(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(13f, 11f), new Vector2(43f, 43f));
                    break;
            }
        }

        static void Bubble(RectTransform parent, Vector2 position, float size)
        {
            var b = UI.Image("Bubble", parent, Sprites.Circle, Teal);
            UI.Pin(b.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
        }
    }
}
