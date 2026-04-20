using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Settings
{
    public class CloudinarySettings
    {
        public string CloudName     { get; set; } = string.Empty;
        public string ApiKey        { get; set; } = string.Empty;
        public string ApiSecret     { get; set; } = string.Empty;
 
        /// <summary>
        /// Default folder path inside your Cloudinary media library.
        /// e.g. "farmbridge/catalog"
        /// </summary>
        public string CatalogFolder { get; set; } = "farmbridge-catalog";
    }
}