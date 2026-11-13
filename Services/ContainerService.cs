using StorageClient.Redesign.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StorageClient.Redesign.Services;

public interface IContainerService
{
    Task<IReadOnlyList<ContainerWithDetailDto>> GetContainersAsync(
        int? documentId = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypesAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(
        int documentTypeId,
        string nameFilter,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContainerHistoryDto>> GetHistoryAsync(
        int containerId, CancellationToken cancellationToken = default);
    Task<string> DownloadHistoryDocumentAsync(int documentId, CancellationToken cancellationToken = default);
}

public sealed class ContainerService : IContainerService, IDisposable
{
    private readonly IAuthStateService _authState;
    private readonly HttpClient _client;

    public ContainerService(IAppSettings settings, IAuthStateService authState)
    {
        _authState = authState;
        _client = new HttpClient
        {
            BaseAddress = new Uri(settings.BaseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    public async Task<IReadOnlyList<ContainerWithDetailDto>> GetContainersAsync(
        int? documentId = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        string filters = documentId.HasValue
            ? $"Operation.DocumentId=={documentId.Value},HidePairedRemainders==true"
            : activeOnly ? "IsActive==1" : string.Empty;

        return await PostAsync<List<ContainerWithDetailDto>>(
            "api/Container/GetFilteredAndSortedContainerssWithDetails",
            new SortRequestDto
            {
                Sorts = "-Name",
                Filters = filters
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypesAsync(
        CancellationToken cancellationToken = default) =>
        await PostAsync<List<DocumentTypeDto>>(
            "api/DocumentType/GetFilteredAndSortedDocumentTypes",
            new SortRequestDto { Sorts = "Name" },
            cancellationToken);

    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(
        int documentTypeId,
        string nameFilter,
        CancellationToken cancellationToken = default)
    {
        string filters = $"TypeId=={documentTypeId}";
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            filters += $",Name@=*{nameFilter.Trim()}";
        }

        return await PostAsync<List<DocumentDto>>(
            "api/Document/GetFilteredAndSortedDocuments",
            new SortRequestDto
            {
                Sorts = "Name",
                Filters = filters
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<ContainerHistoryDto>> GetHistoryAsync(
        int containerId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"api/Container/History?containerId={containerId}");
        using HttpResponseMessage response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<List<ContainerHistoryDto>>(response, cancellationToken);
    }

    public async Task<string> DownloadHistoryDocumentAsync(
        int documentId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"api/Document/Download/{documentId}");
        using HttpResponseMessage response = await SendAsync(request, cancellationToken);

        // Choose the extension from known content types; never use a path supplied by the API.
        string? mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        string? extension = mediaType switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            _ => null
        };
        if (extension is null && (mediaType is null or "application/octet-stream"))
        {
            var disposition = response.Content.Headers.ContentDisposition;
            string? declaredExtension = Path.GetExtension((disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"'))
                ?.ToLowerInvariant();
            if (declaredExtension is ".pdf" or ".jpg" or ".jpeg" or ".png" or ".doc" or ".docx" or ".xls" or ".xlsx")
            {
                extension = declaredExtension;
            }
        }

        if (extension is null)
        {
            throw new InvalidDataException("Формат документа не поддерживается.");
        }

        string folder = Directory.CreateTempSubdirectory("storage-history-").FullName;
        string path = Path.Combine(folder, $"Документ-{documentId}{extension}");
        await using FileStream file = File.Create(path);
        await response.Content.CopyToAsync(file, cancellationToken);
        return path;
    }

    private async Task<T> PostAsync<T>(
        string endpoint,
        object request,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, endpoint) { Content = JsonContent.Create(request) };
        using HttpResponseMessage response = await SendAsync(message, cancellationToken);
        return await ReadJsonAsync<T>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? token = _authState.AccessToken;
        if (!_authState.IsAuthenticated || string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Пользователь не авторизован.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body)
                    ? $"API вернул ошибку {(int)response.StatusCode}."
                    : body,
                null,
                response.StatusCode);
        }
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("API вернул пустой ответ.");

    public void Dispose() => _client.Dispose();
}
