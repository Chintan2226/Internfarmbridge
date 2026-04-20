namespace API.Models.Vendor
{
    public class VM_SaveAddressResponse
    {
        public bool Success { get; set; }
        public int AddressId { get; set; }
        public string Message { get; set; } = "";
    }
}