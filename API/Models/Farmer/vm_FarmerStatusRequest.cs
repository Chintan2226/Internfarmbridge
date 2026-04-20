using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Farmer
{
    public class vm_FarmerStatusRequest
    {
        public int UserId { get; set; }
        public bool Status { get; set; }
        public string Reason { get; set; }

        public int? AdminId { get; set; }
    }
}