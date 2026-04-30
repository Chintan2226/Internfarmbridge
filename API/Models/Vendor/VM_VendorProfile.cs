// API/Models/Vendor/VM_VendorProfile.cs
namespace API.Models.Vendor
{
    public class VM_VendorProfile
    {
        public int VendorId { get; set; }
        public string BusinessName { get; set; } = "";      // c_business_name
        public string ContactPerson { get; set; } = "";     // c_contact_person
        public string Email { get; set; } = "";             // from t_users
        public string Phone { get; set; } = "";             // c_phone
        public string Gstin { get; set; } = "";             // c_gstin
        public string ProfileImageUrl { get; set; } = "";   // from t_users
        public string OnboardingStatus { get; set; } = "";  // c_onboarding_status
        public DateTime MemberSince { get; set; }           // c_created_at
        public bool IsEmailEditable { get; set; } = false;
    }

    public class VM_UpdateProfileRequest
    {
        public int VendorId { get; set; }
        public string BusinessName { get; set; } = "";
        public string ContactPerson { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Gstin { get; set; } = "";
    }
}