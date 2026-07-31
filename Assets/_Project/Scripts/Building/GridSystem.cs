using System.Collections.Generic;
using UnityEngine;

namespace PawsAndCare.Building
{
    /// <summary>
    /// Owns the 2D grid of cells used for placement, room assignment, and pathfinding. The grid is
    /// built AROUND the authored lot floor: its origin, width, and height are derived from that floor's
    /// world bounds so nothing is sized by hand — you author the floor in world space and the grid
    /// discretizes it at cellSize granularity. Coordinates: grid (x, y) maps to world (X, Z) relative
    /// to the derived Origin (the floor's min corner); cell centers are offset by cellSize/2.
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("World units per cell. TDD §12.1: 1 metre per cell.")]
        private float cellSize = 1.0f;

        [SerializeField]
        [Tooltip("The lot floor. The grid sizes itself to the combined world bounds of this object's renderers (reference a parent that spans the whole lot, including locked rooms).")]
        private Transform lotFloor = null;

        [SerializeField]
        private Color occupiedGizmoColor = new Color(0.88f, 0.44f, 0.33f, 0.4f);

        private const float GIZMO_OVERLAY_SCALE = 0.9f;
        private const float GIZMO_OVERLAY_HEIGHT = 0.05f;
        // Nudge the max corner inward before flooring to a cell, so a floor edge sitting exactly on a
        // cell boundary counts the last covered cell rather than the empty one past it.
        private const float EDGE_EPSILON = 0.001f;

        private GridCell[,] cells;
        private readonly List<Room> rooms = new List<Room>();
        private int nextRoomId = 1;

        // Derived from the lot floor (RecomputeMetrics). Cached at runtime; recomputed live in the
        // editor so gizmos and the camera track the floor as it is resized.
        private Vector3 gridOrigin;
        private int width;
        private int height;

        public float CellSize { get { return cellSize; } }
        public List<Room> Rooms { get { return rooms; } }

        /// <summary>World-space min corner of the grid (the lot floor's min X/Z at its surface Y).</summary>
        public Vector3 Origin
        {
            get
            {
                if (!Application.isPlaying)
                {
                    RecomputeMetrics();
                }

                return gridOrigin;
            }
        }

        public int Width
        {
            get
            {
                if (!Application.isPlaying)
                {
                    RecomputeMetrics();
                }

                return width;
            }
        }

        public int Height
        {
            get
            {
                if (!Application.isPlaying)
                {
                    RecomputeMetrics();
                }

                return height;
            }
        }

        private void Awake()
        {
            RecomputeMetrics();

            if (width > 0 && height > 0)
            {
                cells = new GridCell[width, height];

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        cells[x, y] = new GridCell(new Vector2Int(x, y));
                    }
                }
            }
            else
            {
                Debug.LogError("[GridSystem] Could not derive grid size — assign a lotFloor with renderers. No cells were created.", this);
            }
        }

        // Derives origin/width/height from the lot floor's combined renderer bounds. Sets zero size on
        // a missing floor so callers fail safe rather than on stale numbers.
        private void RecomputeMetrics()
        {
            if (lotFloor != null)
            {
                Renderer[] renderers = lotFloor.GetComponentsInChildren<Renderer>();

                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;

                    for (int i = 1; i < renderers.Length; i++)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }

                    // Origin at the floor's min X/Z, at its top surface Y so placed objects sit on it.
                    gridOrigin = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
                    width = Mathf.CeilToInt(bounds.size.x / cellSize);
                    height = Mathf.CeilToInt(bounds.size.z / cellSize);
                }
                else
                {
                    gridOrigin = lotFloor.position;
                    width = 0;
                    height = 0;
                }
            }
            else
            {
                gridOrigin = transform.position;
                width = 0;
                height = 0;
            }
        }

        /// <summary>
        /// Returns the cell at the given grid position, or null if out of bounds or before the grid is built.
        /// </summary>
        public GridCell GetCell(Vector2Int position)
        {
            GridCell result = null;

            if (cells != null && IsInBounds(position))
            {
                result = cells[position.x, position.y];
            }

            return result;
        }

        /// <summary>
        /// Converts a grid coordinate to the world-space center of that cell.
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            Vector3 origin = Origin;
            // World position = grid origin + grid index × cell size (to the cell's near corner)
            //                + half a cell (corner → center). Y stays at the grid's ground level.
            float worldX = origin.x + (gridPos.x * cellSize) + (cellSize * 0.5f);
            float worldZ = origin.z + (gridPos.y * cellSize) + (cellSize * 0.5f);
            return new Vector3(worldX, origin.y, worldZ);
        }

        /// <summary>
        /// Converts a world position to the grid coordinate it falls within.
        /// May return out-of-bounds coordinates; use GetCell for safe lookup.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 origin = Origin;
            // Express the world point in grid-local space, then floor (rounds toward -inf, so points
            // just left/below the grid map to negative cells for correct out-of-bounds detection).
            int gridX = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
            int gridY = Mathf.FloorToInt((worldPos.z - origin.z) / cellSize);
            return new Vector2Int(gridX, gridY);
        }

        /// <summary>
        /// True when the cell exists and is not occupied.
        /// </summary>
        public bool IsCellAvailable(Vector2Int position)
        {
            GridCell cell = GetCell(position);

            bool available = cell != null && !cell.IsOccupied;

            return available;
        }

        /// <summary>
        /// True when every cell of a footprint (origin plus size in cells) is available. Used by
        /// placement to validate a multi-cell object before committing.
        /// </summary>
        public bool AreCellsAvailable(Vector2Int origin, Vector2Int footprint)
        {
            bool available = true;

            for (int x = 0; x < footprint.x && available; x++)
            {
                for (int y = 0; y < footprint.y && available; y++)
                {
                    if (!IsCellAvailable(new Vector2Int(origin.x + x, origin.y + y)))
                    {
                        available = false;
                    }
                }
            }

            return available;
        }

        /// <summary>
        /// Returns the world-AABB-derived cells a world rectangle covers, clamped to the grid. Lets
        /// scene objects (rooms, placed items) claim cells from their world bounds without hand-typed
        /// coordinates.
        /// </summary>
        public List<Vector2Int> GetCellsInBounds(Bounds worldBounds)
        {
            List<Vector2Int> covered = new List<Vector2Int>();

            Vector2Int min = WorldToGrid(worldBounds.min);
            Vector2Int max = WorldToGrid(new Vector3(worldBounds.max.x - EDGE_EPSILON, 0.0f, worldBounds.max.z - EDGE_EPSILON));

            int minX = Mathf.Max(0, min.x);
            int minY = Mathf.Max(0, min.y);
            int maxX = Mathf.Min(Width - 1, max.x);
            int maxY = Mathf.Min(Height - 1, max.y);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    covered.Add(new Vector2Int(x, y));
                }
            }

            return covered;
        }

        /// <summary>
        /// Creates a new room of the given type and assigns the listed cells to it.
        /// Cells that are out of bounds or already assigned to another room are
        /// skipped with a warning. Returns the created Room.
        /// </summary>
        public Room CreateRoom(RoomType type, List<Vector2Int> cellsToAssign)
        {
            Room room = new Room(nextRoomId, type);
            nextRoomId++;

            for (int i = 0; i < cellsToAssign.Count; i++)
            {
                Vector2Int cellPos = cellsToAssign[i];
                GridCell cell = GetCell(cellPos);

                if (cell != null)
                {
                    if (cell.RoomId == 0)
                    {
                        cell.SetRoomId(room.RoomId);
                        room.AddCell(cellPos);
                    }
                    else
                    {
                        Debug.LogWarning($"[GridSystem] CreateRoom: cell {cellPos} already belongs to room {cell.RoomId}, skipping.", this);
                    }
                }
                else
                {
                    Debug.LogWarning($"[GridSystem] CreateRoom: cell {cellPos} is out of bounds, skipping.", this);
                }
            }

            rooms.Add(room);
            return room;
        }

        /// <summary>
        /// Returns the room with the given ID, or null if not found.
        /// </summary>
        public Room GetRoomById(int id)
        {
            Room result = null;

            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].RoomId == id)
                {
                    result = rooms[i];
                }
            }

            return result;
        }

        private bool IsInBounds(Vector2Int position)
        {
            return position.x >= 0
                && position.x < width
                && position.y >= 0
                && position.y < height;
        }

        private void OnDrawGizmos()
        {
            // Recompute from the floor so the overlay tracks it live in the scene view (no play needed).
            RecomputeMetrics();

            if (width > 0 && height > 0)
            {
                Gizmos.color = Color.white;

                // Vertical gridlines: one per column boundary (N columns → N+1 lines).
                for (int x = 0; x <= width; x++)
                {
                    Vector3 start = gridOrigin + new Vector3(x * cellSize, 0.0f, 0.0f);
                    Vector3 end = gridOrigin + new Vector3(x * cellSize, 0.0f, height * cellSize);
                    Gizmos.DrawLine(start, end);
                }

                // Horizontal gridlines: one per row boundary.
                for (int y = 0; y <= height; y++)
                {
                    Vector3 start = gridOrigin + new Vector3(0.0f, 0.0f, y * cellSize);
                    Vector3 end = gridOrigin + new Vector3(width * cellSize, 0.0f, y * cellSize);
                    Gizmos.DrawLine(start, end);
                }

                // Per-cell occupied overlay only renders at runtime (cells[,] exists then).
                if (cells != null)
                {
                    Vector3 overlaySize = new Vector3(cellSize * GIZMO_OVERLAY_SCALE, GIZMO_OVERLAY_HEIGHT, cellSize * GIZMO_OVERLAY_SCALE);
                    Gizmos.color = occupiedGizmoColor;

                    for (int x = 0; x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            if (cells[x, y].IsOccupied)
                            {
                                Gizmos.DrawCube(GridToWorld(new Vector2Int(x, y)), overlaySize);
                            }
                        }
                    }
                }
            }
        }
    }
}
