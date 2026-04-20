// using System.Data;
// using API.BAL;
// using API.Models.Admin;
// using API.Models.Farmer;
// using API.Models.Payment;
// using API.Models.Notification;
// using API.Services;
// using Microsoft.AspNetCore.Mvc;
// using Npgsql;

// namespace API.Controllers
// {
//     [ApiController]
//     [Route("api/[controller]")]
//     public class AdminController : ControllerBase
//     {
//         private readonly RedisService _redisService;
//         private readonly RabbitMqService _rabbitMqService;
//         private readonly NpgsqlConnection _conn;
//         private readonly AdminHelper _adminRepo;
//         private readonly CloudinaryService _cloudinary;
//         private readonly EmailService _emailService;
//         private const string NotifKey = "admin:notifs";

//         public AdminController(
//             AdminHelper adminRepo,
//             CloudinaryService cloudinary,
//             RedisService redisService,
//             RabbitMqService rabbitMqService,
//             EmailService emailService,
//             NpgsqlConnection conn
//         )
//         {
//             _adminRepo = adminRepo;
//             _cloudinary = cloudinary;
//             _redisService = redisService;
//             _rabbitMqService = rabbitMqService;
//             _emailService = emailService;
//             _conn = conn;
//         }

//         [HttpGet("TriggerTest")]
//         public async Task<IActionResult> TriggerTest()
//         {
//             var model = new vm_Notification
//             {
//                 Title = "New Vendor Registration",
//                 Message = "Business 'FarmBridge' is awaiting your approval.",
//                 Type = "Info",
//                 Category = "Registration",
//                 RedirectUrl = "/Admin/Vendors",
//                 CreatedAt = DateTime.Now,
//             };

//             // DEBUG: See what is being sent
//             Console.WriteLine($"Sending: {model.Title}");

//             await _rabbitMqService.PublishNotification(model);

//             return Ok(new { message = "Sent", sentData = model });
//         }

//         [HttpPost("ClearAllNotifications")]
//         public async Task<IActionResult> ClearAllNotifications()
//         {
//             try
//             {
//                 _redisService.DeleteAsync(NotifKey);

//                 // // Option 2: Set it to an empty list (Better for AppendToListAsync logic)
//                 // await _redisService.SetAsync(NotifKey, new List<vm_Notification>(), TimeSpan.FromDays(7));

//                 return Ok(new { success = true, message = "All notifications cleared." });
//             }
//             catch (Exception ex)
//             {
//                 return StatusCode(500, new { success = false, message = ex.Message });
//             }
//         }

//         [HttpPost("MarkAsRead")]
//         public async Task<IActionResult> MarkAsRead([FromForm] string id)
//         {
//             var list = await _redisService.GetAsync<List<vm_Notification>>(NotifKey);
//             var item = list?.FirstOrDefault(x => x.Id == id);
//             if (item != null)
//             {
//                 item.IsRead = true;
//                 await _redisService.SetAsync(NotifKey, list, TimeSpan.FromDays(7));
//                 return Ok(new { success = true });
//             }
//             return NotFound();
//         }

//         [HttpPost("DeleteNotification")]
//         public async Task<IActionResult> DeleteNotification([FromForm] string id)
//         {
//             var list = await _redisService.GetAsync<List<vm_Notification>>(NotifKey);
//             if (list != null)
//             {
//                 // ✅ Remove the specific message from the Redis list
//                 var itemToRemove = list.FirstOrDefault(x => x.Id == id);
//                 if (itemToRemove != null)
//                 {
//                     list.Remove(itemToRemove);
//                     await _redisService.SetAsync(NotifKey, list, TimeSpan.FromDays(7));
//                     return Ok(new { success = true });
//                 }
//             }
//             return NotFound();
//         }

//         [HttpGet("GetNotifications")]
//         public async Task<IActionResult> GetNotifications()
//         {
//             try
//             {
//                 // Fetch the list from Redis using your NotifKey ("admin:notifs")
//                 var data = await _redisService.GetAsync<List<vm_Notification>>(NotifKey);

//                 // If Redis is empty, return an empty list instead of null
//                 return Ok(new { success = true, data = data ?? new List<vm_Notification>() });
//             }
//             catch (Exception ex)
//             {
//                 // Log the error and return a safe response
//                 Console.WriteLine($"Error fetching notifications: {ex.Message}");
//                 return Ok(
//                     new
//                     {
//                         success = false,
//                         data = new List<vm_Notification>(),
//                         message = "Error retrieving notifications from cache.",
//                     }
//                 );
//             }
//         }

//         [HttpPost("MarkAllRead")]
//         public async Task<IActionResult> MarkAllRead()
//         {
//             var list = await _redisService.GetAsync<List<vm_Notification>>(NotifKey);
//             if (list != null)
//             {
//                 list.ForEach(x => x.IsRead = true);
//                 await _redisService.SetAsync(NotifKey, list, TimeSpan.FromDays(7));
//             }
//             return Ok(new { success = true });
//         }

//         [HttpGet("list")]
//         public async Task<IActionResult> GetList(string searchTerm = "", int pageNumber = 1)
//         {
//             try
//             {
//                 // The BAL now returns a clean list of objects with the correct property names
//                 var data = await _adminRepo.GetFarmerList(searchTerm, pageNumber);

//                 return Ok(new
//                 {
//                     success = true,
//                     data = data
//                 });
//             }
//             catch (Exception ex)
//             {
//                 // Return a structured error response
//                 return StatusCode(500, new
//                 {
//                     success = false,
//                     message = "Error retrieving farmer list",
//                     error = ex.Message
//                 });
//             }
//         }

//         [HttpGet("{id}/pending-payments")]
//         public async Task<IActionResult> GetPendingPayments(int id)
//         {
//             var data = await _adminRepo.GetPendingPaymentsAsync(id);
//             return Ok(new { success = true, data });
//         }

//         [HttpPut("approve-payment")]
//         public async Task<IActionResult> ApprovePayment([FromBody] vm_PaymentApproveRequest req)
//         {
//             // 1. We removed the UTR string validation check here
//             if (req == null || req.PaymentId == 0)
//                 return BadRequest(new { success = false, message = "Valid Payment ID is required." });

//             // 2. Auto-generate a secure transaction reference (e.g., FB-UTR-20260413-A1B2C3)
//             string generatedUtr = $"FB-UTR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";

//             // 3. Attach it to the request before sending it to the database
//             req.UtrReference = generatedUtr;

//             bool success = await _adminRepo.ApproveFarmerPaymentAsync(req);

//             if (success) return Ok(new { success = true, message = "Payment successfully released." });
//             return StatusCode(500, new { success = false, message = "Failed to process payment." });
//         }

//         [HttpGet("dashboard")]
//         public async Task<IActionResult> GetDashboard()
//         {
//             var data = await _adminRepo.GetDashboardCounts();

//             if (data == null)
//                 return StatusCode(500, "Error fetching dashboard");

//             return Ok(data);
//         }


//         [HttpGet("details/{id:int}")]
//         public async Task<IActionResult> GetDetails(int id)
//         {
//             try
//             {
//                 var res = await _adminRepo.GetFarmerFullDetailById(id);

//                 // If the Profile ID is 0, the record was not found
//                 if (res.Profile == null || res.Profile.FarmerID == 0)
//                 {
//                     return NotFound(new { success = false, message = "Farmer not found." });
//                 }

//                 return Ok(new { success = true, data = res });
//             }
//             catch (Exception ex)
//             {
//                 return StatusCode(500, new { success = false, message = ex.Message });
//             }
//         }

//         [HttpPut("update-status")]
//         public async Task<IActionResult> UpdateStatus([FromBody] vm_FarmerStatusRequest request)
//         {
//             // Added !request.AdminId.HasValue check for total safety
//             if (request == null || !request.AdminId.HasValue || request.AdminId == 0)
//                 return BadRequest("Admin Identity is required.");

