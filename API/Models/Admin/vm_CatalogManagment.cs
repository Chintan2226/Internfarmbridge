using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Admin
{
    public class vm_CatalogManagment
    {

    }

    // ── Catalog Product ViewModel ─────────────────────────────────────────────
    public class vm_CatalogProduct
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>
        /// Cloudinary secure URL stored in c_image_url.
        /// public_id is derived from this URL via CloudinaryService.ExtractPublicId()
        /// whenever delete or replace is needed — no extra DB column required.
        /// </summary>
        public string? ImageUrl { get; set; }

        public string? QualityParameters { get; set; }  // JSON string
        public bool IsActive { get; set; } = true;
        public long CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // ── Dependency Check ViewModel ────────────────────────────────────────────
    public class vm_CatalogDependency
    {
        public long ProductId { get; set; }
        public int ActiveListingsCount { get; set; }
        public int OpenOrdersCount { get; set; }
        public bool HasDependencies => ActiveListingsCount > 0 || OpenOrdersCount > 0;
        public string WarningMessage => HasDependencies
            ? $"Warning: This product has {ActiveListingsCount} active listing(s) and {OpenOrdersCount} open order(s). Editing may affect ongoing transactions."
            : string.Empty;
    }

    // ── Catalog Log ViewModel ─────────────────────────────────────────────────
    public class vm_CatalogLog
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public long AdminId { get; set; }
        public string? AdminName { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public DateTime LoggedAt { get; set; }
    }


    // ── Toggle status request body ────────────────────────────────────────────
}