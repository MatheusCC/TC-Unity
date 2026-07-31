using UnityEngine;
using Unity.AI.Navigation;

namespace PawsAndCare.Building
{
    /// <summary>
    /// Boot-time facility orchestrator. Auto-discovers scene RoomMarkers and GridFootprints, registers
    /// their rooms/occupancy with the GridSystem, and bakes the NavMesh. All level geometry (floors,
    /// stations) is authored in the scene; this only wires it into the grid at boot, so there are no
    /// manual lists to keep in sync.
    /// </summary>
    public class FacilityBuilder : MonoBehaviour
    {
        [SerializeField]
        private GridSystem gridSystem = null;
        [SerializeField]
        private NavMeshSurface navMeshSurface = null;

        /// <summary>
        /// Registers scene rooms + placed objects with the GridSystem and bakes the NavMesh.
        /// Called by GameManager during boot so ordering vs. other systems (worker spawn) is deterministic.
        /// </summary>
        public void Build()
        {
            if (gridSystem != null)
            {
                RegisterRooms();
                RegisterPlacedObjects();

                // Bake after rooms/occupancy are registered. Scene-placed station prefabs carry the
                // NavMeshModifiers that carve them out of the NavMesh bake.
                if (navMeshSurface != null)
                {
                    navMeshSurface.BuildNavMesh();
                }
                else
                {
                    Debug.LogWarning("[FacilityBuilder] NavMeshSurface reference is missing — assign one in the inspector.", this);
                }
            }
            else
            {
                Debug.LogError("[FacilityBuilder] GridSystem reference is missing — assign one in the inspector.", this);
            }
        }

        // One-time boot scan (not a hot path): every RoomMarker in the scene registers its floor-derived
        // cells as a room. FindObjectsByType avoids a hand-maintained list that could silently miss a room.
        private void RegisterRooms()
        {
            RoomMarker[] markers = FindObjectsByType<RoomMarker>(FindObjectsSortMode.None);

            for (int i = 0; i < markers.Length; i++)
            {
                Room room = gridSystem.CreateRoom(markers[i].RoomType, markers[i].GetCells(gridSystem));

                if (room != null)
                {
                    // Treat the marker's GameObject (the room floor) as the room's anchor object.
                    room.AddPlacedObject(markers[i].gameObject);
                }
            }
        }

        // One-time boot scan: every authored GridFootprint stamps its cells occupied, so IsCellAvailable
        // is truthful for hand-placed stations exactly as it is for runtime-placed ones.
        private void RegisterPlacedObjects()
        {
            GridFootprint[] footprints = FindObjectsByType<GridFootprint>(FindObjectsSortMode.None);

            for (int i = 0; i < footprints.Length; i++)
            {
                footprints[i].Occupy(gridSystem);
            }
        }
    }
}
