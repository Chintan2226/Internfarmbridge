namespace API.Models.Vendor
{
    public class VM_AddToCartRequest
    {
        public int CropId { get; set; }
        public decimal Quantity { get; set; }
        public string Grade { get; set; }
    }
}