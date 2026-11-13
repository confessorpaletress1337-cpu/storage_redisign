using System.Windows;
using System.Windows.Media;
using StorageClient.Redesign.ViewModels;

namespace StorageClient.Redesign.Services
{
    public interface IContainerHistoryGraphLayoutService
    {
        ContainerHistoryGraphViewModel Build(
            IReadOnlyCollection<ContainerHistoryViewModel> history,
            HistoryGraphColumnLayout? columnLayout = null);
    }

    public sealed class ContainerHistoryGraphLayoutService : IContainerHistoryGraphLayoutService
    {
        private const double RowHeight = 92;
        private const double NodeRadius = 17;
        private const double FirstRowCenterY = 46;

        public ContainerHistoryGraphViewModel Build(
            IReadOnlyCollection<ContainerHistoryViewModel> history,
            HistoryGraphColumnLayout? columnLayout = null)
        {
            var layout = columnLayout ?? HistoryGraphColumnLayout.Default;
            var sourceItems = history.ToList();

            if (history.Count == 0)
            {
                return new ContainerHistoryGraphViewModel(
                    sourceItems,
                    Array.Empty<ContainerHistoryOperationRowViewModel>(),
                    Array.Empty<ContainerHistoryGraphNodeViewModel>(),
                    Array.Empty<ContainerHistoryGraphEdgeViewModel>(),
                    layout.CanvasWidth,
                    140);
            }

            var orderedItems = history
                .Select((item, index) => new IndexedHistoryItem(item, index))
                .OrderBy(item => GetEffectiveDate(item.Item))
                .ThenBy(item => item.Item.OperationId ?? int.MaxValue)
                .ThenBy(item => item.Item.ContainerId > 0 ? item.Item.ContainerId : item.Index)
                .ToList();

            var itemsById = orderedItems
                .Where(item => item.Item.ContainerId > 0)
                .GroupBy(item => item.Item.ContainerId)
                .ToDictionary(group => group.Key, group => group.First().Item);

            var operationGroups = orderedItems
                .GroupBy(item => item.Item.OperationId.HasValue
                    ? $"operation:{item.Item.OperationId.Value}"
                    : $"container:{GetStableNodeId(item)}")
                .Select(group => new
                {
                    Items = group.ToList(),
                    Date = group.Min(item => GetEffectiveDate(item.Item)),
                    OperationId = group.Select(item => item.Item.OperationId).FirstOrDefault(id => id.HasValue),
                    FirstId = group.Min(GetStableNodeId)
                })
                .OrderBy(group => group.Date)
                .ThenBy(group => group.OperationId ?? int.MaxValue)
                .ThenBy(group => group.FirstId)
                .ToList();

            var rows = new List<ContainerHistoryOperationRowViewModel>(operationGroups.Count);
            var nodes = new List<ContainerHistoryGraphNodeViewModel>(orderedItems.Count);
            var nodeById = new Dictionary<int, ContainerHistoryGraphNodeViewModel>();

            for (var rowIndex = 0; rowIndex < operationGroups.Count; rowIndex++)
            {
                var group = operationGroups[rowIndex];
                var rowTop = rowIndex * RowHeight;
                var rowCenterY = rowTop + FirstRowCenterY;
                var representative = group.Items
                    .Select(item => item.Item)
                    .FirstOrDefault(item => item.IsRemainder != true)
                    ?? group.Items[0].Item;
                var operationItems = group.Items
                    .Select(item => item.Item)
                    .ToList();

                rows.Add(CreateRow(representative, rowIndex, rowTop, layout));

                var positionedItems = group.Items
                    .Select(item => new
                    {
                        IndexedItem = item,
                        Kind = ResolveKind(item.Item, itemsById, operationItems)
                    })
                    .GroupBy(item => GetLaneCenter(item.Kind, layout));

                foreach (var laneGroup in positionedItems)
                {
                    var laneItems = laneGroup.ToList();
                    for (var laneIndex = 0; laneIndex < laneItems.Count; laneIndex++)
                    {
                        var positioned = laneItems[laneIndex];
                        var verticalOffset = (laneIndex - (laneItems.Count - 1) / 2.0) * 32;
                        var centerY = rowCenterY + verticalOffset;
                        var node = CreateNode(positioned.IndexedItem.Item, positioned.Kind, laneGroup.Key, centerY);
                        nodes.Add(node);

                        var stableId = GetStableNodeId(positioned.IndexedItem);
                        nodeById.TryAdd(stableId, node);
                    }
                }
            }

            var edges = new List<ContainerHistoryGraphEdgeViewModel>();
            foreach (var node in nodes)
            {
                // A transfer only changes the cell and should not create a visible
                // break in the container branch. Skip the proxy itself and connect
                // its children to the last visible container before the transfer.
                if (node.Kind == HistoryNodeKind.TransferProxy ||
                    !TryGetVisibleParent(node.HistoryItem.ParentId, nodeById, out var parent))
                {
                    continue;
                }

                edges.Add(new ContainerHistoryGraphEdgeViewModel
                {
                    Geometry = CreateEdgeGeometry(parent, node),
                    IsSplitEdge = GetOperationTypeId(node.HistoryItem) == 7
                });
            }

            var canvasHeight = Math.Max(140, operationGroups.Count * RowHeight);
            return new ContainerHistoryGraphViewModel(
                sourceItems,
                rows,
                nodes,
                edges,
                layout.CanvasWidth,
                canvasHeight);
        }