//             bool success = await _adminRepo.UpdateFarmerStatusAsync(request);

//             return success ? Ok(new { success = true, message = "Status updated and logged." })
//                            : StatusCode(500, new { success = false, message = "Update failed. Farmer ID may be invalid." });
//         }



//         private List<Dictionary<string, object>> ConvertTable(DataTable table)
//         {
//             var list = new List<Dictionary<string, object>>();
//             foreach (DataRow row in table.Rows)
//             {
//                 var dict = new Dictionary<string, object>();
//                 foreach (DataColumn col in table.Columns)
//                     dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
//                 list.Add(dict);
//             }
//             return list;
//         }


//         [HttpGet("GetDashboardKpi")]
//         public async Task<IActionResult> GetDashboardKpi()
//         {
//             string cacheKey = "dashboard:kpi";

//             try
//             {
//                 // ✅ 1. Check cache
//                 var cachedData = await _redisService.GetAsync<vm_DashboardKpi>(cacheKey);

//                 if (cachedData != null)
//                 {
//                     return Ok(
//                         new
//                         {
//                             success = true,
//                             data = cachedData,
//                             source = "cache",
//                         }
//                     );
//                 }

//                 // ❌ Not in cache → fetch from DB
//                 var kpi = new vm_DashboardKpi
//                 {
//                     TodayRevenue = await _adminRepo.GetTodayRevenue(),
//                     TotalFarmers = await _adminRepo.GetTotalFarmers(),
//                     TotalVendors = await _adminRepo.GetTotalVendors(),
//                     ActiveFOs = await _adminRepo.GetActiveFOs(),
//                     TodayNewOrders = await _adminRepo.GetTodayNewOrders(),
//                 };

//                 // ✅ 2. Store in cache (60 mins)
//                 await _redisService.SetAsync(cacheKey, kpi, TimeSpan.FromMinutes(60));

//                 return Ok(
//                     new
//                     {
//                         success = true,
//                         data = kpi,
//                         source = "db",
//                     }
//                 );
//             }
//             catch (Exception ex)
//             {
//                 return StatusCode(500, new { success = false, message = ex.Message });
//             }
//         }

//         // GET api/AdminApi/GetRevenueChart?period=monthly
//         [HttpGet("GetRevenueChart")]
//         public async Task<IActionResult> GetRevenueChart([FromQuery] string period = "monthly")
//         {
//             string cacheKey = $"dashboard:revenue:{period}";

//             // ✅ FIXED: Use strong type
//             var cachedData = await _redisService.GetAsync<List<vm_ChartPoint>>(cacheKey);

//             if (cachedData != null)
//             {
//                 return Ok(
//                     new
//                     {
//                         success = true,
//                         data = cachedData,
//                         source = "cache",
//                     }
//                 );
//             }

//             var data = await _adminRepo.GetRevenueChart(period);

//             // ✅ Store same type
//             await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));

//             return Ok(
//                 new
//                 {
//                     success = true,
//                     data,
//                     source = "db",
//                 }
//             );
//         }

//         // GET api/AdminApi/GetOrderVolumeChart?period=monthly
//         [HttpGet("GetOrderVolumeChart")]
//         public async Task<IActionResult> GetOrderVolumeChart([FromQuery] string period = "monthly")
//         {
//             string cacheKey = $"dashboard:orders:{period}";

//             // 1. Try to get from Cache
//             // If your vm_ChartPoint has properties "Label" and "Value",
//             // but Redis has "label" and "value", this might return defaults.
//             var cachedData = await _redisService.GetAsync<List<vm_ChartPoint>>(cacheKey);

//             // Check if data exists AND has actual values (not just a list of zeros)
//             if (cachedData != null && cachedData.Any(x => !string.IsNullOrEmpty(x.Label)))
//             {
//                 return Ok(
//                     new
//                     {
//                         success = true,
//                         data = cachedData,
//                         source = "cache",
//                     }
//                 );
//             }

//             // 2. Fallback to DB
//             var data = await _adminRepo.GetRevenueChart(period);

//             // 3. Store the actual Model list, not an anonymous object
//             // Most Redis services use System.Text.Json which defaults to CamelCase
//             await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));

//             return Ok(
//                 new
//                 {
//                     success = true,
//                     data = data,
//                     source = "db",
//                 }
//             );
//         }

//         // GET api/AdminApi/GetTodayCropListings
//         [HttpGet("GetTodayCropListings")]
//         public async Task<IActionResult> GetTodayCropListings()
//         {
//             string cacheKey = "dashboard:today:crops";

//             var cachedData = await _redisService.GetAsync<object>(cacheKey);

//             if (cachedData != null)
//             {
//                 return Ok(new { success = true, data = cachedData });
//             }

//             var data = await _adminRepo.GetTodayCropListings();

//             await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));

//             return Ok(new { success = true, data });
//         }

//         // GET api/AdminApi/GetPendingApprovals
//         private static HashSet<string> _sentNotifications = new HashSet<string>();

//         [HttpGet]
//         [Route("GetPendingApprovals")]
//         public async Task<IActionResult> GetPendingApprovals()
//         {
//             var data = await _adminRepo.GetPendingApprovals();
//             return Ok(new { success = true, data });
//         }

//         // POST api/AdminApi/ApproveUser
//         [HttpPost]
//         [Route("ApproveUser")]
//         public async Task<IActionResult> ApproveUser([FromForm] int userId)
//         {
//             var status = await _adminRepo.ApproveUser(userId);
//             if (status == 1)
//                 return Ok(new { success = true, message = "User approved successfully." });
//             return BadRequest(new { success = false, message = "Approval failed." });
//         }

//         // ── GET: api/CatalogProductApi/GetAll ────────────────────────────────
//         [HttpGet("GetAll")]
//         public async Task<IActionResult> GetAll(
//             [FromQuery] string? category,
//             [FromQuery] string? unit
//         )
//         {
//             var list = await _adminRepo.GetAllCatalogProducts(category, unit);
//             return Ok(list);
//         }

//         // ── GET: api/CatalogProductApi/GetById/{id} ───────────────────────────
//         [HttpGet("GetById/{id:long}")]
//         public async Task<IActionResult> GetById(long id)
//         {
//             var product = await _adminRepo.GetCatalogProductById(id);

//             if (product == null)
//                 return NotFound(new { success = false, message = "Product not found." });

//             return Ok(product);
//         }

//         // ── POST: api/CatalogProductApi/Add ──────────────────────────────────
//         // Accepts multipart/form-data so an image file can be sent with the form fields
//         [HttpPost("Add")]
//         [Consumes("multipart/form-data")]
//         public async Task<IActionResult> Add([FromForm] vm_CatalogProductForm form)
//         {
//             if (string.IsNullOrWhiteSpace(form.Name))
//                 return BadRequest(new { success = false, message = "Product name is required." });
//             if (string.IsNullOrWhiteSpace(form.UnitOfMeasure))
//                 return BadRequest(
//                     new { success = false, message = "Unit of measure is required." }
//                 );

//             // ── Step 1: Upload image to Cloudinary (if provided) ──────────────
//             string? imageUrl = null;

//             if (form.ImageFile != null && form.ImageFile.Length > 0)
//             {
//                 var upload = await _cloudinary.UploadImageAsync(form.ImageFile);
//                 if (!upload.Success)
//                     return BadRequest(
//                         new { success = false, message = $"Image upload failed: {upload.Error}" }
//                     );

//                 imageUrl = upload.SecureUrl;
//                 // SecureUrl is stored in c_image_url
//                 // public_id is derivable anytime via _cloudinary.ExtractPublicId(imageUrl)
//             }

