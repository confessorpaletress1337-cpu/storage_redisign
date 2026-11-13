using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StorageClient.Redesign.Services;
using StorageClient.Redesign.ViewModels;

namespace StorageClient.Redesign.Controls
{
    public partial class ContainerHistoryGraphControl : UserControl
    {
        private static readonly DependencyPropertyKey RenderedGraphPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(RenderedGraph),
                typeof(ContainerHistoryGraphViewModel),
                typeof(ContainerHistoryGraphControl),
                new PropertyMetadata(ContainerHistoryGraphViewModel.Empty));

        public static readonly DependencyProperty RenderedGraphProperty =
            RenderedGraphPropertyKey.DependencyProperty;

        public static readonly DependencyProperty GraphProperty = DependencyProperty.Register(
            nameof(Graph),
            typeof(ContainerHistoryGraphViewModel),
            typeof(ContainerHistoryGraphControl),
            new PropertyMetadata(ContainerHistoryGraphViewModel.Empty, OnGraphChanged));

        public static readonly DependencyProperty OpenDocumentCommandProperty = DependencyProperty.Register(
            nameof(OpenDocumentCommand),
            typeof(ICommand),
            typeof(ContainerHistoryGraphControl),
            new PropertyMetadata(null));

        private readonly IContainerHistoryGraphLayoutService _layoutService =
            new ContainerHistoryGraphLayoutService();

        private HistoryGraphColumnLayout _columnLayout = HistoryGraphColumnLayout.Default;
        private ScrollViewer? _gridScrollViewer;
        private bool _layoutRefreshPending;

        public ContainerHistoryGraphControl()
        {
            InitializeComponent();
            HistoryGrid.LayoutUpdated += HistoryGrid_LayoutUpdated;
        }

        public ContainerHistoryGraphViewModel Graph
        {
            get => (ContainerHistoryGraphViewModel)GetValue(GraphProperty);
            set => SetValue(GraphProperty, value);
        }

        public ContainerHistoryGraphViewModel RenderedGraph =>
            (ContainerHistoryGraphViewModel)GetValue(RenderedGraphProperty);

        public ICommand? OpenDocumentCommand
        {
            get => (ICommand?)GetValue(OpenDocumentCommandProperty);
            set => SetValue(OpenDocumentCommandProperty, value);
        }

        private static void OnGraphChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var control = (ContainerHistoryGraphControl)dependencyObject;
            control.RefreshRenderedGraph();
            control.QueueColumnLayoutRefresh();
        }

        private void HistoryGrid_Loaded(object sender, RoutedEventArgs e)
        {
            AttachGridScrollViewer();
            QueueColumnLayoutRefresh();
        }

        private void HistoryGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachGridScrollViewer();
        }

        private void HistoryGrid_LayoutUpdated(object? sender, EventArgs e)
        {
            if (!HistoryGrid.IsLoaded || HistoryGrid.Columns.Count < 4)
                return;

            var operationWidth = HistoryGrid.Columns[0].ActualWidth;
            var containerWidth = HistoryGrid.Columns[1].ActualWidth;
            var reserveWidth = HistoryGrid.Columns[2].ActualWidth;
            var issueWidth = HistoryGrid.Columns[3].ActualWidth;

            if (operationWidth <= 0 || containerWidth <= 0 || reserveWidth <= 0 || issueWidth <= 0)
                return;

            if (AreClose(operationWidth, _columnLayout.OperationWidth) &&
                AreClose(containerWidth, _columnLayout.ContainerWidth) &&
                AreClose(reserveWidth, _columnLayout.ReserveWidth) &&
                AreClose(issueWidth, _columnLayout.IssueWidth))
            {
                return;
            }

            QueueColumnLayoutRefresh();
        }

        private void QueueColumnLayoutRefresh()
        {
            if (_layoutRefreshPending || !IsInitialized)
                return;

            _layoutRefreshPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _layoutRefreshPending = false;
                CaptureColumnLayoutAndRefresh();
            }));
        }

        private void CaptureColumnLayoutAndRefresh()
        {
            if (!HistoryGrid.IsLoaded || HistoryGrid.Columns.Count < 4)
                return;

            var operationWidth = HistoryGrid.Columns[0].ActualWidth;
            var containerWidth = HistoryGrid.Columns[1].ActualWidth;
            var reserveWidth = HistoryGrid.Columns[2].ActualWidth;
            var issueWidth = HistoryGrid.Columns[3].ActualWidth;

            if (operationWidth <= 0 || containerWidth <= 0 || reserveWidth <= 0 || issueWidth <= 0)
                return;

            _columnLayout = new HistoryGraphColumnLayout(
                operationWidth,
                containerWidth,
                reserveWidth,
                issueWidth);

            RefreshRenderedGraph();
        }

        private void AttachGridScrollViewer()
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(HistoryGrid);
            if (ReferenceEquals(scrollViewer, _gridScrollViewer))
            {
                UpdateGraphScrollTransform();
                return;
            }

            DetachGridScrollViewer();
            _gridScrollViewer = scrollViewer;

            if (_gridScrollViewer != null)
                _gridScrollViewer.ScrollChanged += GridScrollViewer_ScrollChanged;

            UpdateGraphScrollTransform();
        }

        private void DetachGridScrollViewer()
        {
            if (_gridScrollViewer != null)
                _gridScrollViewer.ScrollChanged -= GridScrollViewer_ScrollChanged;

            _gridScrollViewer = null;
        }

        private void GridScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateGraphScrollTransform();
        }

        private void UpdateGraphScrollTransform()
        {
            GraphScrollTransform.X = -(_gridScrollViewer?.HorizontalOffset ?? 0);
            GraphScrollTransform.Y = -(_gridScrollViewer?.VerticalOffset ?? 0);
        }

        private void RefreshRenderedGraph()
        {
            var sourceItems = Graph?.SourceItems ?? Array.Empty<ContainerHistoryViewModel>();
            SetValue(RenderedGraphPropertyKey, _layoutService.Build(sourceItems, _columnLayout));
        }

        private static bool AreClose(double first, double second) => Math.Abs(first - second) < 0.5;

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T result)
                    return result;

                var nestedResult = FindVisualChild<T>(child);
                if (nestedResult != null)
                    return nestedResult;
            }

            return null;
        }
    }
}
