using System;
using System.Collections.Generic;

namespace API.Models.FarmerApp
{
    // --- 1. DASHBOARD MODELS ---
    public class vm_FarmerDashboard
    {
        public vm_FarmerKpi Kpis { get; set; } = new();
        public List<vm_MarketPriceWidget> MarketPrices { get; set; } = new();
        public List<vm_RecentActivity> RecentActivities { get; set; } = new();
        public List<vm_WeatherForecast> WeatherForecast { get; set; } = new();
    }

    public class vm_FarmerKpi
    {
        public int TotalCropsListed { get; set; }
        public int PendingQcRequests { get; set; }
        public int ConfirmedQcSlots { get; set; }
        public decimal TotalPaymentsReceived { get; set; }
    }

    public class vm_MarketPriceWidget
    {
        public string CropName { get; set; } = string.Empty;
        public decimal CurrentMarketPrice { get; set; }
        public decimal AiPredictedPrice { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class vm_RecentActivity
    {
        public string ActionType { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class vm_WeatherForecast
    {
        public string Day { get; set; } = string.Empty;
        public decimal Temp { get; set; }
        public string Condition { get; set; } = string.Empty;
    }

    // --- 2. CROP LISTING MODELS ---
    public class vm_CropListingRequest
    {
        public int? ListingId { get; set; } 
        public int FarmerId { get; set; }
        public int CatalogProductId { get; set; }
        public string Variety { get; set; } = string.Empty;
        public decimal AvailableQuantity { get; set; }
        public string Unit { get; set; } = string.Empty; 
        public decimal AskingPrice { get; set; }
        public DateTime HarvestDate { get; set; }
        public string FarmAddress { get; set; } = string.Empty;
        public bool IsDraft { get; set; } 
    }

    public class vm_CropListingResponse : vm_CropListingRequest
    {
        public string CropName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; 
        public DateTime CreatedAt { get; set; }
    }

    // --- 3. QC SLOT MODELS ---
    public class vm_AvailableQcSlot
    {
        public int SlotId { get; set; }
        public DateTime SlotDate { get; set; }
        public TimeSpan TimeStart { get; set; }
        public TimeSpan TimeEnd { get; set; }
        public decimal AvailableCapacityMt { get; set; }
    }

    public class vm_BookQcSlotRequest
    {
        public int FarmerId { get; set; }
        public int CropListingId { get; set; }
        public int WarehouseId { get; set; }
        public int SlotId { get; set; }
        public DateTime SlotDate { get; set; }
        public TimeSpan TimeStart { get; set; }
        public TimeSpan TimeEnd { get; set; }
    }

    // --- 4. PAYMENT MODELS ---
    public class vm_FarmerPayment
    {
        public int PaymentId { get; set; }
        public string CropName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int PaymentNumber { get; set; }
        public string TriggerEvent { get; set; } = string.Empty;
        public string PaymentMode { get; set; } = string.Empty;
        public string UtrReference { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }

    // --- 5. DROPDOWN & CHART MODELS ---
    public class vm_DropdownItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class vm_IncomeChartPoint
    {
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    // --- 6. QC DASHBOARD MODELS ---
    public class vm_QcDashboard
    {
        public int UpcomingAppts { get; set; }
        public int AwaitingResults { get; set; }
        public int TotalQcPassed { get; set; }
        public decimal PremiumGradeRate { get; set; } // % of crops getting Grade A/Premium
        public List<vm_QcAppointment> Appointments { get; set; } = new();
    }

    public class vm_QcAppointment
    {
        public DateTime SlotDate { get; set; }
        public TimeSpan TimeStart { get; set; }
        public string CropName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    // --- 7. INQUIRY MODELS ---
    public class vm_Inquiry
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class vm_SubmitInquiryRequest
    {
        public int FarmerId { get; set; }
        public int? PaymentId { get; set; } // Optional, if related to a specific payment
        public string Department { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    // --- 8. PROFILE & BANK MODELS ---
    public class vm_FarmerProfileResponse
    {
        // t_farmer_profiles fields
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;       // from t_users
        public string Phone { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;         // c_profile_image_url (t_users)

        // t_bank_accounts fields
        public string BankName { get; set; } = string.Empty;          // c_bank_name
        public string BranchName { get; set; } = string.Empty;        // c_branch_name
        public string AccountHolderName { get; set; } = string.Empty; // c_account_holder_name
        public string AccountNumber { get; set; } = string.Empty;     // c_account_number
        public string IfscCode { get; set; } = string.Empty;          // c_ifsc_code
        public string UpiId { get; set; } = string.Empty;             // c_upi_id
        public string AccountType { get; set; } = string.Empty;       // c_account_type (savings/current)
    }

    public class vm_UpdateProfileRequest : vm_FarmerProfileResponse
    {
        public int FarmerId { get; set; }
    }
}