namespace API.Models.FieldOfficer
{
    public class PaymentRequestDto
    {
        public int inspectionId { get; set; }
        public int farmerId { get; set; }
        public int procurementRequestId { get; set; }
        public decimal advanceAmount { get; set; }
    }

    public class AdvancePaymentRequestDto
    {
        public int procurementRequestId { get; set; }
        public int farmerId { get; set; }
        public int inspectionId { get; set; }
        public decimal advanceAmount { get; set; }
    }
}
