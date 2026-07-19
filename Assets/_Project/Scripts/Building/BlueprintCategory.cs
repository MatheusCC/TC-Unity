namespace PawsAndCare.Building
{
    /// <summary>
    /// Broad class of a blueprint, used to filter the catalog and drive placement rules
    /// (e.g. only stations register with the dispatch system). Append-only per CLAUDE.md.
    /// </summary>
    public enum BlueprintCategory
    {
        STATION = 0,
        FURNITURE = 1,
        DECORATION = 2
    }
}