        private static ContainerHistoryOperationRowViewModel CreateRow(
            ContainerHistoryViewModel item,
            int rowIndex,
            double top,
            HistoryGraphColumnLayout layout)
        {
            var operationName = GetOperationTypeId(item) == 9 && !string.IsNullOrWhiteSpace(item.CellName)
                ? $"Перенос в {item.CellName}"
                : item.OperationName;

            var date = GetEffectiveDate(item);
            return new ContainerHistoryOperationRowViewModel
            {
                HistoryItem = item,
                Top = top,
                Height = RowHeight,
                Width = layout.CanvasWidth,
                OperationWidth = layout.OperationWidth,
                ContainerWidth = layout.ContainerWidth,
                ReserveWidth = layout.ReserveWidth,
                IssueWidth = layout.IssueWidth,
                OperationName = operationName,
                OperationDateText = date == DateTime.MinValue
                    ? "Дата не указана"
                    : date.ToString("dd.MM.yyyy HH:mm"),
                DetailText = string.IsNullOrWhiteSpace(item.LastName)
                    ? string.Empty
                    : $"Пользователь: {item.LastName}",
                BasketDisplayName = !string.IsNullOrWhiteSpace(item.BasketName)
                    ? item.BasketName
                    : item.BasketId.HasValue
                        ? $"Корзина №{item.BasketId.Value}"
                        : string.Empty,
                BasketPositionText = string.IsNullOrWhiteSpace(item.PositionName)
                    ? string.Empty
                    : $" · Позиция: {item.PositionName}",
                IsAlternate = rowIndex % 2 != 0
            };
        }

        private static double GetLaneCenter(HistoryNodeKind kind, HistoryGraphColumnLayout layout)
        {
            return kind switch
            {
                HistoryNodeKind.NormalContainer => layout.ContainerCenter,
                HistoryNodeKind.TransferProxy => layout.ContainerCenter,
                HistoryNodeKind.Reserve => layout.ReserveCenter,
                HistoryNodeKind.FullyReserved => layout.ReserveCenter,
                HistoryNodeKind.Issue => layout.IssueCenter,
                _ => layout.ContainerCenter
            };
        }

        private static ContainerHistoryGraphNodeViewModel CreateNode(
            ContainerHistoryViewModel item,
            HistoryNodeKind kind,
            double centerX,
            double centerY)
        {
            var tooltipLines = new List<string>
            {
                item.Name >= 0 ? $"Контейнер №{item.Name}" : "Контейнер без номера",
                $"Количество: {item.Count} шт.",
                $"Операция: {item.OperationName}"
            };

            var date = GetEffectiveDate(item);
            if (date != DateTime.MinValue)
                tooltipLines.Add($"Дата: {date:dd.MM.yyyy HH:mm}");
            if (item.IsRemainder == true)
                tooltipLines.Add("Остаток после операции");
            if (!string.IsNullOrWhiteSpace(item.Description))
                tooltipLines.Add(item.Description);
            if (!string.IsNullOrWhiteSpace(item.BasketContextText))
                tooltipLines.Add(item.BasketContextText);
            if (!string.IsNullOrWhiteSpace(item.LastName))
                tooltipLines.Add($"Пользователь: {item.LastName}");
            if (!string.IsNullOrWhiteSpace(item.DocumentFilename))
                tooltipLines.Add($"Документ: {item.DocumentFilename}");

            return new ContainerHistoryGraphNodeViewModel
            {
                HistoryItem = item,
                Kind = kind,
                CenterX = centerX,
                CenterY = centerY,
                Left = centerX - NodeRadius,
                Top = centerY - NodeRadius,
                CountText = item.Count.ToString(),
                ToolTipText = string.Join(Environment.NewLine, tooltipLines)
            };
        }

