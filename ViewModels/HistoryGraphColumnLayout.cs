namespace StorageClient.Redesign.ViewModels
{
    public sealed record HistoryGraphColumnLayout(
        double OperationWidth,
        double ContainerWidth,
        double ReserveWidth,
        double IssueWidth)
    {
        public static HistoryGraphColumnLayout Default { get; } = new(
            OperationWidth: 260,
            ContainerWidth: 87,
            ReserveWidth: 87,
            IssueWidth: 86);

        public double CanvasWidth =>
            OperationWidth + ContainerWidth + ReserveWidth + IssueWidth;

        public double ContainerCenter => OperationWidth + ContainerWidth / 2;

        public double ReserveCenter => OperationWidth + ContainerWidth + ReserveWidth / 2;

        public double IssueCenter =>
            OperationWidth + ContainerWidth + ReserveWidth + IssueWidth / 2;
    }
}
