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
    private const string ContainerLoadKey = "main-containers";
    private const string DocumentSearchKey = "main-documents";
    private const string ContainerLoadOperation = "Загрузка контейнеров";

    private readonly IContainerService _containerService;
    private readonly IAuthStateService _authState;
    private readonly IThemeService _themeService;
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ICommandFactory _commandFactory;
    private AsyncRelayCommand? _refreshCommand;
    private RelayCommand? _clearFiltersCommand;
    private RelayCommand? _toggleThemeCommand;

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
    private bool _clearingFilters;

    public MainViewModel(
        IContainerService containerService,
        IAuthStateService authState,
        IThemeService themeService,
        ITaskExecutionService taskExecutionService,
        ICommandFactory commandFactory)
    {
        _containerService = containerService;
        _authState = authState;
        _themeService = themeService;
        _taskExecutionService = taskExecutionService;
        _commandFactory = commandFactory;

        ContainersView = CollectionViewSource.GetDefaultView(Containers);
        ContainersView.Filter = MatchesFilters;
    }

    public ObservableCollection<ContainerWithDetailDto> Containers { get; } = [];
    public ObservableCollection<DocumentTypeDto> DocumentTypes { get; } = [];
    public ObservableCollection<DocumentDto> Documents { get; } = [];
    public ICollectionView ContainersView { get; }

    public ICommand RefreshCommand => _refreshCommand ??=
        _commandFactory.CreateCommand(LoadAsync, ContainerLoadOperation, ShowError, () => !IsLoading);

    public ICommand ClearFiltersCommand => _clearFiltersCommand ??=
        _commandFactory.CreateCommand(ClearFilters, HasFilters);

    public ICommand ToggleThemeCommand => _toggleThemeCommand ??=
        _commandFactory.CreateCommand(ToggleTheme);

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
        set => Set(ref _containerNumberFilter, value);
    }

    public string ArticleFilter
    {
        get => _articleFilter;
        set => Set(ref _articleFilter, value);
    }

    public string ElementFilter
    {
        get => _elementFilter;
        set => Set(ref _elementFilter, value);
    }

    public string CellFilter
    {
        get => _cellFilter;
        set => Set(ref _cellFilter, value);
    }

    public string DescriptionFilter
    {
        get => _descriptionFilter;
        set => Set(ref _descriptionFilter, value);
    }

    public string DocumentNameFilter
    {
        get => _documentNameFilter;
        set => Set(ref _documentNameFilter, value);
    }

    public DocumentTypeDto? SelectedDocumentType
    {
        get => _selectedDocumentType;
        set => Set(ref _selectedDocumentType, value);
    }

    public DocumentDto? SelectedDocument
    {
        get => _selectedDocument;
        set => Set(ref _selectedDocument, value);
    }

    public bool HasSelectedDocumentType => SelectedDocumentType is not null;

    public bool ShowReservedOnly
    {
        get => _showReservedOnly;
        set => Set(ref _showReservedOnly, value);
    }

    public bool ShowDefectedOnly
    {
        get => _showDefectedOnly;
        set => Set(ref _showDefectedOnly, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => Set(ref _errorMessage, value);
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public int TotalCount => Containers.Count;
    public int AvailableCount => Containers.Count(item => item.IsActive == 1 && item.IsReserved != 1 && item.IsDefected != 1);
    public int ReservedCount => Containers.Count(item => item.IsActive == 1 && item.IsReserved == 1);
    public int DefectedCount => Containers.Count(item => item.IsActive == 1 && item.IsDefected == 1);

    public Task LoadAsync() => _taskExecutionService.ExecuteWithDebounceAsync(
        ContainerLoadKey, LoadContainersAsync, 0, ContainerLoadOperation, ShowError);

    private async Task LoadContainersAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            IReadOnlyList<ContainerWithDetailDto> loaded =
                await _containerService.GetContainersAsync(SelectedDocument?.Id, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (DocumentTypes.Count == 0)
            {
                IReadOnlyList<DocumentTypeDto> documentTypes =
                    await _containerService.GetDocumentTypesAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
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
        _taskExecutionService.CancelDebounce(DocumentSearchKey);
        _clearingFilters = true;
        try
        {
            ContainerNumberFilter = string.Empty;
            ArticleFilter = string.Empty;
            ElementFilter = string.Empty;
            CellFilter = string.Empty;
            DescriptionFilter = string.Empty;
            DocumentNameFilter = string.Empty;
            SelectedDocument = null;
            SelectedDocumentType = null;
            ShowReservedOnly = false;
            ShowDefectedOnly = false;
            Documents.Clear();
        }
        finally
        {
            _clearingFilters = false;
        }

        OnPropertyChanged(nameof(HasSelectedDocumentType));
        RefreshFilters();
        _ = LoadAsync();
    }

    private void RefreshFilters()
    {
        ContainersView.Refresh();
        _clearFiltersCommand?.RaiseCanExecuteChanged();
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

    private void ShowError(string message) => ErrorMessage = message;

    private Task ScheduleDocumentLoad(int delayMs)
    {
        if (SelectedDocumentType is not { } documentType)
        {
            _taskExecutionService.CancelDebounce(DocumentSearchKey);
            return Task.CompletedTask;
        }

        string nameFilter = DocumentNameFilter;
        return _taskExecutionService.ExecuteWithDebounceAsync(
            DocumentSearchKey,
            token => LoadDocumentsAsync(documentType.Id, nameFilter, token),
            delayMs, "Загрузка документов", ShowError);
    }

    private async Task LoadDocumentsAsync(
        int documentTypeId, string nameFilter, CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentDto> documents = await _containerService.GetDocumentsAsync(
            documentTypeId, nameFilter, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        Documents.Clear();
        foreach (DocumentDto document in documents)
        {
            Documents.Add(document);
        }
    }

    private void OnSelectedDocumentTypeChanged()
    {
        OnPropertyChanged(nameof(HasSelectedDocumentType));
        SelectedDocument = null;
        Documents.Clear();
        _ = ScheduleDocumentLoad(0);
    }

    protected override void OnPropertyChanged(string? propertyName)
    {
        base.OnPropertyChanged(propertyName);
        if (_clearingFilters)
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(ContainerNumberFilter):
            case nameof(ArticleFilter):
            case nameof(ElementFilter):
            case nameof(CellFilter):
            case nameof(DescriptionFilter):
            case nameof(ShowReservedOnly):
            case nameof(ShowDefectedOnly):
                RefreshFilters();
                break;
            case nameof(DocumentNameFilter):
                _ = ScheduleDocumentLoad(250);
                _clearFiltersCommand?.RaiseCanExecuteChanged();
                break;
            case nameof(SelectedDocumentType):
                OnSelectedDocumentTypeChanged();
                _clearFiltersCommand?.RaiseCanExecuteChanged();
                break;
            case nameof(SelectedDocument):
                _ = LoadAsync();
                _clearFiltersCommand?.RaiseCanExecuteChanged();
                break;
            case nameof(IsLoading):
                _refreshCommand?.RaiseCanExecuteChanged();
                break;
            case nameof(ErrorMessage):
                OnPropertyChanged(nameof(HasError));
                break;
        }
    }
}