//             // ── Step 2: Save to DB via helper ─────────────────────────────────
//             var model = new vm_CatalogProduct
//             {
//                 Name = form.Name.Trim(),
//                 Category = form.Category?.Trim(),
//                 UnitOfMeasure = form.UnitOfMeasure.Trim(),
//                 Description = form.Description?.Trim(),
//                 ImageUrl = imageUrl,
//                 QualityParameters = form.QualityParameters?.Trim(),
//                 IsActive = form.IsActive,
//             };

//             long newId = await _adminRepo.AddCatalogProduct(model, 1);

//             if (newId > 0)
//                 return Ok(
//                     new
//                     {
//                         success = true,
//                         message = "Catalog product added successfully.",
//                         id = newId,
//                         imageUrl,
//                     }
//                 );

//             return StatusCode(
//                 500,
//                 new { success = false, message = "Failed to add catalog product." }
//             );
//         }

//         // ── PUT: api/CatalogProductApi/Edit ──────────────────────────────────
//         [HttpPut("Edit")]
//         [Consumes("multipart/form-data")]
//         public async Task<IActionResult> Edit([FromForm] vm_CatalogProductForm form)
//         {
//             if (form.Id == 0)
//                 return BadRequest(new { success = false, message = "Product ID is required." });
//             if (string.IsNullOrWhiteSpace(form.Name))
//                 return BadRequest(new { success = false, message = "Product name is required." });
//             if (string.IsNullOrWhiteSpace(form.UnitOfMeasure))
//                 return BadRequest(
//                     new { success = false, message = "Unit of measure is required." }
//                 );

//             // Check dependencies (non-blocking warning)
//             var dep = await _adminRepo.CheckDependencies(form.Id);

//             // ── Step 1: Replace image on Cloudinary if a new file was sent ────
//             // ExistingImageUrl is the current c_image_url passed back from the form.
//             // CloudinaryService.ReplaceImageAsync internally calls ExtractPublicId()
//             // to derive the old public_id — no extra DB column needed.
//             string? imageUrl = form.ExistingImageUrl;

//             if (form.ImageFile != null && form.ImageFile.Length > 0)
//             {
//                 var replace = await _cloudinary.ReplaceImageAsync(
//                     form.ImageFile,
//                     form.ExistingImageUrl
//                 );
//                 if (!replace.Success)
//                     return BadRequest(
//                         new { success = false, message = $"Image upload failed: {replace.Error}" }
//                     );

//                 imageUrl = replace.SecureUrl;
//             }

//             // ── Step 2: Update DB via helper ──────────────────────────────────
//             var model = new vm_CatalogProduct
//             {
//                 Id = form.Id,
//                 Name = form.Name.Trim(),
//                 Category = form.Category?.Trim(),
//                 UnitOfMeasure = form.UnitOfMeasure.Trim(),
//                 Description = form.Description?.Trim(),
//                 ImageUrl = imageUrl,
//                 QualityParameters = form.QualityParameters?.Trim(),
//                 IsActive = form.IsActive,
//             };

//             int rows = await _adminRepo.EditCatalogProduct(model, 1);

//             if (rows > 0)
//                 return Ok(
//                     new
//                     {
//                         success = true,
//                         message = "Catalog product updated.",
//                         warning = dep.WarningMessage,
//                         imageUrl,
//                     }
//                 );

//             return StatusCode(
//                 500,
//                 new { success = false, message = "Failed to update catalog product." }
//             );
//         }

//         // ── DELETE: api/CatalogProductApi/Delete/{id} ────────────────────────
//         [HttpDelete("Delete/{id:long}")]
//         public async Task<IActionResult> Delete(long id)
//         {
//             // Helper returns the stored c_image_url so we can clean up Cloudinary
//             var (success, message, existingImageUrl) = await _adminRepo.DeleteCatalogProduct(id, 1);

//             if (success)
//             {
//                 // ── Step 2: Delete image from Cloudinary ──────────────────────
//                 // public_id is derived from the stored URL — no extra column needed
//                 if (!string.IsNullOrWhiteSpace(existingImageUrl))
//                 {
//                     string? publicId = _cloudinary.ExtractPublicId(existingImageUrl);
//                     if (!string.IsNullOrWhiteSpace(publicId))
//                         await _cloudinary.DeleteImageAsync(publicId);
//                 }

//                 return Ok(new { success = true, message });
//             }

//             return BadRequest(new { success = false, message });
//         }

//         // ── PUT: api/CatalogProductApi/ToggleStatus ───────────────────────────
//         [HttpPut("ToggleStatus")]
//         public async Task<IActionResult> ToggleStatus([FromBody] vm_ToggleStatus model)
//         {
//             int rows = await _adminRepo.ToggleCatalogProductStatus(model.Id, model.IsActive, 1);

//             if (rows > 0)
//                 return Ok(
//                     new
//                     {
//                         success = true,
//                         message = $"Product {(model.IsActive ? "activated" : "deactivated")} successfully.",
//                     }
//                 );

//             return StatusCode(500, new { success = false, message = "Failed to update status." });
//         }

//         // ── GET: api/CatalogProductApi/CheckDependencies/{id} ────────────────
//         [HttpGet("CheckDependencies/{id:long}")]
//         public async Task<IActionResult> CheckDependencies(long id)
//         {
//             var dep = await _adminRepo.CheckDependencies(id);
//             return Ok(dep);
//         }

//         // ── GET: api/CatalogProductApi/GetCategories ──────────────────────────
//         [HttpGet("GetCategories")]
//         public async Task<IActionResult> GetCategories()
//         {
//             var categories = await _adminRepo.GetDistinctCategories();
//             return Ok(categories);
//         }

//         // ── GET: api/CatalogProductApi/GetUnits ───────────────────────────────
//         [HttpGet("GetUnits")]
//         public async Task<IActionResult> GetUnits()
//         {
//             var units = await _adminRepo.GetDistinctUnits();
//             return Ok(units);
//         }

//         // Chintan Dankhara The field officer manageent at the Admin side
//         [HttpGet("fo/list")]
//         public async Task<IActionResult> GetFOList(
//             [FromQuery] string search = "",
//             [FromQuery] int page = 1)
//         {
//             var list = await _adminRepo.GetAllFieldOfficersAsync(search, page);
//             return Ok(new { success = true, data = list });
//         }

//         [HttpPost("create")]
//         public async Task<IActionResult> Create([FromBody] CreateFieldOfficerViewModel model)
//         {
//             if (!ModelState.IsValid)
//                 return BadRequest(new { success = false, message = "Invalid data" });

//             var result = await _adminRepo.CreateFieldOfficerAsync(model, 1);

//             if (!result.Success)
//                 return BadRequest(new { success = false, message = result.Message });

//             // ✅ TRIGGER EMAIL: Send the Field Officer their auto-generated password!
//             await _emailService.SendPasswordEmailAsync(model.Email, result.TempPassword, model.FullName);

//             return Ok(new
//             {
//                 success = true,
//                 message = result.Message,
//                 userId = result.UserId,
//                 tempPassword = result.TempPassword
//             });
//         }

//         [HttpPost("ToggleFOStatus")]
//         public async Task<IActionResult> ToggleFOStatus(
//             [FromForm] int userId,
//             [FromForm] bool status
//         )
//         {
//             // Reuses the simple update logic for t_users.c_is_active
//             string qry = "UPDATE public.t_users SET c_is_active = @status WHERE c_id = @id";
//             using (var cmd = new NpgsqlCommand(qry, _conn))
//             {
//                 cmd.Parameters.AddWithValue("@status", status);
//                 cmd.Parameters.AddWithValue("@id", userId);
//                 if (_conn.State != System.Data.ConnectionState.Open)
//                     await _conn.OpenAsync();
//                 await cmd.ExecuteNonQueryAsync();
//                 await _conn.CloseAsync();
//             }
//             return Ok(new { success = true });
//         }

