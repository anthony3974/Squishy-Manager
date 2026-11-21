using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepStation
{
    /// <summary>
    /// The simulation: two station-wide resources (oxygen, power), three rooms, three crew who
    /// walk between them, machines that wear out and get repaired, and emergency oxygen tanks.
    /// Knows nothing about rendering; StationView and StationHud read it.
    /// </summary>
    public class Station : MonoBehaviour
    {
        public const float MaxStamina = 100f;

        public StationTuning tuning = new StationTuning();

        public readonly List<Room> Rooms = new List<Room>();
        public readonly List<CrewMember> Crew = new List<CrewMember>();
        public readonly List<EmergencyTank> Tanks = new List<EmergencyTank>();

        /// <summary>Raised after ResetStation replaces the rooms and crew.</summary>
        public event Action Rebuilt;
        /// <summary>A machine was just fixed. For the sparkle and the repairers' "good as new".</summary>
        public event Action<Room> RepairFinished;
        /// <summary>Something good happened worth a short pop-up (minutes survived, tank refilled, air back).</summary>
        public event Action<string> Milestone;

        public float Oxygen { get; private set; }
        public float Power { get; private set; }
        /// <summary>Net oxygen per second: made, minus breathed, plus tank venting, minus tank refilling.</summary>
        public float OxygenRate { get; private set; }
        /// <summary>Net power per second (made minus wanted), even when the bank is empty.</summary>
        public float PowerRate { get; private set; }
        /// <summary>1 when every room gets the power it wants, lower during a blackout.</summary>
        public float PowerSupply { get; private set; } = 1f;
        /// <summary>Seconds at zero oxygen with every tank empty. Crew faint when it reaches suffocationSeconds.</summary>
        public float SuffocationTime { get; private set; }
        public float Elapsed { get; private set; }
        /// <summary>How many crew are fainted right now.</summary>
        public int FaintedCount { get; private set; }
        /// <summary>Seconds until the last fainted crew member wakes up.</summary>
        public float WakeIn { get; private set; }

        /// <summary>The tank currently venting into the station, if any.</summary>
        public EmergencyTank VentingTank { get; private set; }
        /// <summary>The tank currently being topped up from spare air, if any.</summary>
        public EmergencyTank RefillingTank { get; private set; }
        bool refilling;

        public int FullTanks
        {
            get
            {
                int n = 0;
                foreach (var tank in Tanks) if (tank.IsFull) n++;
                return n;
            }
        }

        public int FloorCount { get; private set; }
        /// <summary>Where crew rest. Every crew member is either at their job or here.</summary>
        public Room Quarters { get; private set; }
        public bool OxygenLow => Oxygen <= tuning.maxOxygen * tuning.lowWarning;
        public bool PowerLow => Power <= tuning.maxPower * tuning.lowWarning;
        public float SuffocationLeft => Mathf.Max(0f, tuning.suffocationSeconds - SuffocationTime);

        void Awake() => ResetStation();

        void Update() => Tick(Time.deltaTime);

        public void ResetStation()
        {
            Rooms.Clear();
            Crew.Clear();

            var quarters = AddRoom("Living Quarters", RoomType.Quarters, 0, 0);
            var scrubber = AddRoom("Oxygen Scrubber", RoomType.Scrubber, 0, 1);
            var generator = AddRoom("Generator", RoomType.Generator, 1, 0);

            Quarters = quarters;

            // Juno starts the dive resting, and joins Mara on the Generator once rested.
            AddCrew("Mara", new Color32(240, 200, 110, 255), CrewTrait.Tireless, 100f, generator, generator);
            AddCrew("Theo", new Color32(110, 215, 200, 255), CrewTrait.Tinkerer, 75f, scrubber, scrubber);
            AddCrew("Juno", new Color32(190, 165, 245, 255), CrewTrait.QuickNapper, 40f, generator, quarters);

            Tanks.Clear();
            for (int i = 0; i < Mathf.Max(0, tuning.emergencyTanks); i++)
                Tanks.Add(new EmergencyTank());
            VentingTank = null;
            RefillingTank = null;
            refilling = false;

            FloorCount = 0;
            foreach (var room in Rooms) FloorCount = Mathf.Max(FloorCount, room.Floor + 1);

            Oxygen = tuning.startOxygen;
            Power = tuning.startPower;
            Elapsed = 0f;
            SuffocationTime = 0f;
            nextMilestone = 0;
            airCrisis = false;
            Tick(0f);
            Rebuilt?.Invoke();
        }

        Room AddRoom(string name, RoomType type, int floor, int column)
        {
            var room = new Room(name, type, floor, column, capacity: 2);
            room.WearLimit = NextWearLimit();
            Rooms.Add(room);
            return room;
        }

        void AddCrew(string name, Color color, CrewTrait trait, float stamina, Room job, Room room)
        {
            var crew = new CrewMember(name, color, trait, stamina, job) { Room = room, Slot = room.FreeSlot() };
            crew.Position = room.Slots[crew.Slot];
            room.Assigned.Add(crew);
            Crew.Add(crew);
        }

        /// <summary>
        /// Give a crew member a standing job in a room and send them there now. If the room
        /// already has a full crew on that job, one of them swaps into the sender's old job so a
        /// tap never fails silently. Sending a resting crew member to their own job pulls them
        /// out of bed early.
        /// </summary>
        public bool Send(CrewMember crew, Room target)
        {
            if (crew == null || target == null)
                return false;
            if (crew.Job == target && crew.Room == target)
                return false;

            CrewMember swapped = null;
            if (crew.Job != target)
            {
                // Prefer swapping with someone standing in the room, so the two visibly trade places.
                int holders = 0;
                foreach (var other in Crew)
                    if (other != crew && other.Job == target) holders++;
                if (holders >= target.Capacity)
                {
                    for (int i = target.Assigned.Count - 1; i >= 0 && swapped == null; i--)
                        if (target.Assigned[i] != crew && target.Assigned[i].Job == target) swapped = target.Assigned[i];
                    if (swapped == null)
                        foreach (var other in Crew)
                            if (other != crew && other.Job == target) swapped = other;
                    swapped.Job = crew.Job;
                }
                crew.Job = target;
            }

            // A tired crew member the player sends to work keeps going until exhausted, so the
            // order isn't immediately undone by auto-rest.
            crew.KeepWorking = target.Type != RoomType.Quarters && crew.Stamina <= tuning.autoRestAt;
            crew.WaitingForBed = false;
            bool moved = MoveTo(crew, target);
            if (swapped != null) Settle(swapped);
            return moved;
        }

        /// <summary>Walk a crew member to a room, taking a free slot or bumping someone off-job.</summary>
        bool MoveTo(CrewMember crew, Room room, int depth = 0)
        {
            if (crew.Room == room) return true;

            var from = crew.Room;
            from.Assigned.Remove(crew);

            int slot = room.FreeSlot();
            if (slot < 0 && depth < 3)
            {
                // Full: whoever is only visiting (resting away from their job, or just swapped
                // onto another job) heads to their own job to make space.
                CrewMember visitor = null;
                for (int i = room.Assigned.Count - 1; i >= 0 && visitor == null; i--)
                    if (room.Assigned[i].Job != room) visitor = room.Assigned[i];
                if (visitor != null && MoveTo(visitor, visitor.Job, depth + 1))
                    slot = room.FreeSlot();
            }

            if (slot < 0)
            {
                from.Assigned.Add(crew);
                return false;
            }
            Assign(crew, room, slot);
            return true;
        }

        /// <summary>Make sure a crew member is at their job or resting in Quarters, nowhere else.</summary>
        void Settle(CrewMember crew)
        {
            if (crew.Room != crew.Job && crew.Room != Quarters)
                MoveTo(crew, crew.Job);
        }

        static void Assign(CrewMember crew, Room room, int slot)
        {
            room.Assigned.Add(crew);
            crew.Room = room;
            crew.Slot = slot;
            StationLayout.BuildPath(crew.Position, room.Slots[slot], crew.Path);
        }

        /// <summary>
        /// Auto-rest: tired crew walk to Quarters on their own, and walk back to their job once
        /// rested. Runs every tick after stamina changes.
        /// </summary>
        void UpdateRest()
        {
            if (Quarters == null) return;
            var t = tuning;
            float rested = Mathf.Min(t.autoReturnAt, MaxStamina) - 0.01f;

            foreach (var crew in Crew)
            {
                crew.WaitingForBed = false;
                if (crew.IsResting) crew.KeepWorking = false;
                if (crew.IsResting && !crew.IsFainted && crew.Job != Quarters && crew.Stamina >= rested)
                    MoveTo(crew, crew.Job);
            }

            // Most tired first, while there are beds.
            restQueue.Clear();
            foreach (var crew in Crew)
                if (NeedsRest(crew)) restQueue.Add(crew);
            restQueue.Sort((a, b) => a.Stamina.CompareTo(b.Stamina));
            foreach (var crew in restQueue)
            {
                if (Quarters.FreeSlot() >= 0) MoveTo(crew, Quarters);
                else crew.WaitingForBed = true;
            }
        }

        readonly List<CrewMember> restQueue = new List<CrewMember>();

        bool NeedsRest(CrewMember crew)
        {
            if (crew.IsFainted || crew.IsMoving || crew.Room == Quarters) return false;
            // Finish the repair in hand unless completely spent.
            if (crew.IsRepairing && !crew.IsExhausted) return false;
            return crew.KeepWorking ? crew.IsExhausted : crew.Stamina <= tuning.autoRestAt;
        }

        // ------------------------------------------------------------------ traits

        float DrainFactor(CrewMember crew) => crew.Trait == CrewTrait.Tireless ? tuning.tirelessDrainFactor : 1f;
        float RepairFactor(CrewMember crew) => crew.Trait == CrewTrait.Tinkerer ? tuning.tinkererRepairFactor : 1f;
        float RestFactor(CrewMember crew) => crew.Trait == CrewTrait.QuickNapper ? tuning.quickNapperRestFactor : 1f;

        /// <summary>The trait and exactly what it does, e.g. "Tireless: tires 25% slower".</summary>
        public string TraitText(CrewMember crew)
        {
            switch (crew.Trait)
            {
                case CrewTrait.Tireless: return $"Tireless: tires {Percent(1f - tuning.tirelessDrainFactor)}% slower";
                case CrewTrait.Tinkerer: return $"Tinkerer: repairs {Percent(tuning.tinkererRepairFactor - 1f)}% faster";
                default: return $"Quick napper: rests {Percent(tuning.quickNapperRestFactor - 1f)}% faster";
            }
        }

        static int Percent(float fraction) => Mathf.RoundToInt(fraction * 100f);

        // ------------------------------------------------------------------ milestones

        static readonly int[] MilestoneMinutes = { 2, 5, 10, 15, 20, 30, 45, 60 };
        int nextMilestone;
        bool airCrisis;

        /// <summary>Minutes survived, and the air coming back after tanks venting or crew fainting.</summary>
        void UpdateMilestones()
        {
            int minutes = nextMilestone < MilestoneMinutes.Length
                ? MilestoneMinutes[nextMilestone]
                : 60 + 30 * (nextMilestone - MilestoneMinutes.Length + 1);
            if (Elapsed >= minutes * 60f)
            {
                nextMilestone++;
                Milestone?.Invoke($"{minutes} minutes on the seabed");
            }

            if (VentingTank != null || SuffocationTime > 0f || FaintedCount > 0) airCrisis = true;
            else if (airCrisis && Oxygen >= tuning.maxOxygen * 0.5f)
            {
                airCrisis = false;
                Milestone?.Invoke("Air restored");
            }
        }

        /// <summary>Seconds of rest left before this crew member walks back to their job.</summary>
        public float RestSecondsLeft(CrewMember crew)
        {
            float gain = tuning.restStaminaGain * RestFactor(crew) * Mathf.Lerp(tuning.unpoweredRestFactor, 1f, PowerSupply);
            float need = Mathf.Max(0f, Mathf.Min(tuning.autoReturnAt, MaxStamina) - crew.Stamina);
            return crew.FaintTime + (gain > 0.001f ? need / gain : float.PositiveInfinity);
        }

        float Efficiency(CrewMember crew) => crew.IsExhausted ? tuning.exhaustedEfficiency : 1f;

        float NextWearLimit()
        {
            float a = Mathf.Max(1f, tuning.breakdownMinSeconds);
            float b = Mathf.Max(1f, tuning.breakdownMaxSeconds);
            return UnityEngine.Random.Range(Mathf.Min(a, b), Mathf.Max(a, b));
        }

        public void Tick(float dt)
        {
            var t = tuning;
            Elapsed += dt;

            foreach (var crew in Crew)
            {
                // Fainted crew stay where they dropped (even mid-walk) and carry on once awake.
                if (crew.IsFainted) crew.FaintTime = Mathf.Max(0f, crew.FaintTime - dt);
                else Walk(crew, dt);
                crew.RepairCooldown = Mathf.Max(0f, crew.RepairCooldown - dt);
                crew.IsRepairing = false;
            }

            // Who is actually on the job, and what does that make and cost? Crew in a broken
            // room repair it instead of producing anything.
            float powerMade = 0f;
            float powerWanted = 0f;
            foreach (var room in Rooms)
            {
                room.Present = 0;
                room.Fainted = 0;
                room.Workforce = 0f;
                room.Repairers = 0;
                room.RepairWork = 0f;
                foreach (var crew in room.Assigned)
                {
                    if (crew.IsFainted)
                    {
                        room.Fainted++;
                        continue;
                    }
                    if (crew.IsMoving) continue;
                    room.Present++;
                    room.Workforce += Efficiency(crew);
                    if (room.Broken && crew.RepairCooldown <= 0f)
                    {
                        crew.IsRepairing = true;
                        room.Repairers++;
                        room.RepairWork += Efficiency(crew) * RepairFactor(crew);
                    }
                }

                switch (room.Type)
                {
                    case RoomType.Generator:
                        if (!room.Broken) powerMade += room.Workforce * t.generatorPower;
                        break;
                    case RoomType.Scrubber:
                        if (!room.Broken) powerWanted += room.Present * t.scrubberPower;
                        break;
                    case RoomType.Quarters:
                        powerWanted += room.Present * t.quartersPowerPerCrew;
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
            Power = Mathf.Clamp(Power + (powerMade - powerWanted * supply) * dt, 0f, t.maxPower);

            // Oxygen, including whatever the emergency tanks give or take.
            float oxygenMade = 0f;
            foreach (var room in Rooms)
                if (room.Type == RoomType.Scrubber && !room.Broken)
                    oxygenMade += room.Workforce * t.scrubberOxygen * supply;
            OxygenRate = oxygenMade - Crew.Count * t.breathePerCrew + TankFlow(dt);
            Oxygen = Mathf.Clamp(Oxygen + OxygenRate * dt, 0f, t.maxOxygen);

            // Out of air: crack open the fullest tank before anyone starts suffocating.
            if (Oxygen <= 0.001f && VentingTank == null)
            {
                foreach (var tank in Tanks)
                    if (tank.Fill > 0.01f && (VentingTank == null || tank.Fill > VentingTank.Fill))
                        VentingTank = tank;
            }

            // Stamina: work drains it, repairs drain it faster, Quarters restore it.
            // Walking, and waiting in a broken room, do neither.
            float restSpeed = Mathf.Lerp(t.unpoweredRestFactor, 1f, supply);
            foreach (var crew in Crew)
            {
                if (crew.IsMoving || crew.IsFainted) continue;
                if (crew.Room.Type == RoomType.Quarters)
                    crew.Stamina = Mathf.Min(MaxStamina, crew.Stamina + t.restStaminaGain * restSpeed * RestFactor(crew) * dt);
                else if (crew.IsRepairing)
                    crew.Stamina = Mathf.Max(0f, crew.Stamina - t.workStaminaDrain * t.repairStaminaFactor * DrainFactor(crew) * dt);
                else if (!crew.Room.Broken)
                    crew.Stamina = Mathf.Max(0f, crew.Stamina - t.workStaminaDrain * DrainFactor(crew) * dt);
            }

            foreach (var room in Rooms)
                UpdateWear(room, supply, dt);

            UpdateRest();

            UpdateFainting(dt);
            UpdateMilestones();

            foreach (var room in Rooms)
                UpdateStatus(room, supply);
        }

        /// <summary>
        /// Running out of air with no tanks left starts a visible countdown. When it ends the
        /// crew faint where they stand instead of the run ending. They come round on their own
        /// after a while and get a fresh countdown, which is enough time for the scrubber crew to
        /// get air flowing again. If nobody has the scrubber job they faint again: a setback
        /// that repeats until it's fixed, never a game over.
        /// </summary>
        void UpdateFainting(float dt)
        {
            var t = tuning;
            int awake = 0;
            FaintedCount = 0;
            WakeIn = 0f;
            foreach (var crew in Crew)
            {
                if (crew.IsFainted)
                {
                    FaintedCount++;
                    WakeIn = Mathf.Max(WakeIn, crew.FaintTime);
                }
                else awake++;
            }

            bool outOfAir = Oxygen <= 0.001f && VentingTank == null;
            if (!outOfAir)
            {
                SuffocationTime = Mathf.Max(0f, SuffocationTime - dt * 2f);
                return;
            }
            // The countdown only runs while somebody is awake to faint.
            if (awake == 0) return;

            SuffocationTime += dt;
            if (SuffocationTime < t.suffocationSeconds) return;

            SuffocationTime = 0f;
            float down = Mathf.Max(1f, t.faintSeconds);
            foreach (var crew in Crew)
            {
                if (crew.IsFainted) continue;
                crew.FaintTime = down;
                crew.IsRepairing = false;
                FaintedCount++;
            }
            WakeIn = down;
        }

        /// <summary>
        /// Oxygen per second the tanks add (venting) or take (refilling). A venting tank empties
        /// completely; tanks refill one at a time from spare air while oxygen is high.
        /// </summary>
        float TankFlow(float dt)
        {
            var t = tuning;
            float perTank = Mathf.Max(1f, t.tankOxygen);
            RefillingTank = null;

            if (VentingTank != null)
            {
                refilling = false;
                float rate = perTank / Mathf.Max(0.1f, t.tankVentSeconds);
                if (dt > 0f)
                {
                    rate = Mathf.Min(rate, VentingTank.Fill * perTank / dt);
                    VentingTank.Fill = Mathf.Max(0f, VentingTank.Fill - rate * dt / perTank);
                }
                if (VentingTank.Fill <= 0.0001f)
                {
                    VentingTank.Fill = 0f;
                    VentingTank = null;
                }
                return rate;
            }

            if (Oxygen >= t.maxOxygen * t.tankRefillAbove) refilling = true;
            else if (Oxygen < t.maxOxygen * (t.tankRefillAbove - 0.1f)) refilling = false;
            if (!refilling || t.tankRefillRate <= 0f) return 0f;

            // Top up the fullest tank that isn't full, so one tank is ready as soon as possible.
            foreach (var tank in Tanks)
                if (!tank.IsFull && (RefillingTank == null || tank.Fill > RefillingTank.Fill))
                    RefillingTank = tank;
            if (RefillingTank == null) return 0f;

            RefillingTank.Fill = Mathf.Min(1f, RefillingTank.Fill + t.tankRefillRate * dt / perTank);
            if (RefillingTank.IsFull) Milestone?.Invoke("Emergency tank refilled");
            return -t.tankRefillRate;
        }

        /// <summary>Machines wear while running, rattle as a warning, break, and get repaired.</summary>
        void UpdateWear(Room room, float supply, float dt)
        {
            room.Rattling = false;
            if (!room.CanBreak) return;

            if (room.Broken)
            {
                room.RepairProgress += room.RepairWork / Mathf.Max(0.1f, tuning.repairSeconds) * dt;
                if (room.RepairProgress >= 1f)
                {
                    room.Broken = false;
                    room.RepairProgress = 0f;
                    room.Wear = 0f;
                    room.WearLimit = NextWearLimit();
                    foreach (var crew in room.Assigned)
                    {
                        if (!crew.IsRepairing) continue;
                        crew.IsRepairing = false;
                        crew.RepairCooldown = tuning.repairCooldown;
                        crew.RepairedRoom = room;
                    }
                    RepairFinished?.Invoke(room);
                }
                return;
            }

            bool running = room.Workforce > 0f && (room.Type != RoomType.Scrubber || supply > 0.02f);
            if (!running) return;

            room.Wear += dt;
            if (room.Wear >= room.WearLimit)
            {
                room.Broken = true;
                room.RepairProgress = 0f;
            }
            else
            {
                room.Rattling = room.WearLimit - room.Wear <= tuning.rattleSeconds;
            }
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
            bool helpComing = false;
            if (room.Present == 0)
                foreach (var crew in room.Assigned)
                    if (crew.IsMoving && !crew.IsFainted) helpComing = true;
            bool tired = room.Present > 0 && room.Workforce < room.Present - 0.001f;

            if (room.Broken) BrokenStatus(room, helpComing);
            else WorkingStatus(room, supply, helpComing, tired);

            // Fainted crew override everything else on the room, with the cause and a time.
            if (room.Fainted > 0 && room.Present == 0)
            {
                float wake = 0f;
                foreach (var crew in room.Assigned)
                    if (crew.IsFainted) wake = Mathf.Max(wake, crew.FaintTime);
                room.RateText = $"no air  ·  waking in ~{Mathf.CeilToInt(wake)}s";
                Set(room, RoomAlert.Failing, "CREW FAINTED");
            }
        }

        void WorkingStatus(Room room, float supply, bool helpComing, bool tired)
        {

            switch (room.Type)
            {
                case RoomType.Generator:
                    room.Activity = room.Workforce / room.Capacity;
                    room.Meter = room.Activity;
                    room.RateText = $"+{room.Workforce * tuning.generatorPower:0.0} power/s";
                    if (room.Present == 0) NoCrewStatus(room, helpComing, PowerLow);
                    else if (room.Rattling) Set(room, RoomAlert.Warning, "RATTLING");
                    else if (tired) Set(room, RoomAlert.Warning, "TIRED CREW");
                    else Set(room, RoomAlert.Ok, "");
                    break;

                case RoomType.Scrubber:
                    room.Activity = room.Workforce * supply / room.Capacity;
                    room.Meter = room.Activity;
                    room.RateText = $"+{room.Workforce * tuning.scrubberOxygen * supply:0.0} O2/s  -{room.Present * tuning.scrubberPower * supply:0.0} power/s";
                    if (room.Present == 0) NoCrewStatus(room, helpComing, OxygenLow);
                    else if (supply < 0.02f) Set(room, RoomAlert.Failing, "NO POWER");
                    else if (supply < 0.98f) Set(room, RoomAlert.Failing, $"LOW POWER {Mathf.RoundToInt(supply * 100f)}%");
                    else if (room.Rattling) Set(room, RoomAlert.Warning, "RATTLING");
                    else if (tired) Set(room, RoomAlert.Warning, "TIRED CREW");
                    else Set(room, RoomAlert.Ok, "");
                    break;

                case RoomType.Quarters:
                    float stamina = 0f;
                    foreach (var crew in room.Assigned)
                        if (!crew.IsMoving && !crew.IsFainted) stamina += crew.Stamina;
                    room.Meter = room.Present > 0 ? stamina / (room.Present * MaxStamina) : 0f;
                    room.Activity = room.Meter;
                    room.RateText = room.Present > 0 ? $"{room.Present} resting  -{room.Present * tuning.quartersPowerPerCrew * supply:0.0} power/s" : "empty";
                    int waiting = 0;
                    foreach (var crew in Crew)
                        if (crew.WaitingForBed) waiting++;
                    if (room.Present > 0 && supply < 0.98f) Set(room, RoomAlert.Warning, "NO POWER: SLOW REST");
                    else if (waiting > 0) Set(room, RoomAlert.Warning, $"BEDS FULL: {waiting} WAITING");
                    else Set(room, room.Present > 0 ? RoomAlert.Ok : RoomAlert.Idle, "");
                    break;
            }
        }

        /// <summary>
        /// A broken machine is red only when nobody is on the way to fix it. Once repairs are
        /// under way (or crew are heading there) it drops to amber.
        /// </summary>
        void BrokenStatus(Room room, bool helpComing)
        {
            room.Activity = 0f;
            room.Meter = room.RepairProgress;

            if (room.Repairers > 0)
            {
                float left = (1f - room.RepairProgress) * tuning.repairSeconds / Mathf.Max(0.01f, room.RepairWork);
                room.RateText = $"repairing {Mathf.FloorToInt(room.RepairProgress * 100f)}%  ~{Mathf.CeilToInt(left)}s";
                Set(room, RoomAlert.Warning, "REPAIRING");
            }
            else if (room.Present > 0)
            {
                // Everyone here repaired something recently and needs a moment.
                float wait = float.MaxValue;
                foreach (var crew in room.Assigned)
                    if (!crew.IsMoving && !crew.IsFainted) wait = Mathf.Min(wait, crew.RepairCooldown);
                room.RateText = $"crew can repair in {Mathf.CeilToInt(wait)}s";
                Set(room, RoomAlert.Warning, "BROKEN");
            }
            else if (helpComing)
            {
                room.RateText = "crew on the way";
                Set(room, RoomAlert.Warning, "BROKEN");
            }
            else if (RestingOnJob(room, out var rester, out float back))
            {
                // Whoever has this job will fix it when they're back from rest.
                room.RateText = RestingText(rester, back);
                Set(room, RoomAlert.Warning, "BROKEN");
            }
            else
            {
                room.RateText = "send crew to repair";
                Set(room, RoomAlert.Failing, "BROKEN");
            }
        }

        /// <summary>
        /// A machine nobody is standing at. If its crew is resting it's amber (they'll be back on
        /// their own); red only when nobody has the job and the station needs it.
        /// </summary>
        void NoCrewStatus(Room room, bool helpComing, bool resourceLow)
        {
            if (helpComing)
            {
                room.RateText = "crew on the way";
                Set(room, RoomAlert.Idle, "");
            }
            else if (RestingOnJob(room, out var rester, out float back))
            {
                room.RateText = RestingText(rester, back);
                Set(room, resourceLow ? RoomAlert.Warning : RoomAlert.Idle, "CREW RESTING");
            }
            else
            {
                room.RateText = "nobody's job";
                Set(room, resourceLow ? RoomAlert.Failing : RoomAlert.Idle, "NO CREW");
            }
        }

        /// <summary>The crew member with this job who'll be back from rest soonest, if any.</summary>
        bool RestingOnJob(Room room, out CrewMember rester, out float back)
        {
            rester = null;
            back = 0f;
            foreach (var crew in Crew)
            {
                if (crew.Job != room || crew.Room == room) continue;
                float left = RestSecondsLeft(crew);
                if (rester == null || left < back)
                {
                    rester = crew;
                    back = left;
                }
            }
            return rester != null;
        }

        static string RestingText(CrewMember crew, float back) =>
            back < 999f ? $"{crew.Name} resting, back in ~{Mathf.CeilToInt(back)}s" : $"{crew.Name} resting";

        static void Set(Room room, RoomAlert alert, string status)
        {
            room.Alert = alert;
            room.StatusText = status;
        }
    }
}
