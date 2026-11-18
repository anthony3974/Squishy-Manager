using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepStation
{
    /// <summary>
    /// The simulation: two station-wide resources (oxygen, power), three rooms, three crew who
    /// walk between them. Knows nothing about rendering; StationView and StationHud read it.
    /// </summary>
    public class Station : MonoBehaviour
    {
        public const float MaxStamina = 100f;

        public StationTuning tuning = new StationTuning();

        public readonly List<Room> Rooms = new List<Room>();
        public readonly List<CrewMember> Crew = new List<CrewMember>();

        /// <summary>Raised after ResetStation replaces the rooms and crew.</summary>
        public event Action Rebuilt;

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
        public float RestartIn { get; private set; }

        public int FloorCount { get; private set; }
        public bool OxygenLow => Oxygen <= tuning.maxOxygen * tuning.lowWarning;
        public bool PowerLow => Power <= tuning.maxPower * tuning.lowWarning;
        public float SuffocationLeft => Mathf.Max(0f, tuning.suffocationSeconds - SuffocationTime);

        void Awake() => ResetStation();

        void Update()
        {
            if (IsLost)
            {
                RestartIn -= Time.deltaTime;
                if (RestartIn <= 0f) ResetStation();
                return;
            }
            Tick(Time.deltaTime);
        }

        public void ResetStation()
        {
            Rooms.Clear();
            Crew.Clear();

            var quarters = AddRoom("Living Quarters", RoomType.Quarters, 0, 0);
            var scrubber = AddRoom("Oxygen Scrubber", RoomType.Scrubber, 0, 1);
            var generator = AddRoom("Generator", RoomType.Generator, 1, 0);

            AddCrew("Mara", new Color32(240, 200, 110, 255), 100f, generator);
            AddCrew("Theo", new Color32(110, 215, 200, 255), 75f, scrubber);
            AddCrew("Juno", new Color32(190, 165, 245, 255), 40f, quarters);

            FloorCount = 0;
            foreach (var room in Rooms) FloorCount = Mathf.Max(FloorCount, room.Floor + 1);

            Oxygen = tuning.startOxygen;
            Power = tuning.startPower;
            Elapsed = 0f;
            SuffocationTime = 0f;
            IsLost = false;
            Tick(0f);
            Rebuilt?.Invoke();
        }

        Room AddRoom(string name, RoomType type, int floor, int column)
        {
            var room = new Room(name, type, floor, column, capacity: 2);
            Rooms.Add(room);
            return room;
        }

        void AddCrew(string name, Color color, float stamina, Room room)
        {
            var crew = new CrewMember(name, color, stamina) { Room = room, Slot = room.FreeSlot() };
            crew.Position = room.Slots[crew.Slot];
            room.Assigned.Add(crew);
            Crew.Add(crew);
        }

        /// <summary>
        /// Send a crew member to a room. If the room is full, one occupant swaps into the
        /// sender's old spot so a tap never fails silently.
        /// </summary>
        public bool Send(CrewMember crew, Room target)
        {
            if (IsLost || crew == null || target == null || crew.Room == target)
                return false;

            var from = crew.Room;
            int fromSlot = crew.Slot;
            from.Assigned.Remove(crew);

            int slot = target.FreeSlot();
            if (slot < 0)
            {
                var swapped = target.Assigned[target.Assigned.Count - 1];
                slot = swapped.Slot;
                target.Assigned.Remove(swapped);
                Assign(swapped, from, fromSlot);
            }

            Assign(crew, target, slot);
            return true;
        }

        static void Assign(CrewMember crew, Room room, int slot)
        {
            room.Assigned.Add(crew);
            crew.Room = room;
            crew.Slot = slot;
            StationLayout.BuildPath(crew.Position, room.Slots[slot], crew.Path);
        }

        float Efficiency(CrewMember crew) => crew.IsExhausted ? tuning.exhaustedEfficiency : 1f;

        public void Tick(float dt)
        {
            Elapsed += dt;

            foreach (var crew in Crew)
                Walk(crew, dt);

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

            // Stamina: work drains it, Quarters restore it. Walking does neither.
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
                {
                    IsLost = true;
                    RestartIn = tuning.restartDelay;
                }
            }
            else
            {
                SuffocationTime = Mathf.Max(0f, SuffocationTime - dt * 2f);
            }

            foreach (var room in Rooms)
                UpdateStatus(room, supply);
        }

        void Walk(CrewMember crew, float dt)
        {
            float budget = dt;
            while (budget > 0f && crew.Path.Count > 0)
            {
                Vector2 target = crew.Path[0];
                Vector2 delta = target - crew.Position;
                float distance = delta.magnitude;
                bool vertical = Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
                float speed = Mathf.Max(0.1f, vertical ? tuning.climbSpeed : tuning.walkSpeed);

                if (distance > 0.0001f)
                {
                    crew.Climbing = vertical;
                    if (!vertical) crew.FacingRight = delta.x > 0f;
                }

                float step = speed * budget;
                if (step >= distance)
                {
                    crew.Position = target;
                    crew.Path.RemoveAt(0);
                    budget -= distance / speed;
                }
                else
                {
                    crew.Position += delta / distance * step;
                    budget = 0f;
                }
            }

            if (crew.Path.Count == 0)
            {
                crew.Climbing = false;
                // Face the room's equipment while working.
                crew.FacingRight = crew.Position.x < crew.Room.Bounds.center.x;
            }
        }

        void UpdateStatus(Room room, float supply)
        {
            bool helpComing = room.Present == 0 && room.Assigned.Count > 0;
            bool tired = room.Present > 0 && room.Workforce < room.Present - 0.001f;

            switch (room.Type)
            {
                case RoomType.Generator:
                    room.Meter = room.Workforce / room.Capacity;
                    room.RateText = room.Present > 0 ? $"+{room.Workforce * tuning.generatorPower:0.0} power/s" : "idle";
                    if (room.Present == 0)
                        Set(room, PowerLow && !helpComing ? RoomAlert.Failing : RoomAlert.Idle, "NO CREW");
                    else if (tired) Set(room, RoomAlert.Warning, "TIRED CREW");
                    else Set(room, RoomAlert.Ok, "");
                    break;

                case RoomType.Scrubber:
                    room.Meter = room.Workforce * supply / room.Capacity;
                    room.RateText = room.Present > 0
                        ? $"+{room.Workforce * tuning.scrubberOxygen * supply:0.0} O2/s  -{room.Present * tuning.scrubberPower * supply:0.0} power/s"
                        : "idle";
                    if (room.Present == 0)
                        Set(room, OxygenLow && !helpComing ? RoomAlert.Failing : RoomAlert.Idle, "NO CREW");
                    else if (supply < 0.02f) Set(room, RoomAlert.Failing, "NO POWER");
                    else if (supply < 0.98f) Set(room, RoomAlert.Failing, $"LOW POWER {Mathf.RoundToInt(supply * 100f)}%");
                    else if (tired) Set(room, RoomAlert.Warning, "TIRED CREW");
                    else Set(room, RoomAlert.Ok, "");
                    break;

                case RoomType.Quarters:
                    float stamina = 0f;
                    foreach (var crew in room.Assigned)
                        if (!crew.IsMoving) stamina += crew.Stamina;
                    room.Meter = room.Present > 0 ? stamina / (room.Present * MaxStamina) : 0f;
                    room.RateText = room.Present > 0 ? $"{room.Present} resting  -{room.Present * tuning.quartersPowerPerCrew * supply:0.0} power/s" : "empty";
                    if (room.Present > 0 && supply < 0.98f) Set(room, RoomAlert.Warning, "NO POWER: SLOW REST");
                    else Set(room, room.Present > 0 ? RoomAlert.Ok : RoomAlert.Idle, "");
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
