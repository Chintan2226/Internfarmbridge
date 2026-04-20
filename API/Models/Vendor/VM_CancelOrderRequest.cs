namespace API.Models.Vendor
{
    public class VM_CancelOrderRequest
    {
        public string OrderId { get; set; } = "";
        public string Reason { get; set; } = "Cancelled by vendor";
    }
}