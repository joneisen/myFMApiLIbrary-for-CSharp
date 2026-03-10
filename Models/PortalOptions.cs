namespace FileMakerDataApi.Models;

/// <summary>
/// Controls which portal rows are returned for a given portal in a record response.
/// </summary>
public class PortalOptions
{
    /// <summary>The portal object name or occurrence name as it appears on the layout.</summary>
    public required string Name { get; set; }

    /// <summary>Maximum number of portal rows to return.</summary>
    public int? Limit { get; set; }

    /// <summary>Starting row offset (1-based) for portal row retrieval.</summary>
    public int? Offset { get; set; }
}