        private static HistoryNodeKind ResolveKind(
            ContainerHistoryViewModel item,
            IReadOnlyDictionary<int, ContainerHistoryViewModel> itemsById,
            IEnumerable<ContainerHistoryViewModel> operationItems)
        {
            return GetOperationTypeId(item) switch
            {
                5 => ResolveIssueKind(item, itemsById, operationItems),
                7 when item.IsRemainder != true => HistoryNodeKind.NormalContainer,
                9 => HistoryNodeKind.TransferProxy,
                3 when item.IsRemainder != true && IsFullReservation(item, itemsById, operationItems)
                    => HistoryNodeKind.FullyReserved,
                3 when item.IsRemainder != true => HistoryNodeKind.Reserve,
                _ when item.IsRemainder == true => HistoryNodeKind.NormalContainer,
                _ when item.IsReserved => HistoryNodeKind.Reserve,
                _ => HistoryNodeKind.NormalContainer
            };
        }

        private static HistoryNodeKind ResolveIssueKind(
            ContainerHistoryViewModel item,
            IReadOnlyDictionary<int, ContainerHistoryViewModel> itemsById,
            IEnumerable<ContainerHistoryViewModel> operationItems)
        {
            if (item.IsRemainder == true)
            {
                return item.IsReserved
                    ? HistoryNodeKind.Reserve
                    : HistoryNodeKind.NormalContainer;
            }

            if (item.IsRemainder == false)
                return HistoryNodeKind.Issue;

            // Legacy issue rows do not always have is_remainder filled in. The
            // operation creates its issued result first and the retained part
            // second, so a valid two-child split can still be reconstructed.
            var siblings = operationItems
                .Where(candidate => candidate.ParentId == item.ParentId)
                .OrderBy(candidate => candidate.ContainerId)
                .ToList();

            if (siblings.Count != 2 ||
                !item.ParentId.HasValue ||
                !itemsById.TryGetValue(item.ParentId.Value, out var parent) ||
                siblings.Sum(candidate => (long)candidate.Count) != parent.Count)
            {
                return HistoryNodeKind.Issue;
            }

            var unreservedCandidates = siblings
                .Where(candidate => !candidate.IsVirtual && !candidate.IsReserved)
                .ToList();
            var retainedPart = unreservedCandidates.Count == 1
                ? unreservedCandidates[0]
                : siblings[^1];

            if (!ReferenceEquals(retainedPart, item))
                return HistoryNodeKind.Issue;

            return item.IsReserved
                ? HistoryNodeKind.Reserve
                : HistoryNodeKind.NormalContainer;
        }

        private static bool TryGetVisibleParent(
            int? parentId,
            IReadOnlyDictionary<int, ContainerHistoryGraphNodeViewModel> nodesById,
            out ContainerHistoryGraphNodeViewModel parent)
        {
            var visitedIds = new HashSet<int>();
            var currentId = parentId;

            while (currentId.HasValue &&
                   visitedIds.Add(currentId.Value) &&
                   nodesById.TryGetValue(currentId.Value, out var candidate))
            {
                if (candidate.Kind != HistoryNodeKind.TransferProxy)
                {
                    parent = candidate;
                    return true;
                }

                currentId = candidate.HistoryItem.ParentId;
            }

            parent = null!;
            return false;
        }

        private static bool IsFullReservation(
            ContainerHistoryViewModel item,
            IReadOnlyDictionary<int, ContainerHistoryViewModel> itemsById,
            IEnumerable<ContainerHistoryViewModel> operationItems)
        {
            if (operationItems.Any(candidate => candidate.IsRemainder == true && candidate.Count > 0))
                return false;

            return item.ParentId.HasValue &&
                   itemsById.TryGetValue(item.ParentId.Value, out var parent) &&
                   parent.Count == item.Count;
        }

        private static int? GetOperationTypeId(ContainerHistoryViewModel item)
        {
            if (item.OperationTypeId.HasValue)
                return item.OperationTypeId;

            return item.OperationName.Trim().ToLowerInvariant() switch
            {
                "резервирование" => 3,
                "покупка" => 4,
                "выдача" => 5,
                "отмена резервирования" => 6,
                "разделение" => 7,
                "инвентаризация" => 8,
                "перенос" => 9,
                _ => null
            };
        }

