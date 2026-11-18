using System.Collections.Generic;
using UnityEngine;

namespace DeepStation
{
    public enum RoomType { Quarters, Scrubber, Generator }

    /// <summary>How a room is doing, from the player's point of view.</summary>
    public enum RoomAlert
    {
        Idle,     // nobody here and nothing urgent
        Ok,       // working as intended
        Warning,  // working, but at reduced output
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

        // Recomputed every tick by Station.
        public int Present;
        public float Workforce;
        /// <summary>0-1 fill for the meter above the room.</summary>
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
        public float Stamina;
        public Room Room;
        public int Slot;
        /// <summary>Feet position in world space.</summary>
        public Vector2 Position;
        /// <summary>Remaining waypoints. Empty means the crew member has arrived.</summary>
        public readonly List<Vector2> Path = new List<Vector2>();
        public bool FacingRight = true;
        public bool Climbing;

        public bool IsMoving => Path.Count > 0;
        public bool IsExhausted => Stamina <= 0.01f;
        public bool IsResting => !IsMoving && Room.Type == RoomType.Quarters;

        public CrewMember(string name, Color color, float stamina)
        {
            Name = name;
            Color = color;
            Stamina = stamina;
        }
    }
}