//         [HttpGet("warehouses")]
//         public async Task<IActionResult> GetWarehouses()
//         {
//             DataTable dt = await _adminRepo.GetActiveWarehousesAsync();

//             var list = new List<object>();
//             foreach (DataRow dr in dt.Rows)
//             {
//                 list.Add(new
//                 {
//                     WarehouseId = Convert.ToInt32(dr["WarehouseId"]),
//                     Name = dr["Name"].ToString(),
//                     District = dr["District"].ToString(),
//                     State = dr["State"].ToString()
//                 });
//             }

//             return Ok(new { success = true, data = list });
//         }

//         [HttpGet("field-officers")]
//         public async Task<IActionResult> GetFieldOfficers()
//         {
//             var data = await _adminRepo.GetFieldOfficersAsync();
//             // Returns raw array directly to match JS expectations
//             return Ok(data);
//         }

//         // ─────────────────────────────────────────────────────────────────────
//         // GET /api/Admin/FoDetail/{foId}
//         // Returns full profile + summary stats for the modal header
//         // ─────────────────────────────────────────────────────────────────────
//         [HttpGet("FoDetail/{foId:long}")]
//         public async Task<IActionResult> GetFoDetail(long foId)
//         {
//             var data = await _adminRepo.GetFoDetailAsync(foId);
//             if (data == null)
//                 return NotFound(new { success = false, message = "Field officer not found." });
 
//             return Ok(new { success = true, data });
//         }
 
//         // ─────────────────────────────────────────────────────────────────────
//         // GET /api/Admin/FoInspections/{foId}
//         // Returns all quality inspections performed by this FO
//         // ─────────────────────────────────────────────────────────────────────
//         [HttpGet("FoInspections/{foId:long}")]
//         public async Task<IActionResult> GetFoInspections(long foId)
//         {
//             var data = await _adminRepo.GetFoInspectionsAsync(foId);
//             return Ok(new
//             {
//                 success = true,
//                 total   = data.Count,
//                 passed  = data.Count(x => x.Passed),
//                 failed  = data.Count(x => !x.Passed),
//                 data
//             });
//         }
 
//         // ─────────────────────────────────────────────────────────────────────
//         // GET /api/Admin/FoWarehouseSlots/{foId}
//         // Returns all warehouse slot bookings linked to this FO's assignments
//         // ─────────────────────────────────────────────────────────────────────
//         [HttpGet("FoWarehouseSlots/{foId:long}")]
//         public async Task<IActionResult> GetFoWarehouseSlots(long foId)
//         {
//             var data = await _adminRepo.GetFoWarehouseSlotsAsync(foId);
//             return Ok(new
//             {
//                 success  = true,
//                 total    = data.Count,
//                 arrived  = data.Count(x => x.CheckInStatus == "arrived" || x.CheckInStatus == "completed"),
//                 noShow   = data.Count(x => x.CheckInStatus == "no_show"),
//                 data
//             });
//         }

//         [HttpPatch("{id}/update-status")]
//         public async Task<IActionResult> UpdateStatus(string id, [FromBody] FoStatusPatchRequest request)
//         {
//             var result = await _adminRepo.ToggleFoStatusAsync("1", id, request.IsActive, "Status changed via Admin UI");
//             if (!result.Success) return BadRequest(new { success = false, message = result.Message });
//             return Ok(new { success = true, message = result.Message });
//         }

//         // --- YOUR OTHER ADVANCED ENDPOINTS ---

//         [HttpGet("{foId}/Performance")]
//         public async Task<IActionResult> GetFoPerformance(string foId)
//         {
//             var data = await _adminRepo.GetFoPerformanceAsync(foId);
//             return Ok(new { success = true, data });
//         }

//         [HttpGet("analytics/field-officers/kpi")]
//         public async Task<IActionResult> GetFieldOfficerAnalyticsKpi()
//         {
//             var data = await _adminRepo.GetFieldOfficerAnalyticsKpiAsync();
//             return Ok(new { success = true, data });
//         }

//         [HttpGet("vendors")]
//         public async Task<IActionResult> GetVendors()
//         {
//             var data = await _adminRepo.GetVendorsAsync();
//             return Ok(new { success = true, data });
//         }

//         // GET /api/Admin/Vendors/{id}
//         [HttpGet("Vendors/{id}")]
//         public async Task<IActionResult> GetVendorById(string id)
//         {
//             if (string.IsNullOrWhiteSpace(id))
//                 return BadRequest(new { message = "Vendor ID is required." });

//             var vendor = await _adminRepo.GetVendorByIdAsync(Convert.ToInt32(id));

//             if (vendor == null)
//                 return NotFound(new { message = $"Vendor '{id}' not found." });

//             return Ok(new { success = true, data = vendor });
//         }

//         [HttpPut("{vendorId}/Status")] // Matches JS Status update
//         public async Task<IActionResult> UpdateVendorStatus(string vendorId, [FromQuery] string status, [FromBody] AdminActionRequest request)
//         {
//             if (!ModelState.IsValid) return BadRequest(ModelState);

//             var result = await _adminRepo.UpdateVendorStatusAsync(request.AdminId, vendorId, status, request.Reason);
//             if (!result.Success) return BadRequest(new { success = false, message = result.Message });

//             return Ok(new { success = true, message = result.Message });
//         }

//         [HttpGet("analytics/vendors/kpi")] // Matches JS KPI fetch
//         public async Task<IActionResult> GetVendorAnalyticsKpi()
//         {
//             var data = await _adminRepo.GetVendorAnalyticsKpiAsync();
//             return Ok(new { success = true, data });
//         }

//         [HttpGet("{vendorId}/DemandTrend")]
//         public async Task<IActionResult> GetVendorDemandTrend(string vendorId, [FromQuery] string timeFrame = "monthly", [FromQuery] string? cropType = null)
//         {
//             var data = await _adminRepo.GetVendorDemandTrendAsync(vendorId, timeFrame, cropType);
//             return Ok(new { success = true, data });
//         }

//         [HttpGet("kpi")]
//         public async Task<IActionResult> GetWarehouseKpis()
//         {
//             var data = await _adminRepo.GetWarehouseAnalyticsKpiAsync();
//             return Ok(new { success = true, data });
//         }

//         // GET api/admin/warehouses
//         [HttpGet("warehouses/all")]
//         public async Task<IActionResult> GetAllWarehouses()
//         {
//             var data = await _adminRepo.GetWarehousesAsync();
//             return Ok(new { success = true, data });
//         }

//         // GET api/admin/warehouses/{id}/Info
//         [HttpGet("{id}/Info")]
//         public async Task<IActionResult> GetWarehouseInfo(long id)
//         {
//             var data = await _adminRepo.GetWarehouseDetailsAsync((int)id);
//             if (data == null) return NotFound(new { success = false, message = "Warehouse not found" });
//             return Ok(new { success = true, data });
//         }

//         // GET api/admin/warehouses/{id}/Stock
//         [HttpGet("{id}/Stock")]
//         public async Task<IActionResult> GetWarehouseStock(long id)
//         {
//             var data = await _adminRepo.GetWarehouseStockAsync((int)id);
//             return Ok(new { success = true, data });
//         }

//         // POST api/admin/warehouses
//         [HttpPost("warehouse")]
//         public async Task<IActionResult> CreateWarehouse([FromBody] WarehouseCreateRequest request)
//         {
//             if (!ModelState.IsValid) return BadRequest(ModelState);
//             var result = await _adminRepo.CreateWarehouseAsync(request);
//             if (!result.Success) return BadRequest(new { success = false, message = result.Message });
//             return Ok(new { success = true, message = result.Message });
//         }

