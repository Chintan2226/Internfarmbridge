using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Admin
{
    public class vm_FarmerFullDetail
    {
        public vm_FarmerProfile Profile { get; set; } = new();
        public List<vm_BankDetail> BankDetails { get; set; } = new();
        public List<vm_CropHistory> CropHistory { get; set; } = new();
        public List<vm_OrderHistory> OrderHistory { get; set; } = new();
    }

    public class vm_FarmerProfile
    {
        public int FarmerID { get; set; }
        public string FullName { get; set; }
        public string MobileNumber { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Location { get; set; }
        public DateTime RegistrationDate { get; set; }
    }

    public class vm_BankDetail
    {
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string IFSCCode { get; set; }
        public string Status { get; set; }
    }

    public class vm_CropHistory
    {
        public int ListingID { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class vm_OrderHistory
    {
        public int OrderID { get; set; }
        public string CropName { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime OrderDate { get; set; }
    }
}