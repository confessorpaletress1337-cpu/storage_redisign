using StorageClient.Redesign.DTOs;
using StorageClient.Redesign.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace StorageClient.Redesign.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IContainerService _containerService;
    private readonly IAuthStateService _authState;
    private readonly IThemeService _themeService;
    private readonly AsyncRelayCommand _refreshCommand;

    private ContainerWithDetailDto? _selectedContainer;
    private string _containerNumberFilter = string.Empty;
    private string _articleFilter = string.Empty;
    private string _elementFilter = string.Empty;
    private string _cellFilter = string.Empty;
    private string _descriptionFilter = string.Empty;
    private string _documentNameFilter = string.Empty;
    private bool _showReservedOnly;
    private bool _showDefectedOnly;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private DocumentTypeDto? _selectedDocumentType;
    private DocumentDto? _selectedDocument;
    private CancellationTokenSource? _documentSearchCancellation;

    public MainViewModel(
        IContainerService containerService,
        IAuthStateService authState,
        IThemeService themeService)
    {
        _containerService = containerService;
        _authState = authState;
        _themeService = themeService;

        ContainersView = CollectionViewSource.GetDefaultView(Containers);
        ContainersView.Filter = MatchesFilters;

        _refreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading);
        ClearFiltersCommand = new RelayCommand(ClearFilters, HasFilters);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
    }

    public ObservableCollection<ContainerWithDetailDto> Containers { get; } = [];
    public ObservableCollection<DocumentTypeDto> DocumentTypes { get; } = [];
    public ObservableCollection<DocumentDto> Documents { get; } = [];
    public ICollectionView ContainersView { get; }

    public ICommand RefreshCommand => _refreshCommand;
    public ICommand ClearFiltersCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public string Username => _authState.Username ?? "Пользователь";
    public string Permission => _authState.Permission;
    public bool IsDarkTheme => _themeService.IsDarkTheme;

    public ContainerWithDetailDto? SelectedContainer
    {
        get => _selectedContainer;
        set => Set(ref _selectedContainer, value);
    }

    public string ContainerNumberFilter
    {
        get => _containerNumberFilter;
        set
        {
            if (!Set(ref _containerNumberFilter, value))
            {
                return;
            }

            RefreshFilters();
        }
    }

    public string ArticleFilter
    {
        get => _articleFilter;
        set
        {
            if (Set(ref _articleFilter, value))
            {
                RefreshFilters();
            }
        }
    }

    public string ElementFilter
    {
        get => _elementFilter;
        set
        {
            if (Set(ref _elementFilter, value))
            {
                RefreshFilters();
            }
        }
    }

    public string CellFilter
    {
        get => _cellFilter;
        set
        {
            if (Set(ref _cellFilter, value))
            {
                RefreshFilters();
            }
        }
    }

    public string DescriptionFilter
    {
        get => _descriptionFilter;
        set
        {
            if (Set(ref _descriptionFilter, value))
            {
                RefreshFilters();
            }
        }
    }

    public string DocumentNameFilter
    {
        get => _documentNameFilter;
        set
        {
            if (Set(ref _documentNameFilter, value))
            {
                ScheduleDocumentLoad();
                ((RelayCommand)ClearFiltersCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public DocumentTypeDto? SelectedDocumentType
    {
        get => _selectedDocumentType;
        set
        {
            if (!Set(ref _selectedDocumentType, value))
            {
                return;
            }

            _selectedDocument = null;
            OnPropertyChanged(nameof(SelectedDocument));
            OnPropertyChanged(nameof(HasSelectedDocumentType));
            Documents.Clear();
            ScheduleDocumentLoad(immediate: true);
            ((RelayCommand)ClearFiltersCommand).RaiseCanExecuteChanged();
        }
    }

    public DocumentDto? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (Set(ref _selectedDocument, value))
            {
                _ = LoadAsync();
                ((RelayCommand)ClearFiltersCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedDocumentType => SelectedDocumentType is not null;

    public bool ShowReservedOnly
    {
        get => _showReservedOnly;
        set
        {
            if (Set(ref _showReservedOnly, value))
            {
                RefreshFilters();
            }
        }
    }

    public bool ShowDefectedOnly
    {
        get => _showDefectedOnly;
        set
        {
            if (Set(ref _showDefectedOnly, value))
            {
                RefreshFilters();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (Set(ref _isLoading, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (Set(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public int TotalCount => Containers.Count;
    public int AvailableCount => Containers.Count(item => item.IsActive == 1 && item.IsReserved != 1 && item.IsDefected != 1);
    public int ReservedCount => Containers.Count(item => item.IsActive == 1 && item.IsReserved == 1);
    public int DefectedCount => Containers.Count(item => item.IsActive == 1 && item.IsDefected == 1);

    public async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            IReadOnlyList<ContainerWithDetailDto> loaded =
                await _containerService.GetContainersAsync(SelectedDocument?.Id);

            if (DocumentTypes.Count == 0)
            {
                IReadOnlyList<DocumentTypeDto> documentTypes =
                    await _containerService.GetDocumentTypesAsync();
                foreach (DocumentTypeDto documentType in documentTypes)
                {
                    DocumentTypes.Add(documentType);
                }
            }

            Containers.Clear();
            foreach (ContainerWithDetailDto container in loaded)
            {
                Containers.Add(container);
            }

            RefreshCounters();
            ContainersView.Refresh();
            SelectedContainer = ContainersView.Cast<ContainerWithDetailDto>().FirstOrDefault();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception is HttpRequestException httpException
                ? ApiErrorFormatter.Format(httpException)
                : exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool MatchesFilters(object item)
    {
        if (item is not ContainerWithDetailDto container)
        {
            return false;
        }

        if (!ShowReservedOnly && container.IsReserved == 1)
        {
            return false;
        }

        if (!ShowDefectedOnly && container.IsDefected == 1)
        {
            return false;
        }

        string containerNumber = ContainerNumberFilter.Trim();
        if (containerNumber.Length > 0
            && !Contains(container.Name?.ToString(CultureInfo.InvariantCulture), containerNumber))
        {
            return false;
        }

        string article = ArticleFilter.Trim();
        if (article.Length > 0
            && !Contains(container.Article?.ToString(CultureInfo.InvariantCulture), article))
        {
            return false;
        }

        string element = ElementFilter.Trim();
        if (element.Length > 0
            && !Contains(container.ElementName, element))
        {
            return false;
        }

        string cell = CellFilter.Trim();
        if (cell.Length > 0
            && !Contains(container.CellName, cell)
            && !Contains(container.CellAddressName, cell))
        {
            return false;
        }

        string description = DescriptionFilter.Trim();
        if (description.Length > 0
            && !Contains(container.Description, description))
        {
            return false;
        }

        return true;
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;

    private bool HasFilters() =>
        !string.IsNullOrWhiteSpace(ContainerNumberFilter)
        || !string.IsNullOrWhiteSpace(ArticleFilter)
        || !string.IsNullOrWhiteSpace(ElementFilter)
        || !string.IsNullOrWhiteSpace(CellFilter)
        || !string.IsNullOrWhiteSpace(DescriptionFilter)
        || !string.IsNullOrWhiteSpace(DocumentNameFilter)
        || SelectedDocumentType is not null
        || SelectedDocument is not null
        || ShowReservedOnly
        || ShowDefectedOnly;

    private void ClearFilters()
    {
        ContainerNumberFilter = string.Empty;
        ArticleFilter = string.Empty;
        ElementFilter = string.Empty;
        CellFilter = string.Empty;
        DescriptionFilter = string.Empty;
        DocumentNameFilter = string.Empty;
        _selectedDocument = null;
        OnPropertyChanged(nameof(SelectedDocument));
        _selectedDocumentType = null;
        OnPropertyChanged(nameof(SelectedDocumentType));
        OnPropertyChanged(nameof(HasSelectedDocumentType));
        Documents.Clear();
        ShowReservedOnly = false;
        ShowDefectedOnly = false;
        _ = LoadAsync();
    }

    private void RefreshFilters()
    {
        ContainersView.Refresh();
        ((RelayCommand)ClearFiltersCommand).RaiseCanExecuteChanged();
    }

    private void RefreshCounters()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(ReservedCount));
        OnPropertyChanged(nameof(DefectedCount));
    }

    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    private void ScheduleDocumentLoad(bool immediate = false)
    {
        _documentSearchCancellation?.Cancel();
        _documentSearchCancellation?.Dispose();
        _documentSearchCancellation = new CancellationTokenSource();
        _ = LoadDocumentsAfterDelayAsync(immediate, _documentSearchCancellation.Token);
    }

    private async Task LoadDocumentsAfterDelayAsync(bool immediate, CancellationToken cancellationToken)
    {
        try
        {
            if (!immediate)
            {
                await Task.Delay(250, cancellationToken);
            }

            DocumentTypeDto? documentType = SelectedDocumentType;
            if (documentType is null)
            {
                return;
            }

            IReadOnlyList<DocumentDto> documents = await _containerService.GetDocumentsAsync(
                documentType.Id,
                DocumentNameFilter,
                cancellationToken);

            Documents.Clear();
            foreach (DocumentDto document in documents)
            {
                Documents.Add(document);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = exception is HttpRequestException httpException
                ? ApiErrorFormatter.Format(httpException)
                : exception.Message;
        }
    }
}
