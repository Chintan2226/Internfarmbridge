using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Admin
{
    public class vm_FieldOfficer
    {
        // Profile Data
        public int Id { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string AssignedRegion { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; }

        // User Account Data
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // Performance Metric
        public int TotalInspections { get; set; }
    }

    public class FieldOfficerRow
{
    public int Id { get; set; } // Changed from string to int to fix the first error
    public string FoCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AssignedRegion { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public int? WarehouseId { get; set; }
    public int InspectionCount { get; set; }
    public int PassedCount { get; set; }
    public int LotsManaged { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class FieldOfficerPerformance
    {
        public string FieldOfficerId { get; set; } = string.Empty;
        public int TotalInspectionsCompleted { get; set; }
        public int InspectionsPending { get; set; }
        public decimal AverageRating { get; set; }
        public decimal AcceptanceRate { get; set; }
    }

    public class FieldOfficerAnalyticsKpi
    {
        public int TotalFieldOfficers { get; set; }
        public int AssessmentsDone { get; set; }
        public int PendingReviews { get; set; }
        public decimal AccuracyRate { get; set; } // Returns a percentage like 96.0
    }

    public class FoStatusPatchRequest
    {
        public bool IsActive { get; set; }
    }

    public class FoInspectionRow
    {
        public long   InspectionId          { get; set; }
        public long   ProcurementRequestId  { get; set; }
        public string FarmerName            { get; set; } = "";
        public string FarmerPhone           { get; set; } = "";
        public string CropName              { get; set; } = "";
        public string CropCategory          { get; set; } = "";
        public string Variety               { get; set; } = "";
        public string Grade                 { get; set; } = "";
        public decimal MoisturePct          { get; set; }
        public decimal ForeignMatterPct     { get; set; }
        public decimal WeightCheckedKg      { get; set; }
        public decimal AcceptedQuantity     { get; set; }
        public decimal RejectedQuantity     { get; set; }
        public string? PestDiseaseObserved  { get; set; }
        public string? DefectsNoted         { get; set; }
        public string? Remarks              { get; set; }
        public bool   Passed               { get; set; }
        public string ProcurementStatus    { get; set; } = "";
        public DateTime? SubmittedAt       { get; set; }
        public List<string> Photos         { get; set; } = new();
    }
 
    // ─── Warehouse Slot Row ────────────────────────────────────────────────────
 
    public class FoWarehouseSlotRow
    {
        public long   SlotId             { get; set; }
        public string FarmerName         { get; set; } = "";
        public string FarmerPhone        { get; set; } = "";
        public string CropName           { get; set; } = "";
        public string WarehouseName      { get; set; } = "";
        public DateOnly SlotDate         { get; set; }
        public TimeOnly SlotTimeStart    { get; set; }
        public TimeOnly SlotTimeEnd      { get; set; }
        public string CheckInStatus      { get; set; } = "";
        public string BookingStatus      { get; set; } = "";
        public DateTime? BookedAt        { get; set; }
        public DateTime? ArrivedAt       { get; set; }
        public string? Notes             { get; set; }
 
        // Linked lot info (if QC completed and lot created)
        public long?   LotId             { get; set; }
        public string? LotGrade          { get; set; }
        public decimal? QuantityAccepted { get; set; }
        public string? LotStatus         { get; set; }
    }
 
    // ─── FO Full Detail (profile + stats) ─────────────────────────────────────
 
    public class FoDetailDto
    {
        public long   Id               { get; set; }
        public string FoCode           { get; set; } = "";
        public string FullName         { get; set; } = "";
        public string Email            { get; set; } = "";
        public string Phone            { get; set; } = "";
        public string AssignedRegion   { get; set; } = "";
        public string WarehouseName    { get; set; } = "";
        public long?  WarehouseId      { get; set; }
        public bool   IsActive         { get; set; }
        public DateTime? CreatedAt     { get; set; }
 
        // Summary stats
        public int TotalInspections    { get; set; }
        public int PassedInspections   { get; set; }
        public int LotsManaged         { get; set; }
    }
}