        private static Geometry CreateEdgeGeometry(
            ContainerHistoryGraphNodeViewModel parent,
            ContainerHistoryGraphNodeViewModel child)
        {
            if ((parent.Kind == HistoryNodeKind.Reserve ||
                 parent.Kind == HistoryNodeKind.FullyReserved) &&
                (child.Kind == HistoryNodeKind.Issue ||
                 GetOperationTypeId(child.HistoryItem) == 6) &&
                child.CenterY > parent.CenterY)
            {
                return CreateOutgoingReserveGeometry(parent, child);
            }

            var figure = new PathFigure
            {
                StartPoint = new Point(parent.CenterX, parent.CenterY),
                IsClosed = false,
                IsFilled = false
            };

            if (Math.Abs(parent.CenterX - child.CenterX) < 0.1)
            {
                figure.Segments.Add(new LineSegment(
                    new Point(child.CenterX, child.CenterY),
                    true));
            }
            else
            {
                var operationBoundaryY = Math.Floor(child.CenterY / RowHeight) * RowHeight;
                var horizontalDirection = Math.Sign(child.CenterX - parent.CenterX);
                var verticalDirection = Math.Sign(child.CenterY - parent.CenterY);
                var cornerRadius = Math.Min(
                    8,
                    Math.Min(
                        Math.Abs(child.CenterX - parent.CenterX) / 2,
                        Math.Min(
                            Math.Abs(operationBoundaryY - parent.CenterY),
                            Math.Abs(child.CenterY - operationBoundaryY))));

                figure.Segments.Add(new LineSegment(
                    new Point(parent.CenterX, operationBoundaryY - verticalDirection * cornerRadius),
                    true));
                figure.Segments.Add(new QuadraticBezierSegment(
                    new Point(parent.CenterX, operationBoundaryY),
                    new Point(parent.CenterX + horizontalDirection * cornerRadius, operationBoundaryY),
                    true));
                figure.Segments.Add(new LineSegment(
                    new Point(child.CenterX - horizontalDirection * cornerRadius, operationBoundaryY),
                    true));
                figure.Segments.Add(new QuadraticBezierSegment(
                    new Point(child.CenterX, operationBoundaryY),
                    new Point(child.CenterX, operationBoundaryY + verticalDirection * cornerRadius),
                    true));
                figure.Segments.Add(new LineSegment(new Point(child.CenterX, child.CenterY), true));
            }

            var geometry = new PathGeometry(new[] { figure });
            geometry.Freeze();
            return geometry;
        }

        private static Geometry CreateOutgoingReserveGeometry(
            ContainerHistoryGraphNodeViewModel parent,
            ContainerHistoryGraphNodeViewModel child)
        {
            const double trackOffset = NodeRadius + 16;
            const double preferredCornerRadius = 8;

            var horizontalDirection = Math.Sign(child.CenterX - parent.CenterX);
            if (horizontalDirection == 0)
                horizontalDirection = 1;

            var trackX = parent.CenterX + horizontalDirection * trackOffset;
            var operationBoundaryY = Math.Floor(child.CenterY / RowHeight) * RowHeight;
            var verticalDistance = operationBoundaryY - parent.CenterY;
            var horizontalDistance = Math.Abs(child.CenterX - trackX);
            var cornerRadius = Math.Min(
                preferredCornerRadius,
                Math.Min(verticalDistance / 3, horizontalDistance / 3));

            if (cornerRadius <= 0)
            {
                var fallbackFigure = new PathFigure
                {
                    StartPoint = new Point(parent.CenterX, parent.CenterY),
                    IsClosed = false,
                    IsFilled = false
                };
                fallbackFigure.Segments.Add(new LineSegment(
                    new Point(child.CenterX, child.CenterY),
                    true));

                var fallbackGeometry = new PathGeometry(new[] { fallbackFigure });
                fallbackGeometry.Freeze();
                return fallbackGeometry;
            }

            var figure = new PathFigure
            {
                StartPoint = new Point(parent.CenterX, parent.CenterY),
                IsClosed = false,
                IsFilled = false
            };

            figure.Segments.Add(new LineSegment(
                new Point(trackX - horizontalDirection * cornerRadius, parent.CenterY),
                true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(trackX, parent.CenterY),
                new Point(trackX, parent.CenterY + cornerRadius),
                true));
            figure.Segments.Add(new LineSegment(
                new Point(trackX, operationBoundaryY - cornerRadius),
                true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(trackX, operationBoundaryY),
                new Point(trackX + horizontalDirection * cornerRadius, operationBoundaryY),
                true));
            figure.Segments.Add(new LineSegment(
                new Point(child.CenterX - horizontalDirection * cornerRadius, operationBoundaryY),
                true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(child.CenterX, operationBoundaryY),
                new Point(child.CenterX, operationBoundaryY + cornerRadius),
                true));
            figure.Segments.Add(new LineSegment(
                new Point(child.CenterX, child.CenterY),
                true));

            var geometry = new PathGeometry(new[] { figure });
            geometry.Freeze();
            return geometry;
        }

        private static DateTime GetEffectiveDate(ContainerHistoryViewModel item)
        {
            if (item.OperationDate != DateTime.MinValue)
                return item.OperationDate;

            return item.DateAdded ?? DateTime.MinValue;
        }

        private static int GetStableNodeId(IndexedHistoryItem item) =>
            item.Item.ContainerId > 0 ? item.Item.ContainerId : -(item.Index + 1);

        private sealed record IndexedHistoryItem(ContainerHistoryViewModel Item, int Index);
    }
}
