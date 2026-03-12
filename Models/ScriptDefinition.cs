namespace FileMakerDataApi.Models;

/// <summary>
/// Defines a FileMaker script to run alongside a Data API request.
/// </summary>
public class ScriptDefinition
{
    /// <summary>The script name as it appears in FileMaker.</summary>
    public required string Name { get; set; }

    /// <summary>Optional parameter to pass to the script.</summary>
    public string? Param { get; set; }

    /// <summary>When the script runs relative to the request. Defaults to PostRequest.</summary>
    public ScriptType Type { get; set; } = ScriptType.PostRequest;
}