//         // PUT api/admin/warehouses/{id}
//         [HttpPut("warehouse/{id}")]
//         public async Task<IActionResult> UpdateWarehouse(long id, [FromBody] WarehouseUpdateRequest request)
//         {
//             if (!ModelState.IsValid) return BadRequest(ModelState);
//             var result = await _adminRepo.UpdateWarehouseAsync((int)id, request);
//             if (!result.Success) return BadRequest(new { success = false, message = result.Message });
//             return Ok(new { success = true, message = result.Message });
//         }

//         [HttpGet("{warehouseId:int}/inventory")]
//         public async Task<IActionResult> GetWarehouseInventory([FromRoute] int warehouseId)
//         {
//             try
//             {
//                 // CHANGED: _helper to _adminRepo to match your constructor
//                 var data = await _adminRepo.GetWarehouseInventoryAsync(warehouseId);
                
//                 // Return exactly what the JavaScript expects: { success: true, data: { ... } }
//                 return Ok(new { success = true, data });
//             }
//             catch (Exception ex)
//             {
//                 return StatusCode(500, new { success = false, message = "Failed to load inventory: " + ex.Message });
//             }
//         }

//         // PATCH api/admin/warehouses/{id}/toggle-status
//         [HttpPost("ToggleFOStatus")]
//         public async Task<IActionResult> ToggleFOStatus([FromForm] int userId, [FromForm] bool status)
//         {
//             // 1. Fetch the FO's email and name before we update them
//             string email = "", name = "";
//             string getInfoSql = "SELECT u.c_email, p.c_full_name FROM t_users u LEFT JOIN t_field_officer_profiles p ON u.c_id = p.c_user_id WHERE u.c_id = @id";
            
//             if (_conn.State != System.Data.ConnectionState.Open) await _conn.OpenAsync();
//             using (var fetchCmd = new NpgsqlCommand(getInfoSql, _conn))
//             {
//                 fetchCmd.Parameters.AddWithValue("@id", userId);
//                 using var reader = await fetchCmd.ExecuteReaderAsync();
//                 if (await reader.ReadAsync())
//                 {
//                     email = reader.IsDBNull(0) ? "" : reader.GetString(0);
//                     name = reader.IsDBNull(1) ? "Field Officer" : reader.GetString(1);
//                 }
//             }

//             // 2. Update the Status
//             string qry = "UPDATE public.t_users SET c_is_active = @status WHERE c_id = @id";
//             using (var cmd = new NpgsqlCommand(qry, _conn))
//             {
//                 cmd.Parameters.AddWithValue("@status", status);
//                 cmd.Parameters.AddWithValue("@id", userId);
//                 await cmd.ExecuteNonQueryAsync();
//             }

//             // ✅ TRIGGER EMAIL: Send Account Activated or Deactivated Email
//             if (!string.IsNullOrEmpty(email))
//             {
//                 await _emailService.SendFieldOfficerStatusChangedEmailAsync(email, name, "Assigned Region", status);
//             }

//             return Ok(new { success = true });
//         }
//         // ── GET: api/CatalogProductApi/GetLogs ───────────────────────────────
//         // [HttpGet("GetLogs")]
//         // public async Task<IActionResult> GetLogs([FromQuery] long? productId)
//         // {
//         //     var logs = await _adminRepo.GetCatalogLogs(productId);
//         //     return Ok(logs);
//         // }

//     }
// }

//     // ── Form model (multipart/form-data) ─────────────────────────────────────
//     public class vm_CatalogProductForm
//     {
//         public long Id { get; set; }
//         public string Name { get; set; } = string.Empty;
//         public string? Category { get; set; }
//         public string UnitOfMeasure { get; set; } = string.Empty;
//         public string? Description { get; set; }
//         public string? QualityParameters { get; set; }
//         public bool IsActive { get; set; } = true;

//         /// <summary>New image file to upload. Optional — omit to keep the existing image.</summary>
//         public IFormFile? ImageFile { get; set; }

//         /// <summary>
//         /// The current c_image_url value passed back by the edit form.
//         /// Used by ReplaceImageAsync to extract old public_id and delete it from Cloudinary.
//         /// </summary>
//         public string? ExistingImageUrl { get; set; }
//     }

//     // ── Toggle status request body ────────────────────────────────────────────
//     public class vm_ToggleStatus
//     {
//         public long Id { get; set; }
//         public bool IsActive { get; set; }
//     }

//     public class FOLoginRequest
//     {
//         public string Email { get; set; } = "";
//         public string Password { get; set; } = "";
//     }

//     public class ChangePasswordRequest
//     {
//         public string NewPassword { get; set; } = "";
//         public string ConfirmPassword { get; set; } = "";
//     }
// }

