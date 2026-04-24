using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MVC.Models
{
    public class GoogleSearchViewModel
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public string Snippet { get; set; }
        public string Source { get; set; }
        public string Thumbnail { get; set; }
    }
}