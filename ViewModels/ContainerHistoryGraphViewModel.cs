using System.Windows.Media;

namespace StorageClient.Redesign.ViewModels
{
    public enum HistoryNodeKind
    {
        NormalContainer,
        Reserve,
        FullyReserved,
        Issue,
        TransferProxy
    }

    public sealed class ContainerHistoryGraphViewModel
    {
        public static ContainerHistoryGraphViewModel Empty { get; } = new(
            Array.Empty<ContainerHistoryViewModel>(),
            Array.Empty<ContainerHistoryOperationRowViewModel>(),
            Array.Empty<ContainerHistoryGraphNodeViewModel>(),
            Array.Empty<ContainerHistoryGraphEdgeViewModel>(),
            HistoryGraphColumnLayout.Default.CanvasWidth,
            140);

        public ContainerHistoryGraphViewModel(
            IReadOnlyList<ContainerHistoryViewModel> sourceItems,
            IReadOnlyList<ContainerHistoryOperationRowViewModel> rows,
            IReadOnlyList<ContainerHistoryGraphNodeViewModel> nodes,
            IReadOnlyList<ContainerHistoryGraphEdgeViewModel> edges,
            double canvasWidth,
            double canvasHeight)
        {
            SourceItems = sourceItems;
            Rows = rows;
            Nodes = nodes;
            Edges = edges;
            CanvasWidth = canvasWidth;
            CanvasHeight = canvasHeight;
        }

        public IReadOnlyList<ContainerHistoryViewModel> SourceItems { get; }
        public IReadOnlyList<ContainerHistoryOperationRowViewModel> Rows { get; }
        public IReadOnlyList<ContainerHistoryGraphNodeViewModel> Nodes { get; }
        public IReadOnlyList<ContainerHistoryGraphEdgeViewModel> Edges { get; }
        public double CanvasWidth { get; }
        public double CanvasHeight { get; }
        public bool HasItems => Nodes.Count > 0;
    }

    public sealed class ContainerHistoryOperationRowViewModel
    {
        public required ContainerHistoryViewModel HistoryItem { get; init; }
        public required double Top { get; init; }
        public required double Height { get; init; }
        public required double Width { get; init; }
        public required double OperationWidth { get; init; }
        public required double ContainerWidth { get; init; }
        public required double ReserveWidth { get; init; }
        public required double IssueWidth { get; init; }
        public required string OperationName { get; init; }
        public required string OperationDateText { get; init; }
        public required string DetailText { get; init; }
        public required string BasketDisplayName { get; init; }
        public required string BasketPositionText { get; init; }
        public required bool IsAlternate { get; init; }
        public bool HasDetailText => !string.IsNullOrWhiteSpace(DetailText);
        public bool HasBasket => HistoryItem.BasketId > 0;
    }

    public sealed class ContainerHistoryGraphNodeViewModel
    {
        public required ContainerHistoryViewModel HistoryItem { get; init; }
        public required HistoryNodeKind Kind { get; init; }
        public required double CenterX { get; init; }
        public required double CenterY { get; init; }
        public required double Left { get; init; }
        public required double Top { get; init; }
        public required string CountText { get; init; }
        public required string ToolTipText { get; init; }
        public bool IsVisible => Kind != HistoryNodeKind.TransferProxy;
        public bool HasDocument => HistoryItem.DocumentId.HasValue;
    }

    public sealed class ContainerHistoryGraphEdgeViewModel
    {
        public required Geometry Geometry { get; init; }
        public required bool IsSplitEdge { get; init; }
    }
}
