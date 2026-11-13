using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StorageClient.Redesign.DTOs
{
    public class ContainerHistoryDto
    {
        public int ContainerId { get; set; }
        public int? ParentId { get; set; }
        public int? Name { get; set; }
        public int ElementId { get; set; }
        public int Count { get; set; }
        public int? CellId { get; set; }
        public string? CellName { get; set; }
        public int? OperationId { get; set; }
        public int? OperationTypeId { get; set; }
        public string? ElementName { get; set; }
        public string? OperationName { get; set; }
        [JsonPropertyName("isRemainder")]
        public bool? IsRemainder { get; set; }
        public DateTime? OperationDate { get; set; }
        public string? DocumentDescription { get; set; }
        public string? DocumentFilename { get; set; }
        public int? DocumentId { get; set; }
        public string? Description { get; set; }
        public string? LastName { get; set; }
        public bool IsReserved { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsActive { get; set; }
        public DateTime? DateAdded { get; set; }
        public int? BasketId { get; set; }
        public string? BasketName { get; set; }
        public int? PositionId { get; set; }
        public string? PositionName { get; set; }
    }
}
