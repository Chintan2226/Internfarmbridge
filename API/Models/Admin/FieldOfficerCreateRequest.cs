using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Admin
{
    public class FieldOfficerCreateRequest
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public int WarehouseId { get; set; }
        public string AssignedRegion { get; set; }
    }
}
