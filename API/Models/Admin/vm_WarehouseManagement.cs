using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Admin
{
    public class vm_WarehouseManagement
    {

    }

    public class WarehouseKpi
    {
        public int TotalWarehouses { get; set; }
        public decimal TotalCapacityMt { get; set; }
        public decimal AverageUtilization { get; set; }
        public int NearCapacityCount { get; set; }
    }
    public class WarehouseCreateRequest
    {
        public string Name { get; set; }
        public int CapacityMt { get; set; }
        public string? Region { get; set; }  // → c_state
        public string? Type { get; set; }  // → c_district
        public string? LocationAddress { get; set; }  // → c_address
                                                      // UI-only fields (no DB column, stored nowhere — keep to avoid model errors)
        public string? Code { get; set; }
        public string? ManagerName { get; set; }
        public string? TemperatureRange { get; set; }
        public string? Certification { get; set; }
    }

    public class WarehouseDetailInfo
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string? Location { get; set; }
        public string? ManagerName { get; set; }
        public string? Temperature { get; set; }
        public string? Certification { get; set; }
        public string? State { get; set; }
        public string? District { get; set; }
        public bool IsActive { get; set; }
    }
    public class WarehouseRow
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }  // "active" | "inactive"
        public string? Region { get; set; }  // from c_state
        public string? Type { get; set; }  // from c_district
        public int CapacityMt { get; set; }
        public decimal UsedMt { get; set; }
        public decimal AvailableMt { get; set; }
        public decimal UtilizationPercentage { get; set; }
    }
    public class WarehouseStockItem
    {
        public string ProductName { get; set; }
        public string? Category { get; set; }
        public string Grade { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal PercentageOfTotalStock { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    public class WarehouseUpdateRequest
    {
        public string Name { get; set; }
        public int CapacityMt { get; set; }
        public string? Region { get; set; }
        public string? Type { get; set; }
        public string? LocationAddress { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Code { get; set; }
        public string? ManagerName { get; set; }
        public string? TemperatureRange { get; set; }
        public string? Certification { get; set; }
    }

}