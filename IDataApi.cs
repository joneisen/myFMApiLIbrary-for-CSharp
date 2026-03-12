#pragma warning disable CS1591
using System.Text.Json.Nodes;
using FileMakerDataApi.Models;

namespace FileMakerDataApi;

/// <summary>
/// Defines all operations exposed by the FileMaker Data API wrapper.
/// Mirrors the PHP DataApiInterface contract.
/// Implement <c>await using</c> for clean session teardown via <see cref="IAsyncDisposable"/>.
/// </summary>
public interface IDataApi : IDisposable, IAsyncDisposable
{
    // Authentication

    Task LoginAsync(string apiUsername, string apiPassword, CancellationToken cancellationToken = default);
    Task LoginOauthAsync(string oAuthRequestId, string oAuthIdentifier, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);

    // Record operations

    Task<string> CreateRecordAsync(string layout, Dictionary<string, object?> data, IEnumerable<ScriptDefinition>? scripts = null, Dictionary<string, object?>? portalData = null, CancellationToken cancellationToken = default);
    Task<string> EditRecordAsync(string layout, string recordId, Dictionary<string, object?> data, string? lastModificationId = null, Dictionary<string, object?>? portalData = null, IEnumerable<ScriptDefinition>? scripts = null, CancellationToken cancellationToken = default);
    Task<string> DuplicateRecordAsync(string layout, string recordId, IEnumerable<ScriptDefinition>? scripts = null, CancellationToken cancellationToken = default);
    Task DeleteRecordAsync(string layout, string recordId, IEnumerable<ScriptDefinition>? scripts = null, CancellationToken cancellationToken = default);
    Task<JsonNode> GetRecordAsync(string layout, string recordId, PortalOptions? portalOptions = null, IEnumerable<ScriptDefinition>? scripts = null, string? responseLayout = null, DateFormat? dateFormat = null, CancellationToken cancellationToken = default);
    Task<JsonArray> GetRecordsAsync(string layout, IEnumerable<SortField>? sort = null, int? offset = null, int? limit = null, IEnumerable<PortalOptions>? portals = null, IEnumerable<ScriptDefinition>? scripts = null, string? responseLayout = null, DateFormat? dateFormat = null, CancellationToken cancellationToken = default);
    Task<JsonArray> FindRecordsAsync(string layout, IEnumerable<FindRequest> query, IEnumerable<SortField>? sort = null, int? offset = null, int? limit = null, IEnumerable<PortalOptions>? portals = null, IEnumerable<ScriptDefinition>? scripts = null, string? responseLayout = null, DateFormat? dateFormat = null, CancellationToken cancellationToken = default);

    // Container uploads

    Task UploadToContainerAsync(string layout, string recordId, string containerFieldName, int containerFieldRepetition, string filePath, string? fileName = null, CancellationToken cancellationToken = default);
    Task UploadToContainerFromBytesAsync(string layout, string recordId, string containerFieldName, int containerFieldRepetition, byte[] fileBytes, string fileName, CancellationToken cancellationToken = default);

    // Scripts

    Task<string?> ExecuteScriptAsync(string layout, string scriptName, string? scriptParam = null, CancellationToken cancellationToken = default);

    // Global fields
    // Note: layout is not used by the FileMaker globals endpoint - global fields are session-scoped.

    Task SetGlobalFieldsAsync(Dictionary<string, string> globalFields, CancellationToken cancellationToken = default);

    // Token management

    string? GetApiToken();
    bool SetApiToken(string token, DateTime? tokenDate = null);
    DateTime? GetApiTokenDate();
    bool SetApiTokenDate();
    bool IsApiTokenExpired();
    Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default);
    Task<bool> ValidateTokenWithServerAsync(CancellationToken cancellationToken = default);

    // Metadata

    Task<JsonNode?> GetProductInfoAsync(CancellationToken cancellationToken = default);
    Task<JsonNode?> GetDatabaseNamesAsync(CancellationToken cancellationToken = default);
    Task<JsonNode?> GetLayoutNamesAsync(CancellationToken cancellationToken = default);
    Task<JsonNode?> GetScriptNamesAsync(CancellationToken cancellationToken = default);
    Task<JsonNode?> GetLayoutMetadataAsync(string layout, string? recordId = null, CancellationToken cancellationToken = default);

    // Version

    void SetDapiVersion(DapiVersion version);
}
