using UnityEngine;

namespace PawsAndCare.Building
{
    /// <summary>
    /// Single cell in the grid — owns its coordinate, occupancy, and room assignment.
    /// Position is fixed at construction; mutate the rest via setters. (Pathfinding is owned by the
    /// NavMesh, not the grid, so cells carry no walkability flag.)
    /// </summary>
    public class GridCell
    {
        private readonly Vector2Int position;
        private bool isOccupied;
        private GameObject occupiedBy;
        private int roomId;

        public Vector2Int Position { get { return position; } }
        public bool IsOccupied { get { return isOccupied; } }
        public GameObject OccupiedBy { get { return occupiedBy; } }
        public int RoomId { get { return roomId; } }

        public GridCell(Vector2Int position)
        {
            this.position = position;
            this.isOccupied = false;
            this.occupiedBy = null;
            this.roomId = 0;
        }

        /// <summary>
        /// Sets the occupant. Pass null to free the cell.
        /// </summary>
        public void SetOccupied(GameObject occupant)
        {
            occupiedBy = occupant;
            isOccupied = (occupant != null);
        }

        /// <summary>
        /// Assigns this cell to a room. Use 0 for "unassigned".
        /// </summary>
        public void SetRoomId(int newRoomId)
        {
            roomId = newRoomId;
        }
    }
}