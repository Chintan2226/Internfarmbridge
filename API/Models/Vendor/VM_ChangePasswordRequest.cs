using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Vendor
{
    public class VM_ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = ""; 
        public string NewPassword { get; set; } = "";
    }
}