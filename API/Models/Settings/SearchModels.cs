using System;
using System.Collections.Generic;

namespace API.Models.Settings
{
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

    public class SearchResponseModel<T> where T : class
    {
        public List<T> Results { get; set; } = new();
        public long TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public long ProcessingTimeMs { get; set; }
        public string Query { get; set; }
    }

    public class CatalogSearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string UnitOfMeasure { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public bool IsActive { get; set; }
    }

    public class CropSearchResult
    {
        public int Id { get; set; }
        public int FarmerId { get; set; }
        public string FarmerName { get; set; }
        public int CatalogProductId { get; set; }
        public string CropName { get; set; }
        public decimal QuantityAvailable { get; set; }
        public string Unit { get; set; }
        public string Variety { get; set; }
        public decimal AskingPrice { get; set; }
        public DateTime HarvestDate { get; set; }
        public string FarmAddress { get; set; }
        public string FarmState { get; set; }
        public string FarmDistrict { get; set; }
        public string Status { get; set; }
    }

    public class OrderSearchResult
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public string VendorBusinessName { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderedAt { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }
        public string LogisticsPartner { get; set; }
        public string TrackingNumber { get; set; }
        public List<OrderItemSearchResult> Items { get; set; } = new();
    }

    public class OrderItemSearchResult
    {
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class UserSearchResult
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string BusinessName { get; set; }
        public string AssignedRegion { get; set; }
        public bool IsActive { get; set; }
    }

    public class QCSearchResult
    {
        public int Id { get; set; }
        public int ProcurementRequestId { get; set; }
        public int FoId { get; set; }
        public string FoName { get; set; }
        public int FarmerId { get; set; }
        public string FarmerName { get; set; }
        public string Grade { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public bool Passed { get; set; }
        public DateTime SubmittedAt { get; set; }
        public decimal? FoAssessedPrice { get; set; }
    }

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

    public class ReindexResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int CatalogProducts { get; set; }
        public int CropListings { get; set; }
        public int Orders { get; set; }
        public int Users { get; set; }
        public int QCRecords { get; set; }
        public int Warehouses { get; set; }
        public int TotalIndexed => CatalogProducts + CropListings + Orders + Users + QCRecords + Warehouses;
    }
    public class CatalogProductDocument
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string UnitOfMeasure { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public int CreatedBy { get; set; }
        public string DocumentType { get; set; } = "catalog_product";
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    }

    public class OrderDocument
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public string VendorBusinessName { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderedAt { get; set; }
        public List<OrderItemDocument> Items { get; set; } = new();
        public string DocumentType { get; set; } = "order";
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    }

    public class OrderItemDocument
    {
        public int CatalogProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }
    // ========== MISSING DOCUMENT CLASSES ==========

    public class CropListingDocument
    {
        public int Id { get; set; }
        public int FarmerId { get; set; }
        public string FarmerName { get; set; }
        public int CatalogProductId { get; set; }
        public string CropName { get; set; }
        public decimal QuantityAvailable { get; set; }
        public string Unit { get; set; }
        public string Variety { get; set; }
        public decimal AskingPrice { get; set; }
        public DateTime HarvestDate { get; set; }
        public string FarmAddress { get; set; }
        public string FarmState { get; set; }
        public string FarmDistrict { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public string DocumentType { get; set; } = "crop_listing";
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserDocument
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string BusinessName { get; set; }
        public string AssignedRegion { get; set; }
        public bool IsActive { get; set; }
        public string DocumentType { get; set; } = "user";
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
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