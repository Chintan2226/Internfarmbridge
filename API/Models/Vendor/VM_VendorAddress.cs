namespace API.Models.Vendor
{
    public class VM_VendorAddress
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string AddressLine1 { get; set; } = "";
        public string AddressLine2 { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string ZipCode { get; set; } = "";
        public bool IsDefault { get; set; }
    }
}