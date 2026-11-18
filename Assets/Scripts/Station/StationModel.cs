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

        /// <summary>Crew assigned here, including any still walking over.</summary>
        public readonly List<CrewMember> Assigned = new List<CrewMember>();

        // Recomputed every tick by Station.
        public int Present;
        public float Workforce;
        public RoomAlert Alert;
        public string StatusText = "";
        public string RateText = "";

        public Room(string name, RoomType type, int floor, int column, int capacity)
        {
            Name = name;
            Type = type;
            Floor = floor;
            Column = column;
            Capacity = capacity;
        }
    }

    public class CrewMember
    {
        public readonly string Name;
        public readonly Color Color;
        public float Stamina;
        public Room Room;
        public float TravelLeft;
        /// <summary>Bumped on every move so views can restart their walk animation.</summary>
        public int MoveId;

        public bool IsMoving => TravelLeft > 0f;
        public bool IsExhausted => Stamina <= 0.01f;

        public CrewMember(string name, Color color, float stamina)
        {
            Name = name;
            Color = color;
            Stamina = stamina;
        }
    }
}
