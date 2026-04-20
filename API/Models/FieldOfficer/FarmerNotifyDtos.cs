namespace API.Models.FieldOfficer
{
    public class FarmerNotifyInfo
    {
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string CropName { get; set; } = "";
    }

    public class RescheduleEmailData
    {
        public int ProcurementRequestId { get; set; }
        public string FarmerEmail { get; set; } = "";
        public string FarmerName { get; set; } = "";
        public string CropName { get; set; } = "";
        public string WarehouseName { get; set; } = "—";
        public string OldSlotDateFormatted { get; set; } = "";
        public string OldTimeRange { get; set; } = "";
        public string NewSlotDateFormatted { get; set; } = "";
        public string NewTimeRange { get; set; } = "";
    }

    public class AcceptEmailData
    {
        public int ProcurementRequestId { get; set; }
        public string FarmerEmail { get; set; } = "";
        public string FarmerName { get; set; } = "";
        public string CropName { get; set; } = "";
        public string WarehouseName { get; set; } = "—";
        public string QuantityDisplay { get; set; } = "—";
        public string SlotDateFormatted { get; set; } = "";
        public string TimeRange { get; set; } = "";
        public string AcceptedAtFormatted { get; set; } = "";
    }

    public class CancelEmailData
    {
        public int ProcurementRequestId { get; set; }
        public string FarmerEmail { get; set; } = "";
        public string FarmerName { get; set; } = "";
        public string CropName { get; set; } = "";
        public string WarehouseName { get; set; } = "—";
        public string SlotDateDisplay { get; set; } = "—";
        public string CancelledAtFormatted { get; set; } = "";
        public string CancelReason { get; set; } = "Cancelled by Field Officer.";
        public string SlotSummary { get; set; } = "";
    }

    public class CancelRequestDto
    {
        public int RequestId { get; set; }
    }

    public class AcceptRequestDto
    {
        public int RequestId { get; set; }
    }
}
