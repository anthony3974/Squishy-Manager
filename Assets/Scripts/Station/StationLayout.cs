using System.Collections.Generic;
using UnityEngine;

namespace DeepStation
{
    /// <summary>
    /// World geometry of the cutaway, shared by the simulation (walking paths) and the view.
    /// Floor 0 is the top floor; floors stack downward. Column 0 is left of the ladder shaft,
    /// column 1 is right of it.
    /// </summary>
    public static class StationLayout
    {
        public const float RoomWidth = 3.7f;
        public const float RoomHeight = 2.5f;
        public const float ShaftWidth = 1.1f;
        public const float FloorSpacing = 3.7f;
        public const float SlabHeight = 0.18f;
        public const float LadderX = 0f;

        /// <summary>The ocean is drawn between these depths and the camera stays inside them.</summary>
        public const float SurfaceY = 16f;
        public const float DeepY = -40f;

        /// <summary>Visible world width the camera aims for in portrait.</summary>
        public const float ViewWidth = 10f;

        public static float FloorBaseY(int floor) => -floor * FloorSpacing;
        public static float WalkY(int floor) => FloorBaseY(floor) + SlabHeight;
        public static float ColumnCenterX(int column) => (column == 0 ? -1f : 1f) * (ShaftWidth + RoomWidth) * 0.5f;

        public static Rect RoomBounds(int floor, int column)
        {
            float cx = ColumnCenterX(column);
            return new Rect(cx - RoomWidth * 0.5f, FloorBaseY(floor), RoomWidth, RoomHeight);
        }

        /// <summary>Where crew stand inside a room, spread around the room's equipment.</summary>
        public static Vector2[] SlotPositions(RoomType type, int floor, int column, int capacity)
        {
            float spread = type == RoomType.Quarters ? 0.85f : type == RoomType.Scrubber ? 1.0f : 1.15f;
            float cx = ColumnCenterX(column);
            var slots = new Vector2[capacity];
            for (int i = 0; i < capacity; i++)
            {
                float offset = capacity == 1 ? -spread : Mathf.Lerp(-spread, spread, i / (capacity - 1f));
                slots[i] = new Vector2(cx + offset, WalkY(floor));
            }
            return slots;
        }

        /// <summary>
        /// Waypoints from one point to another: along the floor, up or down the ladder, along the floor.
        /// </summary>
        public static void BuildPath(Vector2 from, Vector2 to, List<Vector2> path)
        {
            path.Clear();
            if (Mathf.Abs(from.y - to.y) < 0.01f)
            {
                path.Add(to);
                return;
            }
            path.Add(new Vector2(LadderX, from.y));
            path.Add(new Vector2(LadderX, to.y));
            path.Add(to);
        }
    }
}
