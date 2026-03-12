namespace FileMakerDataApi.Models;

/// <summary>
/// A sort instruction applied to a record query.
/// </summary>
public class SortField
{
    /// <summary>The FileMaker field name to sort by.</summary>
    public required string FieldName { get; set; }

    /// <summary>Sort direction. Defaults to ascending.</summary>
    public SortOrder SortOrder { get; set; } = SortOrder.Ascend;
}
