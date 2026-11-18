using System.Collections.Generic;
using UnityEngine;

namespace DeepStation
{
    /// <summary>
    /// The simulation: two station-wide resources (oxygen, power), six rooms, three crew.
    /// Knows nothing about UI; StationUI reads its state every frame.
    /// </summary>
    public class Station : MonoBehaviour
    {
        public const int FloorCount = 3;
        public const int ColumnCount = 2;
        public const float MaxStamina = 100f;

        public StationTuning tuning = new StationTuning();

        public readonly List<Room> Rooms = new List<Room>();
        public readonly List<CrewMember> Crew = new List<CrewMember>();

        public float Oxygen { get; private set; }
        public float Power { get; private set; }
        /// <summary>Net oxygen per second (made minus breathed).</summary>
        public float OxygenRate { get; private set; }
        /// <summary>Net power per second (made minus wanted), even when the bank is empty.</summary>
        public float PowerRate { get; private set; }
        /// <summary>1 when every room gets the power it wants, lower during a blackout.</summary>
        public float PowerSupply { get; private set; } = 1f;
        public float SuffocationTime { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsLost { get; private set; }
        public bool Paused { get; set; }

        public bool OxygenLow => Oxygen <= tuning.maxOxygen * tuning.lowWarning;
        public bool PowerLow => Power <= tuning.maxPower * tuning.lowWarning;
        public float SuffocationLeft => Mathf.Max(0f, tuning.suffocationSeconds - SuffocationTime);

        void Awake() => ResetStation();

        void Update()
        {
            if (!Paused && !IsLost)
                Tick(Time.deltaTime);
        }

        public void ResetStation()
        {
            Rooms.Clear();
            Crew.Clear();

            var quartersA = AddRoom("Quarters A", RoomType.Quarters, 0, 0, 2);
            var generatorA = AddRoom("Generator A", RoomType.Generator, 0, 1, 1);
            var scrubberA = AddRoom("Scrubber A", RoomType.Scrubber, 1, 0, 1);
            AddRoom("Generator B", RoomType.Generator, 1, 1, 1);
            AddRoom("Scrubber B", RoomType.Scrubber, 2, 0, 1);
            AddRoom("Quarters B", RoomType.Quarters, 2, 1, 2);

            AddCrew("Mara", new Color32(255, 122, 100, 255), 100f, generatorA);
            AddCrew("Theo", new Color32(186, 236, 92, 255), 75f, scrubberA);
            AddCrew("Juno", new Color32(255, 134, 206, 255), 40f, quartersA);

            Oxygen = tuning.startOxygen;
            Power = tuning.startPower;
            Elapsed = 0f;
            SuffocationTime = 0f;
            IsLost = false;
            Paused = false;
            Tick(0f);
        }

        Room AddRoom(string name, RoomType type, int floor, int column, int capacity)
        {
            var room = new Room(name, type, floor, column, capacity);
            Rooms.Add(room);
            return room;
        }

        void AddCrew(string name, Color color, float stamina, Room room)
        {
            var crew = new CrewMember(name, color, stamina) { Room = room };
            room.Assigned.Add(crew);
            Crew.Add(crew);
        }

        /// <summary>
        /// Send a crew member to a room. If the room is full, its last occupant swaps
        /// into the sender's old room so a tap never fails silently.
        /// </summary>
        public bool Send(CrewMember crew, Room target)
        {
            if (IsLost || crew == null || target == null || crew.Room == target)
                return false;

            var from = crew.Room;
            from.Assigned.Remove(crew);

            if (target.Assigned.Count >= target.Capacity)
            {
                var swapped = target.Assigned[target.Assigned.Count - 1];
                target.Assigned.Remove(swapped);
                MoveTo(swapped, from);
            }

            MoveTo(crew, target);
            return true;
        }

        void MoveTo(CrewMember crew, Room room)
        {
            room.Assigned.Add(crew);
            crew.Room = room;
            crew.TravelLeft = tuning.travelSeconds;
            crew.MoveId++;
        }

        float Efficiency(CrewMember crew) => crew.IsExhausted ? tuning.exhaustedEfficiency : 1f;

        public void Tick(float dt)
        {
            Elapsed += dt;

            foreach (var crew in Crew)
                if (crew.TravelLeft > 0f)
                    crew.TravelLeft = Mathf.Max(0f, crew.TravelLeft - dt);

            // Who is actually on the job, and what does that make and cost?
            float powerMade = 0f;
            float powerWanted = 0f;
            foreach (var room in Rooms)
            {
                room.Present = 0;
                room.Workforce = 0f;
                foreach (var crew in room.Assigned)
                {
                    if (crew.IsMoving) continue;
                    room.Present++;
                    room.Workforce += Efficiency(crew);
                }

                switch (room.Type)
                {
                    case RoomType.Generator:
                        powerMade += room.Workforce * tuning.generatorPower;
                        break;
                    case RoomType.Scrubber:
                        powerWanted += room.Present * tuning.scrubberPower;
                        break;
                    case RoomType.Quarters:
                        powerWanted += room.Present * tuning.quartersPowerPerCrew;
                        break;
                }
            }

            // Power: if the bank plus this tick's output can't cover demand, every
            // consumer runs at the same reduced fraction instead of flickering on and off.
            float supply = 1f;
            if (powerWanted > 0f)
            {
                if (dt > 0f)
                    supply = Mathf.Clamp01((Power + powerMade * dt) / (powerWanted * dt));
                else if (Power <= 0.001f)
                    supply = Mathf.Clamp01(powerMade / powerWanted);
            }
            PowerSupply = supply;
            PowerRate = powerMade - powerWanted;
            Power = Mathf.Clamp(Power + (powerMade - powerWanted * supply) * dt, 0f, tuning.maxPower);

            // Oxygen.
            float oxygenMade = 0f;
            foreach (var room in Rooms)
                if (room.Type == RoomType.Scrubber)
                    oxygenMade += room.Workforce * tuning.scrubberOxygen * supply;
            OxygenRate = oxygenMade - Crew.Count * tuning.breathePerCrew;
            Oxygen = Mathf.Clamp(Oxygen + OxygenRate * dt, 0f, tuning.maxOxygen);

            // Stamina: work drains it, Quarters restore it.
            float restSpeed = Mathf.Lerp(tuning.unpoweredRestFactor, 1f, supply);
            foreach (var crew in Crew)
            {
                if (crew.IsMoving) continue;
                if (crew.Room.Type == RoomType.Quarters)
                    crew.Stamina = Mathf.Min(MaxStamina, crew.Stamina + tuning.restStaminaGain * restSpeed * dt);
                else
                    crew.Stamina = Mathf.Max(0f, crew.Stamina - tuning.workStaminaDrain * dt);
            }

            // Running out of air starts a visible countdown rather than ending the run outright.
            if (Oxygen <= 0.001f)
            {
                SuffocationTime += dt;
                if (SuffocationTime >= tuning.suffocationSeconds)
                    IsLost = true;
            }
            else
            {
                SuffocationTime = Mathf.Max(0f, SuffocationTime - dt * 2f);
            }

            foreach (var room in Rooms)
                UpdateStatus(room, supply);
        }

        void UpdateStatus(Room room, float supply)
        {
            bool enRoute = room.Present == 0 && room.Assigned.Count > 0;
            bool tired = room.Present > 0 && room.Workforce < room.Present - 0.001f;
            int supplyPercent = Mathf.RoundToInt(supply * 100f);

            switch (room.Type)
            {
                case RoomType.Generator:
                    room.RateText = room.Present > 0
                        ? $"+{room.Workforce * tuning.generatorPower:0.0} power/s"
                        : $"Makes +{tuning.generatorPower:0.0} power/s per crew";
                    if (enRoute) Set(room, RoomAlert.Idle, "Crew on the way");
                    else if (room.Present == 0)
                        Set(room, PowerLow ? RoomAlert.Failing : RoomAlert.Idle,
                            PowerLow ? "POWER LOW: send crew here" : "Idle: needs crew");
                    else if (tired) Set(room, RoomAlert.Warning, "Crew exhausted: half output");
                    else Set(room, RoomAlert.Ok, "Generating");
                    break;

                case RoomType.Scrubber:
                    room.RateText = room.Present > 0
                        ? $"+{room.Workforce * tuning.scrubberOxygen * supply:0.0} O2/s   -{room.Present * tuning.scrubberPower * supply:0.0} power/s"
                        : $"Makes +{tuning.scrubberOxygen:0.0} O2/s, uses {tuning.scrubberPower:0.0} power/s";
                    if (enRoute) Set(room, RoomAlert.Idle, "Crew on the way");
                    else if (room.Present == 0)
                        Set(room, OxygenLow ? RoomAlert.Failing : RoomAlert.Idle,
                            OxygenLow ? "OXYGEN LOW: send crew here" : "Idle: needs crew");
                    else if (supply < 0.02f) Set(room, RoomAlert.Failing, "NO POWER: scrubber stopped");
                    else if (supply < 0.98f) Set(room, RoomAlert.Failing, $"LOW POWER: running at {supplyPercent}%");
                    else if (tired) Set(room, RoomAlert.Warning, "Crew exhausted: half output");
                    else Set(room, RoomAlert.Ok, "Scrubbing");
                    break;

                case RoomType.Quarters:
                    room.RateText = room.Present > 0
                        ? $"+{tuning.restStaminaGain * Mathf.Lerp(tuning.unpoweredRestFactor, 1f, supply):0.0} stamina/s each   -{room.Present * tuning.quartersPowerPerCrew * supply:0.0} power/s"
                        : $"Rest: +{tuning.restStaminaGain:0.0} stamina/s, uses {tuning.quartersPowerPerCrew:0.0} power/s each";
                    if (enRoute) Set(room, RoomAlert.Idle, "Crew on the way");
                    else if (room.Present == 0) Set(room, RoomAlert.Idle, "Empty: crew rest here");
                    else if (supply < 0.98f) Set(room, RoomAlert.Warning, "No power: resting slowly");
                    else Set(room, RoomAlert.Ok, "Resting");
                    break;
            }
        }

        static void Set(Room room, RoomAlert alert, string status)
        {
            room.Alert = alert;
            room.StatusText = status;
        }
    }
}
