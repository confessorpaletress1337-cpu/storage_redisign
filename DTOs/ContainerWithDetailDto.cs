using System.Text.Json.Serialization;

namespace StorageClient.Redesign.DTOs;

public sealed class ContainerWithDetailDto
{
    public int Id { get; set; }
    public int? Name { get; set; }
    public int? Article { get; set; }
    public int ElementId { get; set; }
    public int Count { get; set; }
    public DateOnly? DateExpired { get; set; }
    public int? ParentId { get; set; }
    public int? CellId { get; set; }
    public int? OperationId { get; set; }
    public string? Description { get; set; }
    public string? Price { get; set; }
    public int? PackageTypeId { get; set; }
    public string? PackageTypeName { get; set; }
    public decimal? PackageStep { get; set; }
    public sbyte IsReserved { get; set; }
    public sbyte IsDefected { get; set; }
    public sbyte IsActive { get; set; }
    public sbyte IsVirtual { get; set; }
    public DateTime? DateAdded { get; set; }
    public string? ElementName { get; set; }
    public string? AnalogName { get; set; }
    public string? ElementType { get; set; }
    public string? ElementSubtype { get; set; }
    public string? CellName { get; set; }
    public string? CellAddressName { get; set; }

    [JsonIgnore]
    public string DisplayName => Name?.ToString() ?? Id.ToString();

    [JsonIgnore]
    public string StatusText => IsActive != 1
        ? "Неактивен"
        : IsDefected == 1
            ? "Брак"
            : IsReserved == 1
                ? "Резерв"
                : "Доступен";
}
