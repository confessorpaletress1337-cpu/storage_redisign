using StorageClient.Redesign.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StorageClient.Redesign.Services;

public interface IContainerService
{
    Task<IReadOnlyList<ContainerWithDetailDto>> GetContainersAsync(
        int? documentId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypesAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(
        int documentTypeId,
        string nameFilter,
        CancellationToken cancellationToken = default);
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
        CancellationToken cancellationToken = default)
    {
        string filters = documentId.HasValue
            ? $"Operation.DocumentId=={documentId.Value},HidePairedRemainders==true"
            : string.Empty;

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

    private async Task<T> PostAsync<T>(
        string endpoint,
        object request,
        CancellationToken cancellationToken)
    {
        EnsureAuthorized();

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            endpoint,
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body)
                    ? $"API вернул ошибку {(int)response.StatusCode}."
                    : body,
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("API вернул пустой ответ.");
    }

    private void EnsureAuthorized()
    {
        if (!_authState.IsAuthenticated || string.IsNullOrWhiteSpace(_authState.AccessToken))
        {
            throw new InvalidOperationException("Пользователь не авторизован.");
        }

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authState.AccessToken);
    }

    public void Dispose() => _client.Dispose();
}
