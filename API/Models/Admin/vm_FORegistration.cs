using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Admin
{
    public class vm_FORegistration
    {
        
    }

    public class CreateFieldOfficerViewModel
    {
        
        public string FullName { get; set; }
 
        
        public string Phone { get; set; }
 
       
        public string Email { get; set; }
 
        public int WarehouseId { get; set; }
 
        
        public string? AssignedRegion { get; set; }
 
        // Populated by controller from BAL DataTable
        public List<WarehouseDropdownItem> Warehouses { get; set; } = new();
    }
 
    // ─── Warehouse Dropdown Item ──────────────────────────────────────────
 
    public class WarehouseDropdownItem
    {
        public int    WarehouseId { get; set; }
        public string Name        { get; set; }
        public string District    { get; set; }
        public string State       { get; set; }
    }
 
    // ─── Result ViewModel returned from BAL ───────────────────────────────
 
    public class CreateFOResult
    {
        public bool    Success      { get; set; }
        public string? Message      { get; set; }
        public int?    UserId       { get; set; }
        public string? TempPassword { get; set; }
    }

    public class vm_FOStatusRequest
    {
        public int    UserId   { get; set; }
        public int    AdminId  { get; set; }
        public bool   IsActive { get; set; }
        public string Reason   { get; set; } = "";
    }

}