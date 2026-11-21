using System.Collections.Generic;
using UnityEngine;

namespace DeepStation
{
    public enum RoomType { Quarters, Scrubber, Generator }

    /// <summary>One personality trait per crew member, each with a small effect shown on selection.</summary>
    public enum CrewTrait
    {
        Tireless,     // stamina drains slower while working
        Tinkerer,     // repairs faster
        QuickNapper   // rests faster
    }

    /// <summary>How a room is doing, from the player's point of view.</summary>
    public enum RoomAlert
    {
        Idle,     // nobody here and nothing urgent
        Ok,       // working as intended
        Warning,  // working, but at reduced output, or a problem that is already being handled
        Failing   // not doing its job and the station needs it to
    }

    public class Room
    {
        public readonly string Name;
        public readonly RoomType Type;
        public readonly int Floor;
        public readonly int Column;
        public readonly int Capacity;
        /// <summary>World-space box of the room's interior (bottom-left origin).</summary>
        public readonly Rect Bounds;
        /// <summary>Where each crew slot stands, on the floor line.</summary>
        public readonly Vector2[] Slots;

        /// <summary>Crew assigned here, including any still walking over.</summary>
        public readonly List<CrewMember> Assigned = new List<CrewMember>();

        /// <summary>Machines wear out and break; Quarters never do.</summary>
        public bool CanBreak => Type != RoomType.Quarters;
        /// <summary>Seconds this machine has run since it was last fixed.</summary>
        public float Wear;
        /// <summary>Running seconds this machine lasts before breaking. Re-rolled after each repair.</summary>
        public float WearLimit;
        public bool Broken;
        /// <summary>0-1 progress of the current repair.</summary>
        public float RepairProgress;

        // Recomputed every tick by Station.
        public int Present;
        /// <summary>Crew here who have fainted (not counted in Present).</summary>
        public int Fainted;
        public float Workforce;
        /// <summary>Crew actually repairing right now, and their combined efficiency.</summary>
        public int Repairers;
        public float RepairWork;
        /// <summary>Running and close to breaking: shakes and smokes as a warning.</summary>
        public bool Rattling;
        /// <summary>0-1 how hard the machine is running (drives rotor and bubble animation).</summary>
        public float Activity;
        /// <summary>0-1 fill for the meter above the room (repair progress while broken).</summary>
        public float Meter;
        public RoomAlert Alert;
        /// <summary>Short warning shown above the room when Warning or Failing.</summary>
        public string StatusText = "";
        /// <summary>What the room is doing right now, e.g. "+3.0 power/s".</summary>
        public string RateText = "";

        public Room(string name, RoomType type, int floor, int column, int capacity)
        {
            Name = name;
            Type = type;
            Floor = floor;
            Column = column;
            Capacity = capacity;
            Bounds = StationLayout.RoomBounds(floor, column);
            Slots = StationLayout.SlotPositions(type, floor, column, capacity);
        }

        public int FreeSlot()
        {
            for (int slot = 0; slot < Capacity; slot++)
            {
                bool taken = false;
                foreach (var crew in Assigned)
                    if (crew.Slot == slot) { taken = true; break; }
                if (!taken) return slot;
            }
            return -1;
        }
    }

    public class CrewMember
    {
        public readonly string Name;
        public readonly Color Color;
        public readonly CrewTrait Trait;
        public float Stamina;
        /// <summary>
        /// Standing job set by the player. The crew member is always either here or resting in
        /// Quarters, and walks back here on their own after every rest.
        /// </summary>
        public Room Job;
        /// <summary>Where they are, or are walking to: their Job, or Quarters while resting.</summary>
        public Room Room;
        /// <summary>The player sent them to work while tired: skip auto-rest until exhausted.</summary>
        public bool KeepWorking;
        /// <summary>Needs a rest but both beds are taken.</summary>
        public bool WaitingForBed;
        public int Slot;
        /// <summary>Feet position in world space.</summary>
        public Vector2 Position;
        /// <summary>Remaining waypoints. Empty means the crew member has arrived.</summary>
        public readonly List<Vector2> Path = new List<Vector2>();
        public bool FacingRight = true;
        public bool Climbing;
        /// <summary>Seconds until this crew member can repair again.</summary>
        public float RepairCooldown;
        /// <summary>The room this crew member last finished repairing.</summary>
        public Room RepairedRoom;
        /// <summary>Set every tick: fixing a broken machine right now.</summary>
        public bool IsRepairing;
        /// <summary>Seconds until a crew member who fainted from lack of air comes round. 0 = awake.</summary>
        public float FaintTime;

        /// <summary>Out cold: can't walk, work, repair or rest until FaintTime runs out.</summary>
        public bool IsFainted => FaintTime > 0f;
        public bool IsMoving => Path.Count > 0;
        public bool IsExhausted => Stamina <= 0.01f;
        public bool IsResting => !IsMoving && Room.Type == RoomType.Quarters;

        public CrewMember(string name, Color color, CrewTrait trait, float stamina, Room job)
        {
            Name = name;
            Color = color;
            Trait = trait;
            Stamina = stamina;
            Job = job;
        }
    }

    /// <summary>A canister of reserve oxygen that vents on its own when the air runs out.</summary>
    public class EmergencyTank
    {
        /// <summary>0 = empty, 1 = full.</summary>
        public float Fill = 1f;
        public bool IsFull => Fill >= 0.999f;
    }
}
