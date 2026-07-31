using System.Collections.Generic;
using UnityEngine;

namespace PawsAndCare.Building
{
    /// <summary>
    /// Marks the grid cells a placed object (station/furniture/decoration) occupies. Cells are derived
    /// from the object's world position and its Blueprint footprint — placement and boot registration
    /// both call Occupy, and rearrange/sell call Free. Carrying the Blueprint also lets sell/refund and
    /// save/load know what this instance is.
    /// </summary>
    public class GridFootprint : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The blueprint this instance was placed from — supplies its footprint (and cost for refunds).")]
        private Blueprint blueprint = null;

        private GridSystem gridSystem;
        private readonly List<Vector2Int> occupiedCells = new List<Vector2Int>();

        public Blueprint Blueprint
        {
            get { return blueprint; }
        }

        public IReadOnlyList<Vector2Int> OccupiedCells
        {
            get { return occupiedCells; }
        }

        /// <summary>
        /// Stamps this object's footprint cells as occupied, deriving them from its world position.
        /// Convention: transform.position is the footprint's world centre.
        /// </summary>
        public void Occupy(GridSystem grid)
        {
            if (grid != null)
            {
                gridSystem = grid;
                Free();

                Vector2Int footprint = blueprint != null ? blueprint.Footprint : Vector2Int.one;

                if (blueprint == null)
                {
                    Debug.LogError("[GridFootprint] No blueprint assigned — defaulting to a 1x1 footprint.", this);
                }

                Vector2Int origin = ResolveOriginCell(footprint);

                for (int x = 0; x < footprint.x; x++)
                {
                    for (int y = 0; y < footprint.y; y++)
                    {
                        Vector2Int cellPos = new Vector2Int(origin.x + x, origin.y + y);
                        GridCell cell = gridSystem.GetCell(cellPos);

                        if (cell != null)
                        {
                            cell.SetOccupied(gameObject);
                            occupiedCells.Add(cellPos);
                        }
                        else
                        {
                            Debug.LogWarning($"[GridFootprint] Cell {cellPos} is off the grid — not occupied. Is this object grid-aligned?", this);
                        }
                    }
                }
            }
        }

        /// <summary>Frees the cells this object occupied (for rearrange/sell). Safe to call when unoccupied.</summary>
        public void Free()
        {
            if (gridSystem != null)
            {
                for (int i = 0; i < occupiedCells.Count; i++)
                {
                    GridCell cell = gridSystem.GetCell(occupiedCells[i]);

                    if (cell != null)
                    {
                        cell.SetOccupied(null);
                    }
                }
            }

            occupiedCells.Clear();
        }

        // Min-corner cell of the footprint. transform.position is the footprint centre, so the origin
        // cell's centre is offset back by half the footprint's extent; flooring that (a cell centre)
        // lands robustly on the min-corner cell for any footprint size.
        private Vector2Int ResolveOriginCell(Vector2Int footprint)
        {
            float halfExtentX = (footprint.x - 1) * 0.5f * gridSystem.CellSize;
            float halfExtentZ = (footprint.y - 1) * 0.5f * gridSystem.CellSize;
            Vector3 originCellCentre = transform.position - new Vector3(halfExtentX, 0.0f, halfExtentZ);

            return gridSystem.WorldToGrid(originCellCentre);
        }
    }
}
