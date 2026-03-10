namespace FileMakerDataApi.Models;

/// <summary>
/// Controls how dates are formatted in Data API responses.
/// </summary>
public enum DateFormat
{
    /// <summary>FileMaker default date format.</summary>
    Default = 0,

    /// <summary>Date format based on the file's locale setting.</summary>
    FileLocale = 1,

    /// <summary>ISO 8601 date format.</summary>
    Iso8601 = 2
}
