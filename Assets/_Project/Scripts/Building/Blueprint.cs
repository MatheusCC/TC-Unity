using UnityEngine;

namespace PawsAndCare.Building
{
    /// <summary>
    /// Static template for a placeable object (station, furniture, or decoration): what it is, what it
    /// costs, how many grid cells it occupies, and the gates on it. The catalog reads these to build
    /// its entries; build mode reads the footprint/prefab to preview and place. Runtime placement state
    /// (which cells it sits on) lives on the placed instance's GridFootprint, not here.
    /// </summary>
    [CreateAssetMenu(fileName = "Blueprint_", menuName = "PawsAndCare/Building/Blueprint")]
    public class Blueprint : ScriptableObject
    {
        private const int MIN_FOOTPRINT_CELLS = 1;

        [SerializeField]
        [Tooltip("Name shown in the catalog, e.g. \"Bathing Station\".")]
        private string displayName = "New Blueprint";

        [SerializeField]
        [TextArea(2, 4)]
        [Tooltip("Short catalog description.")]
        private string description = "";

        [SerializeField]
        [Tooltip("One-time purchase cost in dollars.")]
        private float cost = 100.0f;

        [SerializeField]
        [Tooltip("Footprint in grid cells (x by y). Minimum 1x1.")]
        private Vector2Int footprint = Vector2Int.one;

        [SerializeField]
        [Tooltip("Prefab instantiated on placement. Must carry a GridFootprint (and a ServiceStation for STATION category).")]
        private GameObject placedPrefab = null;

        [SerializeField]
        [Tooltip("Icon shown in the catalog entry.")]
        private Sprite icon = null;

        [SerializeField]
        [Tooltip("Broad class — filters the catalog and drives placement rules.")]
        private BlueprintCategory category = BlueprintCategory.STATION;

        [SerializeField]
        [Tooltip("Room type this must be placed inside. NONE = any room.")]
        private RoomType requiredRoomType = RoomType.NONE;

        [SerializeField]
        [Range(0.0f, 100.0f)]
        [Tooltip("Reputation needed before this appears unlocked in the catalog. 0 = available from start.")]
        private float requiredReputation = 0.0f;

        public string DisplayName
        {
            get { return displayName; }
        }

        public string Description
        {
            get { return description; }
        }

        public float Cost
        {
            get { return cost; }
        }

        /// <summary>Footprint in grid cells, clamped to a minimum of 1x1 so a mis-authored asset never spans zero cells.</summary>
        public Vector2Int Footprint
        {
            get { return new Vector2Int(Mathf.Max(MIN_FOOTPRINT_CELLS, footprint.x), Mathf.Max(MIN_FOOTPRINT_CELLS, footprint.y)); }
        }

        public GameObject PlacedPrefab
        {
            get { return placedPrefab; }
        }

        public Sprite Icon
        {
            get { return icon; }
        }

        public BlueprintCategory Category
        {
            get { return category; }
        }

        public RoomType RequiredRoomType
        {
            get { return requiredRoomType; }
        }

        public float RequiredReputation
        {
            get { return requiredReputation; }
        }
    }
}
