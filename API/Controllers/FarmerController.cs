using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FarmerController : ControllerBase
    {
        public FarmerController()
        {
            
        }

        [Authorize(Roles ="farmer")]
        [HttpGet]
        public IActionResult GetOk()
        {
            return Ok("Authorized");
        }
    }
}