using System;
using System.Collections.Generic;

namespace MVC.Models
{
    // Request Model
    public class SearchRequestModel
    {
        public string Query { get; set; }
        public string SearchType { get; set; } = "all";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; }
        public string SortOrder { get; set; } = "desc";
        public string Category { get; set; }
        public string Status { get; set; }
        public string Role { get; set; }
        public string State { get; set; }
        public string Grade { get; set; }
        public bool? IsActive { get; set; }
        public bool? Passed { get; set; }
        public int? VendorId { get; set; }
        public int? FarmerId { get; set; }
        public int? FoId { get; set; }
    }

    // Universal Search Result
    public class UniversalSearchResult
    {
        public string Type { get; set; }
        public int Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public object Data { get; set; }
    }

    // Re-index Result
    public class ReindexResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int CatalogProducts { get; set; }
        public int CropListings { get; set; }
        public int Orders { get; set; }
        public int Users { get; set; }
        public int QCRecords { get; set; }
        public int TotalIndexed => CatalogProducts + CropListings + Orders + Users + QCRecords;
    }

    // Vendor - Catalog Search Result
    public class CatalogSearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string UnitOfMeasure { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public string Grade { get; set; }
        public decimal Price { get; set; }
        public decimal QuantityAvailable { get; set; }
    }

    // Vendor - Order Search Result
    public class OrderSearchResult
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public string VendorBusinessName { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderedAt { get; set; }
    }

    // Farmer - Crop Search Result
    public class CropSearchResult
    {
        public int Id { get; set; }
        public int FarmerId { get; set; }
        public string FarmerName { get; set; }
        public string CropName { get; set; }
        public decimal QuantityAvailable { get; set; }
        public string Unit { get; set; }
        public string Variety { get; set; }
        public decimal AskingPrice { get; set; }
        public string Status { get; set; }
    }

    // Field Officer - QC Search Result
    public class QCSearchResult
    {
        public int Id { get; set; }
        public int ProcurementRequestId { get; set; }
        public string FoName { get; set; }
        public string FarmerName { get; set; }
        public string Grade { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public bool Passed { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string? CropType { get; set; }
        public decimal Quantity { get; set; }
        public string? Location { get; set; }
        public string? Status { get; set; }
    }
    public class WarehouseDocument
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string State { get; set; }
        public string District { get; set; }
        public int DailyCapacity { get; set; }
        public bool IsActive { get; set; }
        public string DocumentType { get; set; } = "warehouse";
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    }
}