using System.Data;
using API.BAL;
using API.Models.Admin;
using API.Models.Farmer;
using API.Models.Payment;
using API.Models.Notification;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly RedisService _redisService;
        private readonly RabbitMqService _rabbitMqService;
        private readonly NpgsqlConnection _conn;
        private readonly AdminHelper _adminRepo;
        private readonly CloudinaryService _cloudinary;
        private readonly EmailService _emailService;
        private const string NotifKey = "admin:notifs";

        public AdminController(
            AdminHelper adminRepo,
            CloudinaryService cloudinary,
            RedisService redisService,
            RabbitMqService rabbitMqService,
            EmailService emailService,
            NpgsqlConnection conn
        )
        {
            _adminRepo = adminRepo;
            _cloudinary = cloudinary;
            _redisService = redisService;
            _rabbitMqService = rabbitMqService;
            _emailService = emailService;
            _conn = conn;
        }

        [HttpGet("TriggerTest")]
        public async Task<IActionResult> TriggerTest()
        {
            var model = new vm_Notification
            {
                Title = "New Vendor Registration",
                Message = "Business 'FarmBridge' is awaiting your approval.",
                Type = "Info",
                Category = "Registration",
                RedirectUrl = "/Admin/Vendors",
                CreatedAt = DateTime.Now,
            };

            Console.WriteLine($"Sending: {model.Title}");
            await _rabbitMqService.PublishNotification(model);
            return Ok(new { message = "Sent", sentData = model });
        }

        [HttpPost("ClearAllNotifications")]
        public async Task<IActionResult> ClearAllNotifications()
        {
            try
            {
                // Removed 'await' because DeleteAsync returns void in your wrapper
                _redisService.DeleteAsync(NotifKey);
                return Ok(new { success = true, message = "All notifications cleared." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("MarkAsRead")]
        public async Task<IActionResult> MarkAsRead([FromForm] string id)
        {
            var list = await _redisService.GetAsync<List<vm_Notification>>(NotifKey);
            var item = list?.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.IsRead = true;
                await _redisService.SetAsync(NotifKey, list, TimeSpan.FromDays(7));
                return Ok(new { success = true });
            }
            return NotFound();
        }

        [HttpPost("DeleteNotification")]
        public async Task<IActionResult> DeleteNotification([FromForm] string id)
        {
            var list = await _redisService.GetAsync<List<vm_Notification>>(NotifKey);
            if (list != null)
            {
                var itemToRemove = list.FirstOrDefault(x => x.Id == id);
                if (itemToRemove != null)
                {
                    list.Remove(itemToRemove);
                    await _redisService.SetAsync(NotifKey, list, TimeSpan.FromDays(7));
                    return Ok(new { success = true });
                }
            }
            return NotFound();
        }

        [HttpGet("GetNotifications")]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                var data = await _redisService.GetAsync<List<vm_Notification>>(NotifKey);
                return Ok(new { success = true, data = data ?? new List<vm_Notification>() });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching notifications: {ex.Message}");
                return Ok(new { success = false, data = new List<vm_Notification>(), message = "Error retrieving notifications from cache." });
            }
        }

        [HttpPost("MarkAllRead")]
        public async Task<IActionResult> MarkAllRead()
        {
            var list = await _redisService.GetAsync<List<vm_Notification>>(NotifKey);
            if (list != null)
            {
                list.ForEach(x => x.IsRead = true);
                await _redisService.SetAsync(NotifKey, list, TimeSpan.FromDays(7));
            }
            return Ok(new { success = true });
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList(string searchTerm = "", int pageNumber = 1)
        {
            try
            {
                var data = await _adminRepo.GetFarmerList(searchTerm, pageNumber);
                return Ok(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error retrieving farmer list", error = ex.Message });
            }
        }

        [HttpGet("{id}/pending-payments")]
        public async Task<IActionResult> GetPendingPayments(int id)
        {
            var data = await _adminRepo.GetPendingPaymentsAsync(id);
            return Ok(new { success = true, data });
        }

        [HttpPut("approve-payment")]
        public async Task<IActionResult> ApprovePayment([FromBody] vm_PaymentApproveRequest req)
        {
            if (req == null || req.PaymentId == 0)
                return BadRequest(new { success = false, message = "Valid Payment ID is required." });

            string generatedUtr = $"FB-UTR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
            req.UtrReference = generatedUtr;

            bool success = await _adminRepo.ApproveFarmerPaymentAsync(req);

            if (success) return Ok(new { success = true, message = "Payment successfully released." });
            return StatusCode(500, new { success = false, message = "Failed to process payment." });
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var data = await _adminRepo.GetDashboardCounts();
            if (data == null) return StatusCode(500, "Error fetching dashboard");
            return Ok(data);
        }

        [HttpGet("details/{id:int}")]
        public async Task<IActionResult> GetDetails(int id)
        {
            try
            {
                var res = await _adminRepo.GetFarmerFullDetailById(id);
                if (res.Profile == null || res.Profile.FarmerID == 0)
                {
                    return NotFound(new { success = false, message = "Farmer not found." });
                }
                return Ok(new { success = true, data = res });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("update-status")]
        public async Task<IActionResult> UpdateStatus([FromBody] vm_FarmerStatusRequest request)
        {
            if (request == null || !request.AdminId.HasValue || request.AdminId == 0)
                return BadRequest("Admin Identity is required.");

            bool success = await _adminRepo.UpdateFarmerStatusAsync(request);

            return success ? Ok(new { success = true, message = "Status updated and logged." })
                           : StatusCode(500, new { success = false, message = "Update failed. Farmer ID may be invalid." });
        }

        private List<Dictionary<string, object>> ConvertTable(DataTable table)
        {
            var list = new List<Dictionary<string, object>>();
            foreach (DataRow row in table.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                    dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                list.Add(dict);
            }
            return list;
        }

        [HttpGet("GetDashboardKpi")]
        public async Task<IActionResult> GetDashboardKpi()
        {
            string cacheKey = "dashboard:kpi";
            try
            {
                var cachedData = await _redisService.GetAsync<vm_DashboardKpi>(cacheKey);
                if (cachedData != null)
                {
                    return Ok(new { success = true, data = cachedData, source = "cache" });
                }

                var kpi = new vm_DashboardKpi
                {
                    TodayRevenue = await _adminRepo.GetTodayRevenue(),
                    TotalFarmers = await _adminRepo.GetTotalFarmers(),
                    TotalVendors = await _adminRepo.GetTotalVendors(),
                    ActiveFOs = await _adminRepo.GetActiveFOs(),
                    TodayNewOrders = await _adminRepo.GetTodayNewOrders(),
                };

                await _redisService.SetAsync(cacheKey, kpi, TimeSpan.FromMinutes(60));
                return Ok(new { success = true, data = kpi, source = "db" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("GetRevenueChart")]
        public async Task<IActionResult> GetRevenueChart([FromQuery] string period = "monthly")
        {
            string cacheKey = $"dashboard:revenue:{period}";
            var cachedData = await _redisService.GetAsync<List<vm_ChartPoint>>(cacheKey);

            if (cachedData != null)
            {
                return Ok(new { success = true, data = cachedData, source = "cache" });
            }

            var data = await _adminRepo.GetRevenueChart(period);
            await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));
            return Ok(new { success = true, data, source = "db" });
        }

        [HttpGet("GetOrderVolumeChart")]
        public async Task<IActionResult> GetOrderVolumeChart([FromQuery] string period = "monthly")
        {
            string cacheKey = $"dashboard:orders:{period}";
            var cachedData = await _redisService.GetAsync<List<vm_ChartPoint>>(cacheKey);

            if (cachedData != null && cachedData.Any(x => !string.IsNullOrEmpty(x.Label)))
            {
                return Ok(new { success = true, data = cachedData, source = "cache" });
            }

            var data = await _adminRepo.GetRevenueChart(period);
            await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));
            return Ok(new { success = true, data = data, source = "db" });
        }

        [HttpGet("GetTodayCropListings")]
        public async Task<IActionResult> GetTodayCropListings()
        {
            string cacheKey = "dashboard:today:crops";
            var cachedData = await _redisService.GetAsync<object>(cacheKey);

            if (cachedData != null)
            {
                return Ok(new { success = true, data = cachedData });
            }

            var data = await _adminRepo.GetTodayCropListings();
            await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));
            return Ok(new { success = true, data });
        }

        [HttpGet("GetPendingApprovals")]
        public async Task<IActionResult> GetPendingApprovals()
        {
            var data = await _adminRepo.GetPendingApprovals();
            return Ok(new { success = true, data });
        }

        [HttpPost("ApproveUser")]
        public async Task<IActionResult> ApproveUser([FromForm] int userId)
        {
            Console.WriteLine($"\n--- STARTING VENDOR APPROVAL FOR ID: {userId} ---");
            
            var status = await _adminRepo.ApproveUser(userId);
            Console.WriteLine($"AdminRepo Status returned: {status}");
            
            if (status == 1)
            {
                string email = "", businessName = "Vendor";
                
                try 
                {
                    if (_conn.State != System.Data.ConnectionState.Open) 
                        await _conn.OpenAsync();

                    string getInfoSql = @"
                        SELECT u.c_email, v.c_business_name 
                        FROM t_vendor_profiles v
                        JOIN t_users u ON v.c_user_id = u.c_id
                        WHERE v.c_id = @id OR u.c_id = @id 
                        LIMIT 1";
                    
                    using (var fetchCmd = new NpgsqlCommand(getInfoSql, _conn))
                    {
                        fetchCmd.Parameters.AddWithValue("@id", userId);
                        using var reader = await fetchCmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            email = !reader.IsDBNull(0) ? reader.GetString(0) : "";
                            businessName = !reader.IsDBNull(1) ? reader.GetString(1) : "Vendor";
                        }
                    }
                    _conn.Close();
                    
                    Console.WriteLine($"DB SEARCH RESULT -> Email: '{email}', Business: '{businessName}'");
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine($"DATABASE ERROR: {dbEx.Message}");
                }

                // 3. TRIGGER THE REAL EMAIL
                if (!string.IsNullOrEmpty(email))
                {
                    try 
                    {
                        Console.WriteLine($"ATTEMPTING TO SEND EMAIL TO: {email}...");
                        await _emailService.SendVendorApprovedEmailAsync(email, businessName);
                        Console.WriteLine("EMAIL SENT SUCCESSFULLY!");
                    }
                    catch (Exception emailEx)
                    {
                        Console.WriteLine($"EMAIL CRASHED: {emailEx.Message}");
                    }
                }
                else 
                {
                    Console.WriteLine("EMAIL SKIPPED: The email address was empty or not found in the DB!");
                }

                return Ok(new { success = true, message = "User approved successfully." });
            }
            
            return BadRequest(new { success = false, message = "Approval failed." });
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] string? category, [FromQuery] string? unit)
        {
            var list = await _adminRepo.GetAllCatalogProducts(category, unit);
            return Ok(list);
        }

        [HttpGet("GetById/{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var product = await _adminRepo.GetCatalogProductById(id);
            if (product == null) return NotFound(new { success = false, message = "Product not found." });
            return Ok(product);
        }

        [HttpPost("Add")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Add([FromForm] vm_CatalogProductForm form)
        {
            if (string.IsNullOrWhiteSpace(form.Name)) return BadRequest(new { success = false, message = "Product name is required." });
            if (string.IsNullOrWhiteSpace(form.UnitOfMeasure)) return BadRequest(new { success = false, message = "Unit of measure is required." });

            string? imageUrl = null;
            if (form.ImageFile != null && form.ImageFile.Length > 0)
            {
                var upload = await _cloudinary.UploadImageAsync(form.ImageFile);
                if (!upload.Success) return BadRequest(new { success = false, message = $"Image upload failed: {upload.Error}" });
                imageUrl = upload.SecureUrl;
            }

            var model = new vm_CatalogProduct
            {
                Name = form.Name.Trim(),
                Category = form.Category?.Trim(),
                UnitOfMeasure = form.UnitOfMeasure.Trim(),
                Description = form.Description?.Trim(),
                ImageUrl = imageUrl,
                QualityParameters = form.QualityParameters?.Trim(),
                IsActive = form.IsActive,
            };

            long newId = await _adminRepo.AddCatalogProduct(model, 1);

            if (newId > 0)
                return Ok(new { success = true, message = "Catalog product added successfully.", id = newId, imageUrl });

            return StatusCode(500, new { success = false, message = "Failed to add catalog product." });
        }

        [HttpPut("Edit")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Edit([FromForm] vm_CatalogProductForm form)
        {
            if (form.Id == 0) return BadRequest(new { success = false, message = "Product ID is required." });
            if (string.IsNullOrWhiteSpace(form.Name)) return BadRequest(new { success = false, message = "Product name is required." });
            if (string.IsNullOrWhiteSpace(form.UnitOfMeasure)) return BadRequest(new { success = false, message = "Unit of measure is required." });

            var dep = await _adminRepo.CheckDependencies(form.Id);
            string? imageUrl = form.ExistingImageUrl;

            if (form.ImageFile != null && form.ImageFile.Length > 0)
            {
                var replace = await _cloudinary.ReplaceImageAsync(form.ImageFile, form.ExistingImageUrl);
                if (!replace.Success) return BadRequest(new { success = false, message = $"Image upload failed: {replace.Error}" });
                imageUrl = replace.SecureUrl;
            }

            var model = new vm_CatalogProduct
            {
                Id = form.Id,
                Name = form.Name.Trim(),
                Category = form.Category?.Trim(),
                UnitOfMeasure = form.UnitOfMeasure.Trim(),
                Description = form.Description?.Trim(),
                ImageUrl = imageUrl,
                QualityParameters = form.QualityParameters?.Trim(),
                IsActive = form.IsActive,
            };

            int rows = await _adminRepo.EditCatalogProduct(model, 1);

            if (rows > 0) return Ok(new { success = true, message = "Catalog product updated.", warning = dep.WarningMessage, imageUrl });

            return StatusCode(500, new { success = false, message = "Failed to update catalog product." });
        }

        [HttpDelete("Delete/{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var (success, message, existingImageUrl) = await _adminRepo.DeleteCatalogProduct(id, 1);
            if (success)
            {
                if (!string.IsNullOrWhiteSpace(existingImageUrl))
                {
                    string? publicId = _cloudinary.ExtractPublicId(existingImageUrl);
                    if (!string.IsNullOrWhiteSpace(publicId)) await _cloudinary.DeleteImageAsync(publicId);
                }
                return Ok(new { success = true, message });
            }
            return BadRequest(new { success = false, message });
        }

        [HttpPut("ToggleStatus")]
        public async Task<IActionResult> ToggleStatus([FromBody] vm_ToggleStatus model)
        {
            int rows = await _adminRepo.ToggleCatalogProductStatus(model.Id, model.IsActive, 1);
            if (rows > 0) return Ok(new { success = true, message = $"Product {(model.IsActive ? "activated" : "deactivated")} successfully." });
            return StatusCode(500, new { success = false, message = "Failed to update status." });
        }

        [HttpGet("CheckDependencies/{id:long}")]
        public async Task<IActionResult> CheckDependencies(long id)
        {
            var dep = await _adminRepo.CheckDependencies(id);
            return Ok(dep);
        }

        [HttpGet("GetCategories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _adminRepo.GetDistinctCategories();
            return Ok(categories);
        }

        [HttpGet("GetUnits")]
        public async Task<IActionResult> GetUnits()
        {
            var units = await _adminRepo.GetDistinctUnits();
            return Ok(units);
        }

        [HttpGet("fo/list")]
        public async Task<IActionResult> GetFOList([FromQuery] string search = "", [FromQuery] int page = 1)
        {
            var list = await _adminRepo.GetAllFieldOfficersAsync(search, page);
            return Ok(new { success = true, data = list });
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateFieldOfficerViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data" });

            var result = await _adminRepo.CreateFieldOfficerAsync(model, 1);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            await _emailService.SendPasswordEmailAsync(model.Email, result.TempPassword, model.FullName);

            return Ok(new
            {
                success = true,
                message = result.Message,
                userId = result.UserId,
                tempPassword = result.TempPassword
            });
        }

        [HttpPost("ToggleFOStatus")]
        public async Task<IActionResult> ToggleFOStatus([FromForm] int userId, [FromForm] bool status)
        {
            string email = "", name = "";
            string getInfoSql = "SELECT u.c_email, p.c_full_name FROM t_users u LEFT JOIN t_field_officer_profiles p ON u.c_id = p.c_user_id WHERE u.c_id = @id";
            
            if (_conn.State != System.Data.ConnectionState.Open) 
                await _conn.OpenAsync();

            using (var fetchCmd = new NpgsqlCommand(getInfoSql, _conn))
            {
                fetchCmd.Parameters.AddWithValue("@id", userId);
                using var reader = await fetchCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    email = !reader.IsDBNull(0) ? reader.GetString(0) : "";
                    name = !reader.IsDBNull(1) ? reader.GetString(1) : "Field Officer";
                }
            }

            string qry = "UPDATE public.t_users SET c_is_active = @status WHERE c_id = @id";
            using (var cmd = new NpgsqlCommand(qry, _conn))
            {
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@id", userId);
                await cmd.ExecuteNonQueryAsync();
            }

            // CRITICAL FIX: Removed 'await' to prevent CS4008 compiler error
            _conn.Close();

            if (!string.IsNullOrEmpty(email))
            {
                await _emailService.SendFieldOfficerStatusChangedEmailAsync(email, name, "Assigned Region", status);
            }

            return Ok(new { success = true });
        }

        [HttpGet("warehouses")]
        public async Task<IActionResult> GetWarehouses()
        {
            DataTable dt = await _adminRepo.GetActiveWarehousesAsync();
            var list = new List<object>();
            foreach (DataRow dr in dt.Rows)
            {
                list.Add(new
                {
                    WarehouseId = Convert.ToInt32(dr["WarehouseId"]),
                    Name = dr["Name"].ToString(),
                    District = dr["District"].ToString(),
                    State = dr["State"].ToString()
                });
            }
            return Ok(new { success = true, data = list });
        }

        [HttpGet("field-officers")]
        public async Task<IActionResult> GetFieldOfficers()
        {
            var data = await _adminRepo.GetFieldOfficersAsync();
            return Ok(data);
        }

        [HttpGet("FoDetail/{foId:long}")]
        public async Task<IActionResult> GetFoDetail(long foId)
        {
            var data = await _adminRepo.GetFoDetailAsync(foId);
            if (data == null) return NotFound(new { success = false, message = "Field officer not found." });
            return Ok(new { success = true, data });
        }
 
        [HttpGet("FoInspections/{foId:long}")]
        public async Task<IActionResult> GetFoInspections(long foId)
        {
            var data = await _adminRepo.GetFoInspectionsAsync(foId);
            return Ok(new { success = true, total = data.Count, passed = data.Count(x => x.Passed), failed = data.Count(x => !x.Passed), data });
        }
 
        [HttpGet("FoWarehouseSlots/{foId:long}")]
        public async Task<IActionResult> GetFoWarehouseSlots(long foId)
        {
            var data = await _adminRepo.GetFoWarehouseSlotsAsync(foId);
            return Ok(new { success = true, total = data.Count, arrived = data.Count(x => x.CheckInStatus == "arrived" || x.CheckInStatus == "completed"), noShow = data.Count(x => x.CheckInStatus == "no_show"), data });
        }

        [HttpPatch("{id}/update-status")]
        public async Task<IActionResult> UpdateStatus(string id, [FromBody] FoStatusPatchRequest request)
        {
            var result = await _adminRepo.ToggleFoStatusAsync("1", id, request.IsActive, "Status changed via Admin UI");
            if (!result.Success) return BadRequest(new { success = false, message = result.Message });

            // ✅ 1. TRIGGER THE REAL EMAIL HERE (This is the endpoint the frontend actually uses!)
            try 
            {
                string email = "", name = "";
                // Safe query that checks both User ID and Profile ID just in case
                string getInfoSql = "SELECT u.c_email, p.c_full_name FROM t_users u JOIN t_field_officer_profiles p ON u.c_id = p.c_user_id WHERE p.c_id = @id OR u.c_id = @id LIMIT 1";
                
                if (_conn.State != System.Data.ConnectionState.Open) 
                    await _conn.OpenAsync();

                using (var fetchCmd = new NpgsqlCommand(getInfoSql, _conn))
                {
                    fetchCmd.Parameters.AddWithValue("@id", Convert.ToInt32(id));
                    using var reader = await fetchCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        email = !reader.IsDBNull(0) ? reader.GetString(0) : "";
                        name = !reader.IsDBNull(1) ? reader.GetString(1) : "Field Officer";
                    }
                }
                _conn.Close();

                // ✅ 2. Fire the corresponding template
                if (!string.IsNullOrEmpty(email))
                {
                    await _emailService.SendFieldOfficerStatusChangedEmailAsync(
                        email, 
                        name, 
                        "Assigned Region", 
                        request.IsActive // Passes true/false directly to decide which template to use
                    );
                }
            } 
            catch (Exception ex) 
            { 
                Console.WriteLine("Email trigger failed: " + ex.Message); 
            }

            return Ok(new { success = true, message = result.Message });
        }

        [HttpGet("{foId}/Performance")]
        public async Task<IActionResult> GetFoPerformance(string foId)
        {
            var data = await _adminRepo.GetFoPerformanceAsync(foId);
            return Ok(new { success = true, data });
        }

        [HttpGet("analytics/field-officers/kpi")]
        public async Task<IActionResult> GetFieldOfficerAnalyticsKpi()
        {
            var data = await _adminRepo.GetFieldOfficerAnalyticsKpiAsync();
            return Ok(new { success = true, data });
        }

        [HttpGet("vendors")]
        public async Task<IActionResult> GetVendors()
        {
            var data = await _adminRepo.GetVendorsAsync();
            return Ok(new { success = true, data });
        }

        [HttpGet("Vendors/{id}")]
        public async Task<IActionResult> GetVendorById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { message = "Vendor ID is required." });
            var vendor = await _adminRepo.GetVendorByIdAsync(Convert.ToInt32(id));
            if (vendor == null) return NotFound(new { message = $"Vendor '{id}' not found." });
            return Ok(new { success = true, data = vendor });
        }

        [HttpPut("{vendorId}/Status")]
        public async Task<IActionResult> UpdateVendorStatus(string vendorId, [FromQuery] string status, [FromBody] AdminActionRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _adminRepo.UpdateVendorStatusAsync(request.AdminId, vendorId, status, request.Reason);
            if (!result.Success) return BadRequest(new { success = false, message = result.Message });
            return Ok(new { success = true, message = result.Message });
        }

        [HttpGet("analytics/vendors/kpi")]
        public async Task<IActionResult> GetVendorAnalyticsKpi()
        {
            var data = await _adminRepo.GetVendorAnalyticsKpiAsync();
            return Ok(new { success = true, data });
        }

        [HttpGet("{vendorId}/DemandTrend")]
        public async Task<IActionResult> GetVendorDemandTrend(string vendorId, [FromQuery] string timeFrame = "monthly", [FromQuery] string? cropType = null)
        {
            var data = await _adminRepo.GetVendorDemandTrendAsync(vendorId, timeFrame, cropType);
            return Ok(new { success = true, data });
        }

        [HttpGet("kpi")]
        public async Task<IActionResult> GetWarehouseKpis()
        {
            var data = await _adminRepo.GetWarehouseAnalyticsKpiAsync();
            return Ok(new { success = true, data });
        }

        [HttpGet("warehouses/all")]
        public async Task<IActionResult> GetAllWarehouses()
        {
            var data = await _adminRepo.GetWarehousesAsync();
            return Ok(new { success = true, data });
        }

        [HttpGet("{id}/Info")]
        public async Task<IActionResult> GetWarehouseInfo(long id)
        {
            var data = await _adminRepo.GetWarehouseDetailsAsync((int)id);
            if (data == null) return NotFound(new { success = false, message = "Warehouse not found" });
            return Ok(new { success = true, data });
        }

        [HttpGet("{id}/Stock")]
        public async Task<IActionResult> GetWarehouseStock(long id)
        {
            var data = await _adminRepo.GetWarehouseStockAsync((int)id);
            return Ok(new { success = true, data });
        }

        [HttpPost("warehouse")]
        public async Task<IActionResult> CreateWarehouse([FromBody] WarehouseCreateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _adminRepo.CreateWarehouseAsync(request);
            if (!result.Success) return BadRequest(new { success = false, message = result.Message });
            return Ok(new { success = true, message = result.Message });
        }

        [HttpPut("warehouse/{id}")]
        public async Task<IActionResult> UpdateWarehouse(long id, [FromBody] WarehouseUpdateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _adminRepo.UpdateWarehouseAsync((int)id, request);
            if (!result.Success) return BadRequest(new { success = false, message = result.Message });
            return Ok(new { success = true, message = result.Message });
        }

        [HttpGet("{warehouseId:int}/inventory")]
        public async Task<IActionResult> GetWarehouseInventory([FromRoute] int warehouseId)
        {
            try
            {
                var data = await _adminRepo.GetWarehouseInventoryAsync(warehouseId);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load inventory: " + ex.Message });
            }
        }

        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(long id, [FromQuery] bool isActive)
        {
            var result = await _adminRepo.ToggleWarehouseStatusAsync((int)id, isActive);
            if (!result.Success) return BadRequest(new { success = false, message = result.Message });
            return Ok(new { success = true, message = result.Message });
        }

    }

    public class vm_CatalogProductForm
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? QualityParameters { get; set; }
        public bool IsActive { get; set; } = true;
        public IFormFile? ImageFile { get; set; }
        public string? ExistingImageUrl { get; set; }
    }

    public class vm_ToggleStatus
    {
        public long Id { get; set; }
        public bool IsActive { get; set; }
    }

    public class FOLoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class ChangePasswordRequest
    {
        public string NewPassword { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
    }
}