namespace FileMakerDataApi.Models;

/// <summary>
/// A single find criterion block for a FileMaker Data API find request.
/// Multiple FindRequest objects in one call are treated as OR conditions.
/// Fields within a single FindRequest are treated as AND conditions.
/// </summary>
public class FindRequest
{
    /// <summary>
    /// Field name / value pairs that make up this criterion block.
    /// Values support FileMaker find operators (e.g. "==Exact", ">100", "10...20").
    /// </summary>
    public required Dictionary<string, string> Fields { get; set; }

    /// <summary>
    /// When true, records matching this criterion are excluded from the result set.
    /// Equivalent to FileMaker's omit flag.
    /// </summary>
    public bool Omit { get; set; } = false;
}
