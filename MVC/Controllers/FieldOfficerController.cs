using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MVC.Filters;

namespace MVC.Controllers
{

    [FieldOfficerAuthorize]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class FieldOfficerController : Controller
    {
        private readonly ILogger<FieldOfficerController> _logger;

        public FieldOfficerController(ILogger<FieldOfficerController> logger)
        {
            _logger = logger;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        // ✅ QC Request Management Binod
        public IActionResult QCRequest()
        {
            return View(); // will return Views/FieldOfficer/RequestManagement.cshtml
        }
        // ✅ Profile Ruman
        public IActionResult Profile()
        {
            return View(); // will return Views/FieldOfficer/Profile.cshtml
        }
        public IActionResult QualityParams()
        {
            return View(); // will return Views/FieldOfficer/Profile.cshtml
        }
        public IActionResult QualityForm()
        {
            return View(); // will return Views/FieldOfficer/QualityForm.cshtml
        }

        public IActionResult InspectionDetail()
        {
            return View(); // will return Views/FieldOfficer/InspectionDetail.cshtml
        }
        public IActionResult InspectionHistory()
        {
            return View(); // will return Views/FieldOfficer/InspectionDetail.cshtml
        }

        public IActionResult PaymentHistory()
        {
            return View(); // will return Views/FieldOfficer/PaymentHistory.cshtml
        }

        public IActionResult Catalog()
        {
            return View(); // will return Views/FieldOfficer/Profile.cshtml
        }
       
         public IActionResult  UploadImage()
        {
            return View(); 
        }
    

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}