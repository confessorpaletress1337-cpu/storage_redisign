using StorageClient.Redesign.DTOs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StorageClient.Redesign.ViewModels
{
    public class ContainerHistoryViewModel
    {
        public ContainerHistoryViewModel(ContainerHistoryDto dto)
        {
            // Инициализация с защитой от null
            ContainerId = dto.ContainerId;
            ParentId = dto.ParentId;
            OperationName = dto.OperationName ?? "Неизвестная операция";
            OperationId = dto.OperationId;
            OperationTypeId = dto.OperationTypeId;
            IsRemainder = dto.IsRemainder;
            OperationDate = (DateTime?)dto.OperationDate ?? DateTime.MinValue;
            DateAdded = dto.DateAdded;
            Count = dto.Count;
            DocumentFilename = dto.DocumentFilename ?? string.Empty;
            DocumentId = dto.DocumentId;
            ElementName = dto.ElementName ?? string.Empty;
            ElementId = dto.ElementId;
            Name = dto.Name ?? -1; // Пример значения по умолчанию
            CellId = dto.CellId;
            CellName = dto.CellName ?? string.Empty;
            Description = dto.Description ?? string.Empty;
            LastName = dto.LastName ?? string.Empty;
            IsReserved = dto.IsReserved;
            IsVirtual = dto.IsVirtual;
            IsActive = dto.IsActive;
            BasketId = dto.BasketId;
            BasketName = dto.BasketName ?? string.Empty;
            PositionId = dto.PositionId;
            PositionName = dto.PositionName ?? string.Empty;
            BasketContextText = string.Join(
                Environment.NewLine,
                new[]
                {
                    string.IsNullOrWhiteSpace(BasketName) ? null : $"\u041a\u043e\u0440\u0437\u0438\u043d\u0430: {BasketName}",
                    string.IsNullOrWhiteSpace(PositionName) ? null : $"\u041f\u043e\u0437\u0438\u0446\u0438\u044f: {PositionName}"
                }.Where(value => value != null));
            CalculateVisibilities();
        }

        private void CalculateVisibilities()
        {
            var isReservation = OperationName.Equals("Резервирование", StringComparison.OrdinalIgnoreCase);
            var isIssuance = OperationName.Equals("Выдача", StringComparison.OrdinalIgnoreCase);
            var isRemainder = IsRemainder == true;

            ContainerVisibility = OperationName switch
            {
                "Покупка" or "Инвентаризация" or "Перенос" or "Отмена резервирования"
                    => Visibility.Visible,
                _ when isRemainder && (isReservation || isIssuance)
                    => Visibility.Visible,
                _ => Visibility.Collapsed
            };

            ReserveVisibility = isReservation && !isRemainder
                ? Visibility.Visible
                : Visibility.Collapsed;

            IssueVisibility = isIssuance && !isRemainder
                ? Visibility.Visible
                : Visibility.Collapsed;

            BasketContextVisibility = (isReservation || isIssuance)
                && IsRemainder != true
                && !string.IsNullOrWhiteSpace(BasketContextText)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            BasketNameVisibility = string.IsNullOrWhiteSpace(BasketName)
                ? Visibility.Collapsed
                : Visibility.Visible;
            PositionNameVisibility = string.IsNullOrWhiteSpace(PositionName)
                ? Visibility.Collapsed
                : Visibility.Visible;
            DescriptionVisibility = string.IsNullOrWhiteSpace(Description)
                ? Visibility.Collapsed
                : Visibility.Visible;
            UserVisibility = string.IsNullOrWhiteSpace(LastName)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // Свойства с явным указанием nullability
        public string DisplayOperationName => IsRemainder == true
            ? OperationName switch
            {
                "Резервирование" => "Остаток от резервирования",
                "Выдача" => "Остаток от выдачи",
                _ => "Остаток"
            }
            : OperationName;
        public bool HasDocument => DocumentId is > 0;
        public string OperationDateText => (OperationDate == DateTime.MinValue ? DateAdded : OperationDate)
            ?.ToString("dd.MM.yyyy HH:mm") ?? "—";

        public int ContainerId { get; }
        public int? ParentId { get; }
        public int Name { get; }  // Теперь не nullable
        public int ElementId { get; }
        public int Count { get; }
        public int? CellId { get; }
        public string CellName { get; }
        public int? OperationId { get; }
        public int? OperationTypeId { get; }
        public string OperationName { get; }
        public DateTime OperationDate { get; }  // Теперь не nullable
        public DateTime? DateAdded { get; }
        public bool? IsRemainder { get; }
        public bool IsReserved { get; }
        public bool IsVirtual { get; }
        public bool IsActive { get; }
        public string DocumentFilename { get; }
        public int? DocumentId { get; }
        public string ElementName { get; }
        public string Description  { get; }
        public string LastName { get; }
        public int? BasketId { get; }
        public string BasketName { get; }
        public int? PositionId { get; }
        public string PositionName { get; }
        public string BasketContextText { get; }
        public Visibility ContainerVisibility { get; private set; }
        public Visibility ReserveVisibility { get; private set; }
        public Visibility IssueVisibility { get; private set; }
        public Visibility BasketContextVisibility { get; private set; }
        public Visibility BasketNameVisibility { get; private set; }
        public Visibility PositionNameVisibility { get; private set; }
        public Visibility DescriptionVisibility { get; private set; }
        public Visibility UserVisibility { get; private set; }
    }
}
