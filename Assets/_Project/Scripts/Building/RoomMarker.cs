using System.Collections.Generic;
using UnityEngine;

namespace PawsAndCare.Building
{
    /// <summary>
    /// Scene-placed marker that registers its GameObject as a room with the GridSystem at boot. Attach
    /// to a room's floor; the room's cells are derived from that floor's world bounds — you size the
    /// floor visually and the cells follow, so there are no grid coordinates to hand-author. Only the
    /// RoomType is set here.
    /// </summary>
    public class RoomMarker : MonoBehaviour
    {
        [SerializeField]
        private RoomType roomType = RoomType.RECEPTION;

        public RoomType RoomType
        {
            get { return roomType; }
        }

        /// <summary>
        /// Returns the grid cells this room covers, derived from the floor's world bounds via the grid.
        /// Empty if the floor has no renderers to measure.
        /// </summary>
        public List<Vector2Int> GetCells(GridSystem grid)
        {
            List<Vector2Int> cells;

            if (grid != null && TryGetWorldBounds(out Bounds bounds))
            {
                cells = grid.GetCellsInBounds(bounds);
            }
            else
            {
                cells = new List<Vector2Int>();
                Debug.LogError("[RoomMarker] No GridSystem or no renderer bounds to derive cells from.", this);
            }

            return cells;
        }

        // World AABB of this room's floor, encapsulating all child renderers.
        private bool TryGetWorldBounds(out Bounds bounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            bool hasBounds = renderers.Length > 0;

            if (hasBounds)
            {
                bounds = renderers[0].bounds;

                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
            else
            {
                bounds = new Bounds(transform.position, Vector3.zero);
            }

            return hasBounds;
        }

        // Draws the cells this room actually claims, live in the scene view — resize the floor and the
        // filled cells follow, so placement is verifiable without entering play mode. Editor-only
        // callback; FindFirstObjectByType is fine here since gizmos never run in the runtime hot path.
        private void OnDrawGizmos()
        {
            GridSystem grid = FindFirstObjectByType<GridSystem>();

            if (grid != null && TryGetWorldBounds(out Bounds bounds))
            {
                List<Vector2Int> cells = grid.GetCellsInBounds(bounds);
                Vector3 cellSize = new Vector3(grid.CellSize * 0.9f, 0.05f, grid.CellSize * 0.9f);

                Gizmos.color = new Color(0.0f, 0.85f, 1.0f, 0.65f);

                for (int i = 0; i < cells.Count; i++)
                {
                    Gizmos.DrawCube(grid.GridToWorld(cells[i]), cellSize);
                }
            }
        }
    }
}
