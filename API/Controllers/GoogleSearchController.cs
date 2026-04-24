using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GoogleSearchController : ControllerBase
    {
        private readonly SerpApiService _serpApi;

        public GoogleSearchController(SerpApiService serpApi)
        {
            _serpApi = serpApi;
        }

        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            var result = await _serpApi.SearchGoogle(query);
            return Content(result, "application/json");
        }
    }
}