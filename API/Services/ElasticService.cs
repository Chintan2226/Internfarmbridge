using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Models.Settings;
using System.Diagnostics;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using Elastic.Clients.Elasticsearch.Core.Search;
using System.Text.Json;

namespace API.Services
{
    public class ElasticService
    {
        private readonly ElasticsearchClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ElasticService> _logger;
        private readonly string _defaultIndex;

        public ElasticService(IConfiguration configuration, ILogger<ElasticService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var cloudId = _configuration["Elasticsearch:CloudId"];
            var apiKey = _configuration["Elasticsearch:ApiKey"];
            _defaultIndex = _configuration["Elasticsearch:DefaultIndex"] ?? "farmbridge";

            var settings = new ElasticsearchClientSettings(cloudId, new ApiKey(apiKey))
                .DefaultIndex(_defaultIndex)
                .EnableDebugMode();

            _client = new ElasticsearchClient(settings);

            // Initialize indexes on startup
            Task.Run(async () => await InitializeIndexesAsync()).Wait();
        }

        // ============== INDEX INITIALIZATION ==============

        private async Task InitializeIndexesAsync()
        {
            try
            {
                await CreateIndexIfNotExistsAsync("catalog_products");
                await CreateIndexIfNotExistsAsync("crop_listings");
                await CreateIndexIfNotExistsAsync("orders");
                await CreateIndexIfNotExistsAsync("users");
                await CreateIndexIfNotExistsAsync("qc_records");
                await CreateIndexIfNotExistsAsync("warehouses");
                await CreateIndexIfNotExistsAsync("vendor_catalog");
                await CreateIndexIfNotExistsAsync("procurement_requests");

                _logger.LogInformation("ElasticSearch indexes initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ElasticSearch indexes");
            }
        }

        // Naya reindex method add karo
        public async Task<int> ReindexProcurementRequestsByFoIdAsync(int foId)
        {
            int indexed = 0;
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
        SELECT 
            pr.c_id,
            pr.c_farmer_id,
            COALESCE(fp.c_full_name, '') as farmer_name,
            COALESCE(cp.c_name, '') as crop_name,
            pr.c_requested_quantity,
            COALESCE(pr.c_unit, 'kg'),
            COALESCE(fp.c_district, '') as location,
            wsb.c_slot_date,
            wsb.c_slot_time_start,
            wsb.c_slot_time_end,
            pr.c_status,
            pr.c_assigned_fo_id
        FROM t_procurement_requests pr
        LEFT JOIN t_farmer_profiles fp ON pr.c_farmer_id = fp.c_id
        LEFT JOIN t_farmer_crop_listings fcl ON pr.c_crop_listing_id = fcl.c_id
        LEFT JOIN t_catalog_products cp ON fcl.c_catalog_product_id = cp.c_id
        LEFT JOIN t_warehouse_slot_bookings wsb ON pr.c_slot_id = wsb.c_id
        WHERE pr.c_assigned_fo_id = @foId
          AND pr.c_status IN ('pending', 'scheduled')";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@foId", foId);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var doc = new Dictionary<string, object>
                {
                    ["ProcurementRequestId"] = reader.GetInt32(0),
                    ["FarmerId"] = reader.GetInt32(1),
                    ["FarmerName"] = reader.GetString(2),
                    ["CropName"] = reader.GetString(3),
                    ["Quantity"] = reader.GetDecimal(4),
                    ["Unit"] = reader.GetString(5),
                    ["Location"] = reader.GetString(6),
                    ["SlotDate"] = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    ["SlotTimeStart"] = reader.IsDBNull(8) ? null : reader.GetTimeSpan(8).ToString(@"hh\:mm"),
                    ["SlotTimeEnd"] = reader.IsDBNull(9) ? null : reader.GetTimeSpan(9).ToString(@"hh\:mm"),
                    ["Status"] = reader.GetString(10),
                    ["FoId"] = reader.GetInt32(11),
                    ["DocumentType"] = "procurement_request",
                    ["IndexedAt"] = DateTime.UtcNow
                };

                var response = await _client.IndexAsync(doc, idx => idx
                    .Index("procurement_requests")
                    .Id(reader.GetInt32(0).ToString())
                );

                if (response.IsValidResponse) indexed++;
            }

            _logger.LogInformation($"Reindexed {indexed} procurement requests for FO {foId}");
            return indexed;
        }

        // Search method add karo
        public async Task<SearchResponseModel<QCSearchResult>> SearchProcurementRequestsAsync(SearchRequestModel request)
        {
            var response = new SearchResponseModel<QCSearchResult>();

            try
            {
                int from = (request.Page - 1) * request.PageSize;

                Query query;
                if (!string.IsNullOrWhiteSpace(request.Query))
                {
                    query = new BoolQuery
                    {
                        Should = new List<Query>
                {
                    new WildcardQuery(new Field("FarmerName"))
                    {
                        Value = $"*{request.Query.ToLower()}*"
                    },
                    new WildcardQuery(new Field("CropName"))
                    {
                        Value = $"*{request.Query.ToLower()}*"
                    },
                    new WildcardQuery(new Field("Location"))
                    {
                        Value = $"*{request.Query.ToLower()}*"
                    },
                    new MultiMatchQuery
                    {
                        Query = request.Query,
                        Fields = new[] { "FarmerName^3", "CropName^2", "Location" },
                        Fuzziness = new Fuzziness("AUTO"),
                        Operator = Operator.Or
                    }
                },
                        MinimumShouldMatch = 1
                    };
                }
                else
                {
                    query = new MatchAllQuery();
                }

                // FoId filter — hamesha apply karo
                var finalQuery = new BoolQuery
                {
                    Must = new List<Query>
            {
                new TermQuery { Field = "FoId", Value = request.FoId ?? 0 },
                query
            }
                };

                var searchRequest = new SearchRequest("procurement_requests")
                {
                    From = from,
                    Size = request.PageSize,
                    Query = finalQuery
                };

                var result = await _client.SearchAsync<object>(searchRequest);

                Console.WriteLine($"=== PROCUREMENT SEARCH DEBUG ===");
                Console.WriteLine($"Query: {request.Query}, FoId: {request.FoId}, Total: {result.Total}");

                if (result.IsValidResponse && result.Documents.Any())
                {
                    foreach (var doc in result.Documents)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(doc);
                        using var document = System.Text.Json.JsonDocument.Parse(json);
                        var root = document.RootElement;

                        response.Results.Add(new QCSearchResult
                        {
                            ProcurementRequestId = root.TryGetProperty("ProcurementRequestId", out var prId) ? prId.GetInt32() : 0,
                            FarmerName = root.TryGetProperty("FarmerName", out var fn) ? fn.GetString() : null,
                            CropType = root.TryGetProperty("CropName", out var cn) ? cn.GetString() : null,
                            Quantity = root.TryGetProperty("Quantity", out var qty) ? qty.GetDecimal() : 0,
                            Location = root.TryGetProperty("Location", out var loc) ? loc.GetString() : null,
                            Status = root.TryGetProperty("Status", out var st) ? st.GetString() : null,
                            FoId = root.TryGetProperty("FoId", out var foid) ? foid.GetInt32() : 0,
                            SlotDate = root.TryGetProperty("SlotDate", out var sd) && sd.ValueKind != JsonValueKind.Null 
                                    ? sd.GetDateTime() : null,
                            StartTime = root.TryGetProperty("SlotTimeStart", out var st2) ? st2.GetString() : null,
                            EndTime = root.TryGetProperty("SlotTimeEnd", out var et) ? et.GetString() : null,

                        });
                    }

                    response.TotalCount = result.Total;
                    response.Page = request.Page;
                    response.PageSize = request.PageSize;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching procurement requests: {ex.Message}");
                _logger.LogError(ex, "Error searching procurement requests");
            }

            return response;
        }

        private async Task CreateIndexIfNotExistsAsync(string indexName)
        {
            var existsResponse = await _client.Indices.ExistsAsync(indexName);

            if (!existsResponse.Exists)
            {
                var createResponse = await _client.Indices.CreateAsync(indexName);

                if (!createResponse.IsValidResponse)
                {
                    _logger.LogWarning($"Failed to create index {indexName}: {createResponse.DebugInformation}");
                }
            }
        }

        // ============== UNIVERSAL SEARCH (FIXED FOR WAREHOUSES) ==============

        public async Task<List<UniversalSearchResult>> UniversalSearchForMVCAsync(string query, int limit = 50)
        {
            var results = new List<UniversalSearchResult>();

            try
            {
                // Search warehouses - WITH QUERY FILTER (not MatchAll)
                var warehouseResult = await _client.SearchAsync<WarehouseDocument>("warehouses", s => s
                    .Size(limit)
                    .Query(q => q.MatchAll())
                );

                Console.WriteLine($"Warehouse search - Total: {warehouseResult.Total}, IsValid: {warehouseResult.IsValidResponse}, Documents: {warehouseResult.Documents.Count}");
                // Search catalog_products
                var catalogResult = await _client.SearchAsync<CatalogProductDocument>("catalog_products", s => s
                    .Size(limit)
                    .Query(q => q.QueryString(qs => qs.Query($"*{query}*")))
                );

                // Search users
                var userResult = await _client.SearchAsync<UserDocument>("users", s => s
                    .Size(limit)
                    .Query(q => q.QueryString(qs => qs
                        .Query($"*{query}*")
                        .Fields(new[] {
                            "fullName^3",
                            "email^2",
                            "businessName^2",
                            "phone",
                            "role"
                        })
                    ))
                );

                // Search crop_listings
                var cropResult = await _client.SearchAsync<CropListingDocument>("crop_listings", s => s
                    .Size(limit)
                    .Query(q => q.QueryString(qs => qs.Query($"*{query}*")))
                );

                // Search orders
                var orderResult = await _client.SearchAsync<OrderDocument>("orders", s => s
                    .Size(limit)
                    .Query(q => q.QueryString(qs => qs.Query($"*{query}*")))
                );

                // Add warehouses - Manual dictionary mapping
                if (warehouseResult.IsValidResponse && warehouseResult.Documents.Any())
                {
                    foreach (var warehouse in warehouseResult.Documents)
                    {
                        results.Add(new UniversalSearchResult
                        {
                            Type = "warehouse",
                            Id = warehouse.Id,
                            Title = warehouse.Name,
                            Subtitle = $"{warehouse.District}, {warehouse.State} | Capacity: {warehouse.DailyCapacity} MT",
                            Description = warehouse.Address ?? "",
                            Url = $"/Admin/Warehouse/{warehouse.Id}",
                            Status = warehouse.IsActive ? "Active" : "Inactive",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Add catalog products
                if (catalogResult.IsValidResponse && catalogResult.Documents.Any())
                {
                    foreach (var product in catalogResult.Documents)
                    {
                        results.Add(new UniversalSearchResult
                        {
                            Type = "catalog",
                            Id = product.Id,
                            Title = product.Name,
                            Subtitle = $"Category: {product.Category} | {product.UnitOfMeasure}",
                            Description = product.Description?.Length > 100 ? product.Description.Substring(0, 100) : product.Description,
                            Url = $"/Admin/Catalog",
                            Status = product.IsActive ? "Active" : "Inactive",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Add users
                if (userResult.IsValidResponse && userResult.Documents.Any())
                {
                    foreach (var user in userResult.Documents)
                    {
                        string url = "";
                        if (user.Role == "farmer")
                            url = $"/Admin/FarmerManagment?userId={user.Id}";
                        else if (user.Role == "vendor")
                            url = $"/Admin/Vendor?userId={user.Id}";
                        else if (user.Role == "field_officer")
                            url = $"/Admin/FieldOfficers?userId={user.Id}";
                        else
                            url = $"/Admin/UserDetails/{user.Id}";

                        results.Add(new UniversalSearchResult
                        {
                            Type = "user",
                            Id = user.Id,
                            Title = user.FullName ?? user.BusinessName ?? user.Email,
                            Subtitle = $"{user.Role} | {user.Email}",
                            Description = $"Phone: {user.Phone ?? "N/A"}",
                            Url = url,
                            Status = user.IsActive ? "Active" : "Inactive",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // // Add crop listings
                // if (cropResult.IsValidResponse && cropResult.Documents.Any())
                // {
                //     foreach (var crop in cropResult.Documents)
                //     {
                //         results.Add(new UniversalSearchResult
                //         {
                //             Type = "crop",
                //             Id = crop.Id,
                //             Title = crop.CropName,
                //             Subtitle = $"Farmer: {crop.FarmerName} | {crop.QuantityAvailable} {crop.Unit}",
                //             Description = $"Variety: {crop.Variety} | Location: {crop.FarmState}",
                //             Url = $"/Farmer/Listing/{crop.Id}",
                //             Status = crop.Status,
                //             CreatedAt = DateTime.UtcNow
                //         });
                //     }
                // }

                // Add orders
                if (orderResult.IsValidResponse && orderResult.Documents.Any())
                {
                    foreach (var order in orderResult.Documents)
                    {
                        results.Add(new UniversalSearchResult
                        {
                            Type = "order",
                            Id = order.Id,
                            Title = $"Order #{order.Id}",
                            Subtitle = $"Vendor: {order.VendorBusinessName} | ₹{order.TotalAmount}",
                            Description = $"Status: {order.Status}",
                            Url = $"/Vendor/OrderDetails/{order.Id}",
                            Status = order.Status,
                            CreatedAt = order.OrderedAt
                        });
                    }
                }

                Console.WriteLine($"Universal search found {results.Count} results for query: {query}");
                _logger.LogInformation($"Universal search found {results.Count} results for query: {query}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Universal search error: {ex.Message}");
                _logger.LogError(ex, "Error performing universal search");
            }

            return results;
        }

        // ============== INDEXING METHODS ==============

        public async Task<bool> IndexCatalogProductAsync(CatalogProductDocument product)
        {
            try
            {
                var response = await _client.IndexAsync(product, idx => idx
                    .Index("catalog_products")
                    .Id(product.Id)
                );

                if (response.IsValidResponse)
                {
                    _logger.LogInformation($"Indexed catalog product {product.Id}: {product.Name}");
                    return true;
                }

                _logger.LogError($"Failed to index catalog product {product.Id}: {response.DebugInformation}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error indexing catalog product {product.Id}");
                return false;
            }
        }

        public async Task<bool> IndexOrderAsync(OrderDocument order)
        {
            try
            {
                var response = await _client.IndexAsync(order, idx => idx
                    .Index("orders")
                    .Id(order.Id)
                );

                if (response.IsValidResponse)
                {
                    _logger.LogInformation($"Indexed order {order.Id} for vendor {order.VendorId}");
                    return true;
                }

                _logger.LogError($"Failed to index order {order.Id}: {response.DebugInformation}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error indexing order {order.Id}");
                return false;
            }
        }

        public async Task<bool> DeleteDocumentAsync<T>(string indexName, int id)
        {
            try
            {
                var response = await _client.DeleteAsync(indexName, id.ToString());

                if (response.IsValidResponse)
                {
                    _logger.LogInformation($"Deleted document {id} from {indexName}");
                    return true;
                }

                _logger.LogError($"Failed to delete document {id} from {indexName}: {response.DebugInformation}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting document {id} from {indexName}");
                return false;
            }
        }

        // ============== RE-INDEX METHODS ==============

        private async Task<int> ReindexWarehousesAsync()
        {
            int indexed = 0;
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT c_id, c_name, c_address, c_state, c_district, c_daily_capacity, c_is_active FROM t_warehouses";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);

                Console.WriteLine($"Indexing warehouse: ID={id}, Name={name}");

                var doc = new
                {
                    id = id,
                    name = name,
                    address = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    state = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    district = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    dailyCapacity = reader.GetInt32(5),
                    isActive = reader.GetBoolean(6),
                    documentType = "warehouse",
                    indexedAt = DateTime.UtcNow
                };

                var response = await _client.IndexAsync(doc, idx => idx
                    .Index("warehouses")
                    .Id(id)
                );

                if (response.IsValidResponse)
                {
                    indexed++;
                    Console.WriteLine($"Successfully indexed warehouse: {name}");
                }
                else
                {
                    Console.WriteLine($"Failed to index warehouse {id}: {response.DebugInformation}");
                }
            }

            return indexed;
        }

        public async Task<ReindexResult> ReindexAllAsync()
        {
            var result = new ReindexResult();

            try
            {
                _logger.LogInformation("Starting full re-index...");

                result.CatalogProducts = await ReindexCatalogProductsAsync();
                result.CropListings = await ReindexCropListingsAsync();
                result.Orders = await ReindexOrdersAsync();
                result.Users = await ReindexUsersAsync();
                result.QCRecords = await ReindexQCRecordsAsync();
                result.Warehouses = await ReindexWarehousesAsync();
                result.VendorCatalog = await ReindexVendorCatalogAsync();

                result.Success = true;
                result.Message = $"Re-index completed. Indexed: {result.TotalIndexed} records";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Re-index failed: {ex.Message}";
                _logger.LogError(ex, "Re-index failed");
            }

            return result;
        }

        private async Task<int> ReindexCatalogProductsAsync()
        {
            int indexed = 0;
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT c_id, c_name, c_category, c_unit_of_measure, c_description, c_image_url, c_is_active FROM t_catalog_products";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var doc = new
                {
                    id = reader.GetInt32(0),
                    name = reader.GetString(1),
                    category = reader.IsDBNull(2) ? null : reader.GetString(2),
                    unitOfMeasure = reader.GetString(3),
                    description = reader.IsDBNull(4) ? null : reader.GetString(4),
                    imageUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                    isActive = reader.GetBoolean(6),
                    documentType = "catalog_product",
                    indexedAt = DateTime.UtcNow
                };

                var response = await _client.IndexAsync(doc, idx => idx.Index("catalog_products").Id(doc.id));
                if (response.IsValidResponse) indexed++;
            }

            return indexed;
        }

        private async Task<int> ReindexCropListingsAsync()
        {
            int indexed = 0;
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT fcl.c_id, fcl.c_farmer_id, COALESCE(fp.c_full_name, ''), fcl.c_catalog_product_id,
                       COALESCE(cp.c_name, ''), fcl.c_quantity_available, fcl.c_unit, COALESCE(fcl.c_variety, ''),
                       fcl.c_asking_price, fcl.c_harvest_date, COALESCE(fcl.c_farm_address, ''),
                       COALESCE(fcl.c_farm_state, ''), COALESCE(fcl.c_farm_district, ''), fcl.c_status
                FROM t_farmer_crop_listings fcl
                LEFT JOIN t_farmer_profiles fp ON fcl.c_farmer_id = fp.c_id
                LEFT JOIN t_catalog_products cp ON fcl.c_catalog_product_id = cp.c_id";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var doc = new
                {
                    id = reader.GetInt32(0),
                    farmerId = reader.GetInt32(1),
                    farmerName = reader.GetString(2),
                    catalogProductId = reader.GetInt32(3),
                    cropName = reader.GetString(4),
                    quantityAvailable = reader.GetDecimal(5),
                    unit = reader.GetString(6),
                    variety = reader.GetString(7),
                    askingPrice = reader.GetDecimal(8),
                    harvestDate = reader.GetDateTime(9),
                    farmAddress = reader.GetString(10),
                    farmState = reader.GetString(11),
                    farmDistrict = reader.GetString(12),
                    status = reader.GetString(13),
                    documentType = "crop_listing",
                    indexedAt = DateTime.UtcNow
                };

                var response = await _client.IndexAsync(doc, idx => idx.Index("crop_listings").Id(doc.id));
                if (response.IsValidResponse) indexed++;
            }

            return indexed;
        }

        private async Task<int> ReindexOrdersAsync()
        {
            int indexed = 0;
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT vo.c_id, vo.c_vendor_id, COALESCE(vp.c_business_name, ''), vo.c_status, 
                       vo.c_total_amount, vo.c_ordered_at
                FROM t_vendor_orders vo
                LEFT JOIN t_vendor_profiles vp ON vo.c_vendor_id = vp.c_id";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var doc = new
                {
                    id = reader.GetInt32(0),
                    vendorId = reader.GetInt32(1),
                    vendorBusinessName = reader.GetString(2),
                    status = reader.GetString(3),
                    totalAmount = reader.GetDecimal(4),
                    orderedAt = reader.GetDateTime(5),
                    documentType = "order",
                    indexedAt = DateTime.UtcNow
                };

                var response = await _client.IndexAsync(doc, idx => idx.Index("orders").Id(doc.id));
                if (response.IsValidResponse) indexed++;
            }

            return indexed;
        }

        private async Task<int> ReindexUsersAsync()
        {
            int indexed = 0;
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT u.c_id, u.c_email, u.c_role, u.c_is_active,
                       COALESCE(fp.c_full_name, vp.c_business_name, fop.c_full_name, '')
                FROM t_users u
                LEFT JOIN t_farmer_profiles fp ON u.c_id = fp.c_user_id
                LEFT JOIN t_vendor_profiles vp ON u.c_id = vp.c_user_id
                LEFT JOIN t_field_officer_profiles fop ON u.c_id = fop.c_user_id
                WHERE u.c_role IN ('farmer', 'vendor', 'field_officer', 'admin')";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var doc = new
                {
                    id = reader.GetInt32(0),
                    email = reader.GetString(1),
                    role = reader.GetString(2),
                    isActive = reader.GetBoolean(3),
                    fullName = reader.GetString(4),
                    documentType = "user",
                    indexedAt = DateTime.UtcNow
                };

                var response = await _client.IndexAsync(doc, idx => idx.Index("users").Id(doc.id));
                if (response.IsValidResponse) indexed++;
            }

            return indexed;
        }

        private async Task<int> ReindexQCRecordsAsync()
        {
            int indexed = 0;
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT qif.c_id, qif.c_procurement_request_id, qif.c_fo_id, COALESCE(fop.c_full_name, ''),
                       qif.c_grade, qif.c_accepted_quantity, qif.c_passed, qif.c_submitted_at
                FROM t_quality_inspection_forms qif
                LEFT JOIN t_field_officer_profiles fop ON qif.c_fo_id = fop.c_id";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var doc = new
                {
                    id = reader.GetInt32(0),
                    procurementRequestId = reader.GetInt32(1),
                    foId = reader.GetInt32(2),
                    foName = reader.GetString(3),
                    grade = reader.IsDBNull(4) ? null : reader.GetString(4),
                    acceptedQuantity = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                    passed = reader.GetBoolean(6),
                    submittedAt = reader.GetDateTime(7),
                    documentType = "qc_record",
                    indexedAt = DateTime.UtcNow
                };

                var response = await _client.IndexAsync(doc, idx => idx.Index("qc_records").Id(doc.id));
                if (response.IsValidResponse) indexed++;
            }

            return indexed;
        }

        // ============== OTHER SEARCH METHODS ==============

        public async Task<SearchResponseModel<CatalogSearchResult>> SearchCatalogForMVCAsync(SearchRequestModel request)
        {
            var response = new SearchResponseModel<CatalogSearchResult>();

            try
            {
                var searchRequest = new SearchRequest("catalog_products")
                {
                    Size = 100,
                    Query = new MatchAllQuery()
                };

                var result = await _client.SearchAsync<CatalogProductDocument>(searchRequest);

                if (result.IsValidResponse && result.Documents.Any())
                {
                    response.Results = result.Documents.Select(p => new CatalogSearchResult
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Category = p.Category,
                        UnitOfMeasure = p.UnitOfMeasure,
                        Description = p.Description,
                        ImageUrl = p.ImageUrl,
                        IsActive = p.IsActive
                    }).ToList();

                    response.TotalCount = result.Total;
                    response.Page = request.Page;
                    response.PageSize = request.PageSize;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching catalog");
            }

            return response;
        }

        public async Task<SearchResponseModel<CropSearchResult>> SearchCropsForMVCAsync(SearchRequestModel request)
        {
            var response = new SearchResponseModel<CropSearchResult>();

            try
            {
                int from = (request.Page - 1) * request.PageSize;

                var searchRequest = new SearchRequest("crop_listings")
                {
                    From = from,
                    Size = request.PageSize,
                    Query = BuildCropSearchQuery(request.Query, request.FarmerId, request.Status, request.State)
                };

                var result = await _client.SearchAsync<object>(searchRequest);

                Console.WriteLine($"=== CROP SEARCH DEBUG ===");
                Console.WriteLine($"Query: {request.Query}");
                Console.WriteLine($"FarmerId: {request.FarmerId}");
                Console.WriteLine($"Status: {request.Status}");
                Console.WriteLine($"Total Records: {result.Total}");

                if (result.IsValidResponse && result.Documents.Any())
                {
                    foreach (var doc in result.Documents)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(doc);
                        using var document = System.Text.Json.JsonDocument.Parse(json);
                        var root = document.RootElement;

                        var cropResult = new CropSearchResult
                        {
                            Id = root.TryGetProperty("id", out var id) ? id.GetInt32() :
                                 (root.TryGetProperty("Id", out var id2) ? id2.GetInt32() : 0),
                            FarmerId = root.TryGetProperty("farmerId", out var fid) ? fid.GetInt32() :
                                      (root.TryGetProperty("FarmerId", out var fid2) ? fid2.GetInt32() : 0),
                            FarmerName = root.TryGetProperty("farmerName", out var fname) ? fname.GetString() :
                                        (root.TryGetProperty("FarmerName", out var fname2) ? fname2.GetString() : null),
                            CropName = root.TryGetProperty("cropName", out var cname) ? cname.GetString() :
                                      (root.TryGetProperty("CropName", out var cname2) ? cname2.GetString() : null),
                            QuantityAvailable = root.TryGetProperty("quantityAvailable", out var qty) ? qty.GetDecimal() :
                                               (root.TryGetProperty("QuantityAvailable", out var qty2) ? qty2.GetDecimal() : 0),
                            Unit = root.TryGetProperty("unit", out var unit) ? unit.GetString() :
                                  (root.TryGetProperty("Unit", out var unit2) ? unit2.GetString() : "kg"),
                            Variety = root.TryGetProperty("variety", out var varName) ? varName.GetString() :
                                     (root.TryGetProperty("Variety", out var varName2) ? varName2.GetString() : null),
                            AskingPrice = root.TryGetProperty("askingPrice", out var price) ? price.GetDecimal() :
                                         (root.TryGetProperty("AskingPrice", out var price2) ? price2.GetDecimal() : 0),
                            Status = root.TryGetProperty("status", out var stat) ? stat.GetString() :
                                    (root.TryGetProperty("Status", out var stat2) ? stat2.GetString() : null)
                        };

                        response.Results.Add(cropResult);
                    }

                    response.TotalCount = result.Total;
                    response.Page = request.Page;
                    response.PageSize = request.PageSize;
                }

                Console.WriteLine($"Returning {response.Results.Count} crop results");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching crops: {ex.Message}");
                _logger.LogError(ex, "Error searching crops");
            }

            return response;
        }

        public async Task<SearchResponseModel<OrderSearchResult>> SearchOrdersForMVCAsync(SearchRequestModel request)
        {
            var response = new SearchResponseModel<OrderSearchResult>();

            try
            {
                int from = (request.Page - 1) * request.PageSize;

                var searchRequest = new SearchRequest("orders")
                {
                    From = from,
                    Size = request.PageSize,
                    Query = BuildOrderSearchQuery(request.Query, request.VendorId, request.Status)
                };

                var result = await _client.SearchAsync<object>(searchRequest);

                if (result.IsValidResponse && result.Documents.Any())
                {
                    response.Results = MapToOrderResults(result.Documents);
                    response.TotalCount = result.Total;
                    response.Page = request.Page;
                    response.PageSize = request.PageSize;
                    response.ProcessingTimeMs = result.Took;
                    response.Query = request.Query;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching orders");
            }

            return response;
        }

        public async Task<SearchResponseModel<UserSearchResult>> SearchUsersForMVCAsync(SearchRequestModel request)
        {
            var response = new SearchResponseModel<UserSearchResult>();

            try
            {
                int from = (request.Page - 1) * request.PageSize;

                var searchRequest = new SearchRequest("users")
                {
                    From = from,
                    Size = request.PageSize,
                    Query = BuildUserSearchQuery(request.Query, request.Role, request.IsActive)
                };

                var result = await _client.SearchAsync<object>(searchRequest);

                if (result.IsValidResponse && result.Documents.Any())
                {
                    response.Results = MapToUserResults(result.Documents);
                    response.TotalCount = result.Total;
                    response.Page = request.Page;
                    response.PageSize = request.PageSize;
                    response.ProcessingTimeMs = result.Took;
                    response.Query = request.Query;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users");
            }

            return response;
        }

        public async Task<SearchResponseModel<QCSearchResult>> SearchQCRecordsForMVCAsync(SearchRequestModel request)
        {
            var response = new SearchResponseModel<QCSearchResult>();

            try
            {
                int from = (request.Page - 1) * request.PageSize;

                // Build query with multiple field search
                Query query;

                if (!string.IsNullOrWhiteSpace(request.Query))
                {
                    // Multi-match search across multiple fields with fuzzy support
                    query = new BoolQuery
                    {
                        Should = new List<Query>
                        {
                            new WildcardQuery(new Field("FarmerName"))
                            {
                                Value = $"*{request.Query.ToLower()}*"
                            },
                            new WildcardQuery(new Field("CropName"))
                            {
                                Value = $"*{request.Query.ToLower()}*"
                            },
                            new MultiMatchQuery
                            {
                                Query = request.Query,
                                Fields = new[] { "FarmerName^3", "CropName^2", "Location", "Grade" },
                                Fuzziness = new Fuzziness("AUTO"),
                                Operator = Operator.Or
                            }
                        },
                        MinimumShouldMatch = 1
                    };
                }
                else
                {
                    query = new MatchAllQuery();
                }

                var mustWrapper = new BoolQuery
                {
                    Must = new List<Query>
                    {
                        new TermQuery { Field = "FoId", Value = request.FoId ?? 0 },
                        query  // upar wali search query
                    }
                };

                var searchRequest = new SearchRequest("qc_records")
                {
                    From = from,
                    Size = request.PageSize,
                    Query = request.FoId.HasValue ? mustWrapper : query  // foId hai toh filter karo
                };

                var result = await _client.SearchAsync<object>(searchRequest);

                Console.WriteLine($"=== SEARCH DEBUG ===");
                Console.WriteLine($"Query: {request.Query}");
                Console.WriteLine($"IsValid: {result.IsValidResponse}");
                Console.WriteLine($"Total Records: {result.Total}");

                if (result.IsValidResponse && result.Documents.Any())
                {
                    foreach (var doc in result.Documents)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(doc);
                        using var document = System.Text.Json.JsonDocument.Parse(json);
                        var root = document.RootElement;

                        var qcResult = new QCSearchResult
                        {
                            Id = root.TryGetProperty("Id", out var id) ? id.GetInt32() : 0,
                            ProcurementRequestId = root.TryGetProperty("ProcurementRequestId", out var prId) ? prId.GetInt32() : 0,
                            FoId = root.TryGetProperty("FoId", out var foId) ? foId.GetInt32() : 0,
                            FoName = root.TryGetProperty("FoName", out var foName) ? foName.GetString() : null,
                            Grade = root.TryGetProperty("Grade", out var grade) ? grade.GetString() : null,
                            AcceptedQuantity = root.TryGetProperty("AcceptedQuantity", out var accQty) ? accQty.GetDecimal() : 0,
                            Passed = root.TryGetProperty("Passed", out var passed) ? passed.GetBoolean() : false,
                            SubmittedAt = root.TryGetProperty("SubmittedAt", out var subAt) ? subAt.GetDateTime() : DateTime.MinValue,

                            FarmerName = root.TryGetProperty("FarmerName", out var farmerName) ? farmerName.GetString() : null,
                            CropType = root.TryGetProperty("CropName", out var cropName) ? cropName.GetString() : null,
                            Quantity = root.TryGetProperty("AcceptedQuantity", out var qty) ? qty.GetDecimal() : 0,
                            Location = root.TryGetProperty("Location", out var loc) ? loc.GetString() : null,
                            Status = root.TryGetProperty("Status", out var status) ? status.GetString() : null
                        };

                        response.Results.Add(qcResult);
                    }

                    response.TotalCount = result.Total;
                    response.Page = request.Page;
                    response.PageSize = request.PageSize;
                }

                Console.WriteLine($"Returning {response.Results.Count} results");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching QC records: {ex.Message}");
                _logger.LogError(ex, "Error searching QC records");
            }

            return response;
        }

        // ============== HEALTH CHECK ==============

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var response = await _client.Cluster.HealthAsync();
                return response.IsValidResponse;
            }
            catch
            {
                return false;
            }
        }

        // ============== PRIVATE HELPER METHODS ==============

        private Query BuildSearchQuery(string query, string category, bool? isActive)
        {
            var queries = new List<Query>();

            if (!string.IsNullOrWhiteSpace(query))
            {
                queries.Add(new MultiMatchQuery
                {
                    Query = query,
                    Fields = new[] { "name^3", "category^2", "description" },
                    Fuzziness = new Fuzziness("AUTO")
                });
            }

            if (!string.IsNullOrEmpty(category))
            {
                queries.Add(new TermQuery
                {
                    Field = "category",
                    Value = category
                });
            }

            if (isActive.HasValue)
            {
                queries.Add(new TermQuery
                {
                    Field = "isActive",
                    Value = isActive.Value
                });
            }

            if (queries.Count == 0)
                return new MatchAllQuery();

            return new BoolQuery { Must = queries };
        }

        private Query BuildCropSearchQuery(string query, int? farmerId, string status, string state)
        {
            var mustQueries = new List<Query>();

            // 1. Search query - strict matching
            if (!string.IsNullOrWhiteSpace(query))
            {
                var textSearch = new BoolQuery
                {
                    Should = new List<Query>
                    {
                        new WildcardQuery(new Field("cropName"))
                        {
                            Value = $"*{query.ToLower()}*"
                        },
                        new MultiMatchQuery
                        {
                            Query = query,
                            Fields = new[] { "cropName^3", "variety^2", "farmerName" },
                            Fuzziness = new Fuzziness("AUTO"),
                            Operator = Operator.Or
                        }
                    },
                    MinimumShouldMatch = 1
                };
                mustQueries.Add(textSearch);
            }

            // 2. Farmer filter
            if (farmerId.HasValue)
            {
                mustQueries.Add(new TermQuery
                {
                    Field = "farmerId",
                    Value = farmerId.Value
                });
            }

            // 3. Status filter
            if (!string.IsNullOrEmpty(status))
            {
                mustQueries.Add(new TermQuery
                {
                    Field = "status",
                    Value = status.ToLower()
                });
            }

            // 4. State filter
            if (!string.IsNullOrEmpty(state))
            {
                mustQueries.Add(new TermQuery
                {
                    Field = "farmState",
                    Value = state
                });
            }

            if (mustQueries.Count == 0)
                return new MatchAllQuery();

            if (mustQueries.Count == 1)
                return mustQueries[0];

            return new BoolQuery { Must = mustQueries };
        }

        private Query BuildOrderSearchQuery(string query, int? vendorId, string status)
        {
            var queries = new List<Query>();

            if (!string.IsNullOrWhiteSpace(query))
            {
                queries.Add(new MultiMatchQuery
                {
                    Query = query,
                    Fields = new[] { "vendorBusinessName^2", "status" },
                    Fuzziness = new Fuzziness("AUTO")
                });
            }

            if (vendorId.HasValue)
            {
                queries.Add(new TermQuery
                {
                    Field = "vendorId",
                    Value = vendorId.Value
                });
            }

            if (!string.IsNullOrEmpty(status))
            {
                queries.Add(new TermQuery
                {
                    Field = "status",
                    Value = status
                });
            }

            if (queries.Count == 0)
                return new MatchAllQuery();

            return new BoolQuery { Must = queries };
        }

        private Query BuildUserSearchQuery(string query, string role, bool? isActive)
        {
            var queries = new List<Query>();

            if (!string.IsNullOrWhiteSpace(query))
            {
                queries.Add(new MultiMatchQuery
                {
                    Query = query,
                    Fields = new[] { "email^3", "fullName^2", "businessName^2", "phone" },
                    Fuzziness = new Fuzziness("AUTO")
                });
            }

            if (!string.IsNullOrEmpty(role))
            {
                queries.Add(new TermQuery
                {
                    Field = "role",
                    Value = role
                });
            }

            if (isActive.HasValue)
            {
                queries.Add(new TermQuery
                {
                    Field = "isActive",
                    Value = isActive.Value
                });
            }

            if (queries.Count == 0)
                return new MatchAllQuery();

            return new BoolQuery { Must = queries };
        }

        private Query BuildQCSearchQuery(string query, int? foId, bool? passed, string grade)
        {
            var queries = new List<Query>();

            if (!string.IsNullOrWhiteSpace(query))
            {
                // Use AND operator instead of OR for more precise matching
                queries.Add(new MultiMatchQuery
                {
                    Query = query,
                    Fields = new[] { "farmerName^3", "cropName^2", "location", "grade" },
                    Fuzziness = new Fuzziness("AUTO"),
                    Operator = Operator.And,  // Changed from Or to And
                    MinimumShouldMatch = "100%"  // Require all terms to match
                });
            }

            if (foId.HasValue)
            {
                queries.Add(new TermQuery
                {
                    Field = "foId",
                    Value = foId.Value
                });
            }

            if (passed.HasValue)
            {
                queries.Add(new TermQuery
                {
                    Field = "passed",
                    Value = passed.Value
                });
            }

            if (!string.IsNullOrEmpty(grade))
            {
                queries.Add(new TermQuery
                {
                    Field = "grade",
                    Value = grade
                });
            }

            if (queries.Count == 0)
                return new MatchAllQuery();

            return new BoolQuery { Must = queries };
        }

        private List<CatalogSearchResult> MapToCatalogResults(IReadOnlyCollection<object> documents)
        {
            var results = new List<CatalogSearchResult>();

            foreach (var doc in documents)
            {
                var dict = doc as IDictionary<string, object>;
                if (dict != null)
                {
                    results.Add(new CatalogSearchResult
                    {
                        Id = Convert.ToInt32(dict["id"]),
                        Name = dict["name"]?.ToString(),
                        Category = dict["category"]?.ToString(),
                        UnitOfMeasure = dict["unitOfMeasure"]?.ToString(),
                        Description = dict["description"]?.ToString(),
                        ImageUrl = dict["imageUrl"]?.ToString(),
                        IsActive = Convert.ToBoolean(dict["isActive"])
                    });
                }
            }

            return results;
        }

        private List<CropSearchResult> MapToCropResults(IReadOnlyCollection<object> documents)
        {
            var results = new List<CropSearchResult>();

            foreach (var doc in documents)
            {
                var dict = doc as IDictionary<string, object>;
                if (dict != null)
                {
                    // Debug: Print all keys
                    Console.WriteLine("Document keys: " + string.Join(", ", dict.Keys));

                    var result = new CropSearchResult
                    {
                        // Try both PascalCase and lowercase field names
                        Id = dict.ContainsKey("Id") ? Convert.ToInt32(dict["Id"]) :
                             (dict.ContainsKey("id") ? Convert.ToInt32(dict["id"]) : 0),

                        FarmerId = dict.ContainsKey("FarmerId") ? Convert.ToInt32(dict["FarmerId"]) :
                                  (dict.ContainsKey("farmerId") ? Convert.ToInt32(dict["farmerId"]) : 0),

                        FarmerName = dict.ContainsKey("FarmerName") ? dict["FarmerName"]?.ToString() :
                                    (dict.ContainsKey("farmerName") ? dict["farmerName"]?.ToString() : null),

                        CropName = dict.ContainsKey("CropName") ? dict["CropName"]?.ToString() :
                                  (dict.ContainsKey("cropName") ? dict["cropName"]?.ToString() : null),

                        QuantityAvailable = dict.ContainsKey("QuantityAvailable") ? Convert.ToDecimal(dict["QuantityAvailable"]) :
                                           (dict.ContainsKey("quantityAvailable") ? Convert.ToDecimal(dict["quantityAvailable"]) : 0),

                        Unit = dict.ContainsKey("Unit") ? dict["Unit"]?.ToString() :
                              (dict.ContainsKey("unit") ? dict["unit"]?.ToString() : "kg"),

                        Variety = dict.ContainsKey("Variety") ? dict["Variety"]?.ToString() :
                                 (dict.ContainsKey("variety") ? dict["variety"]?.ToString() : null),

                        AskingPrice = dict.ContainsKey("AskingPrice") ? Convert.ToDecimal(dict["AskingPrice"]) :
                                     (dict.ContainsKey("askingPrice") ? Convert.ToDecimal(dict["askingPrice"]) : 0),

                        Status = dict.ContainsKey("Status") ? dict["Status"]?.ToString() :
                                (dict.ContainsKey("status") ? dict["status"]?.ToString() : null)
                    };

                    results.Add(result);
                }
            }

            Console.WriteLine($"Mapped {results.Count} crop results");
            return results;
        }

        private List<OrderSearchResult> MapToOrderResults(IReadOnlyCollection<object> documents)
        {
            var results = new List<OrderSearchResult>();

            foreach (var doc in documents)
            {
                var dict = doc as IDictionary<string, object>;
                if (dict != null)
                {
                    results.Add(new OrderSearchResult
                    {
                        Id = Convert.ToInt32(dict["id"]),
                        VendorId = Convert.ToInt32(dict["vendorId"]),
                        VendorBusinessName = dict["vendorBusinessName"]?.ToString(),
                        Status = dict["status"]?.ToString(),
                        TotalAmount = Convert.ToDecimal(dict["totalAmount"]),
                        OrderedAt = Convert.ToDateTime(dict["orderedAt"])
                    });
                }
            }

            return results;
        }

        private List<UserSearchResult> MapToUserResults(IReadOnlyCollection<object> documents)
        {
            var results = new List<UserSearchResult>();

            foreach (var doc in documents)
            {
                var dict = doc as IDictionary<string, object>;
                if (dict != null)
                {
                    results.Add(new UserSearchResult
                    {
                        Id = Convert.ToInt32(dict["id"]),
                        Email = dict["email"]?.ToString(),
                        Role = dict["role"]?.ToString(),
                        FullName = dict["fullName"]?.ToString(),
                        IsActive = Convert.ToBoolean(dict["isActive"])
                    });
                }
            }

            return results;
        }

        private List<QCSearchResult> MapToQCResults(IReadOnlyCollection<object> documents)
        {
            var results = new List<QCSearchResult>();

            foreach (var doc in documents)
            {
                var dict = doc as IDictionary<string, object>;
                if (dict != null)
                {
                    var result = new QCSearchResult
                    {
                        // Map exactly as they appear in Elasticsearch
                        Id = Convert.ToInt32(dict["Id"]),
                        ProcurementRequestId = Convert.ToInt32(dict["ProcurementRequestId"]),
                        FoId = Convert.ToInt32(dict["FoId"]),
                        FoName = dict["FoName"]?.ToString(),
                        Grade = dict["Grade"]?.ToString(),
                        AcceptedQuantity = Convert.ToDecimal(dict["AcceptedQuantity"]),
                        Passed = Convert.ToBoolean(dict["Passed"]),
                        SubmittedAt = Convert.ToDateTime(dict["SubmittedAt"]),

                        // UI Display Fields - using actual field names from Elasticsearch
                        FarmerName = dict["FarmerName"]?.ToString(),
                        CropType = dict["CropName"]?.ToString(),
                        Quantity = Convert.ToDecimal(dict["AcceptedQuantity"]),
                        Location = dict["Location"]?.ToString(),
                        Status = dict["Status"]?.ToString()
                    };

                    results.Add(result);
                }
            }

            return results;
        }
        // ============== INDEX SINGLE QC RECORD (REAL-TIME) ==============

        public async Task<bool> IndexQCRecordAsync(int inspectionId)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                var sql = @"
            SELECT 
                qif.c_id, 
                qif.c_procurement_request_id, 
                qif.c_fo_id, 
                COALESCE(fop.c_full_name, '') as fo_name,
                qif.c_grade, 
                qif.c_accepted_quantity, 
                qif.c_rejected_quantity,
                qif.c_passed, 
                qif.c_submitted_at,
                pr.c_farmer_id, 
                COALESCE(fp.c_full_name, '') as farmer_name,
                qif.c_fo_assessed_price
            FROM t_quality_inspection_forms qif
            LEFT JOIN t_field_officer_profiles fop ON qif.c_fo_id = fop.c_id
            LEFT JOIN t_procurement_requests pr ON qif.c_procurement_request_id = pr.c_id
            LEFT JOIN t_farmer_profiles fp ON pr.c_farmer_id = fp.c_id
            WHERE qif.c_id = @inspectionId";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@inspectionId", inspectionId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var doc = new Dictionary<string, object>
                    {
                        ["Id"] = reader.GetInt32(0),
                        ["ProcurementRequestId"] = reader.GetInt32(1),
                        ["FoId"] = reader.GetInt32(2),
                        ["FoName"] = reader.GetString(3),
                        ["Grade"] = reader.IsDBNull(4) ? null : reader.GetString(4),
                        ["AcceptedQuantity"] = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                        ["RejectedQuantity"] = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                        ["Passed"] = reader.GetBoolean(7),
                        ["SubmittedAt"] = reader.GetDateTime(8),
                        ["FarmerId"] = reader.GetInt32(9),
                        ["FarmerName"] = reader.GetString(10),
                        ["FoAssessedPrice"] = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                        ["DocumentType"] = "qc_record",
                        ["IndexedAt"] = DateTime.UtcNow
                    };

                    var response = await _client.IndexAsync(doc, idx => idx
                        .Index("qc_records")
                        .Id(reader.GetInt32(0).ToString())
                    );

                    if (response.IsValidResponse)
                    {
                        _logger.LogInformation($"✅ Indexed QC record {inspectionId} to Elasticsearch");
                        return true;
                    }
                    else
                    {
                        _logger.LogError($"❌ Failed to index QC record {inspectionId}: {response.DebugInformation}");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error indexing QC record {inspectionId}");
                return false;
            }
        }


        // ========== GET QC RECORDS COUNT BY FO ID ==========

        public async Task<long> GetQCRecordsCountByFoIdAsync(int foId)
        {
            try
            {
                // Fix 1: Use Indices instead of Index
                var allResponse = await _client.CountAsync<object>(c => c
                    .Indices("qc_records")  // ✅ Fixed
                    .Query(q => q.MatchAll())
                );
                Console.WriteLine($"Total records in qc_records index: {allResponse.Count}");

                // Fix 2: Use Indices() method
                var searchResponse = await _client.SearchAsync<object>(s => s
                    .Indices("qc_records")  // ✅ Fixed - using Indices() method
                    .Size(0)
                    .Query(q => q
                        .Term(t => t.Field("foId").Value(foId))
                    )
                );

                Console.WriteLine($"Records with foId={foId}: {searchResponse.Total}");

                if (searchResponse.Total == 0)
                {
                    var searchResponse2 = await _client.SearchAsync<object>(s => s
                        .Indices("qc_records")  // ✅ Fixed
                        .Size(0)
                        .Query(q => q
                            .Term(t => t.Field("FoId").Value(foId))
                        )
                    );
                    Console.WriteLine($"Records with FoId={foId}: {searchResponse2.Total}");
                    return searchResponse2.Total;
                }

                return searchResponse.Total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting QC records count from Elasticsearch");
                return 0;
            }
        }
        public async Task<object> GetFirstDocumentAsync()
        {
            try
            {
                // Use the same working approach as SearchQCRecordsForMVCAsync
                var searchRequest = new SearchRequest("qc_records")
                {
                    Size = 1,
                    Query = new MatchAllQuery()
                };

                var response = await _client.SearchAsync<object>(searchRequest);

                Console.WriteLine($"=== GetFirstDocumentAsync Debug ===");
                Console.WriteLine($"IsValid: {response.IsValidResponse}");
                Console.WriteLine($"Total: {response.Total}");
                Console.WriteLine($"Documents Count: {response.Documents.Count}");

                if (response.IsValidResponse && response.Documents.Any())
                {
                    var doc = response.Documents.First();

                    // Try to convert to dictionary
                    if (doc is IDictionary<string, object> dict)
                    {
                        return new
                        {
                            success = true,
                            fieldNames = dict.Keys.ToList(),
                            sampleData = dict
                        };
                    }
                    else
                    {
                        // If not dictionary, return the raw object
                        return new
                        {
                            success = true,
                            type = doc.GetType().Name,
                            rawDocument = doc
                        };
                    }
                }

                return new { success = false, message = "No documents found in qc_records index" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetFirstDocumentAsync: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        // ========== REINDEX QC RECORDS BY FO ID ==========

        public async Task<int> ReindexQCRecordsByFoIdAsync(int foId)
        {
            int indexed = 0;
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            Console.WriteLine($"=== REINDEX START for FO {foId} ===");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            // First check how many records in DB
            var countSql = @"
        SELECT COUNT(*) 
        FROM t_quality_inspection_forms qif
        JOIN t_procurement_requests pr ON qif.c_procurement_request_id = pr.c_id
        WHERE pr.c_assigned_fo_id = @foId";

            using var countCmd = new NpgsqlCommand(countSql, conn);
            countCmd.Parameters.AddWithValue("@foId", foId);
            var dbCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            Console.WriteLine($"Records in DB for FO {foId}: {dbCount}");

            // Get data to index
            var sql = @"
    SELECT 
        qif.c_id, 
        qif.c_procurement_request_id, 
        qif.c_fo_id, 
        COALESCE(fop.c_full_name, '') as fo_name,
        qif.c_grade, 
        qif.c_accepted_quantity, 
        qif.c_rejected_quantity,
        qif.c_passed, 
        qif.c_submitted_at,
        pr.c_farmer_id, 
        COALESCE(fp.c_full_name, '') as farmer_name,
        qif.c_fo_assessed_price,
        COALESCE(cp.c_name, '') as crop_name,
        COALESCE(fp.c_district, '') as location,
        pr.c_status
    FROM t_quality_inspection_forms qif
    LEFT JOIN t_field_officer_profiles fop ON qif.c_fo_id = fop.c_id
    LEFT JOIN t_procurement_requests pr ON qif.c_procurement_request_id = pr.c_id
    LEFT JOIN t_farmer_profiles fp ON pr.c_farmer_id = fp.c_id
    LEFT JOIN t_farmer_crop_listings fcl ON pr.c_crop_listing_id = fcl.c_id
    LEFT JOIN t_catalog_products cp ON fcl.c_catalog_product_id = cp.c_id
    WHERE qif.c_fo_id = @foId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@foId", foId);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var doc = new Dictionary<string, object>
                {
                    ["Id"] = reader.GetInt32(0),
                    ["ProcurementRequestId"] = reader.GetInt32(1),
                    ["FoId"] = reader.GetInt32(2),
                    ["FoName"] = reader.GetString(3),
                    ["Grade"] = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ["AcceptedQuantity"] = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                    ["RejectedQuantity"] = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                    ["Passed"] = reader.GetBoolean(7),
                    ["SubmittedAt"] = reader.GetDateTime(8),
                    ["FarmerId"] = reader.GetInt32(9),
                    ["FarmerName"] = reader.GetString(10),      // For UI FARMER column
                    ["FoAssessedPrice"] = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                    ["CropName"] = reader.GetString(12),         // For UI CROP column
                    ["Location"] = reader.GetString(13),         // For UI LOCATION column
                    ["Status"] = reader.GetString(14),           // For UI STATUS column
                    ["DocumentType"] = "qc_record",
                    ["IndexedAt"] = DateTime.UtcNow
                };

                Console.WriteLine($"Indexing record ID: {reader.GetInt32(0)}");

                var response = await _client.IndexAsync(doc, idx => idx
                    .Index("qc_records")  // Make sure index name is correct
                    .Id(reader.GetInt32(0).ToString())
                );

                if (response.IsValidResponse)
                {
                    indexed++;
                    Console.WriteLine($"✅ Indexed record {reader.GetInt32(0)}");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to index: {response.DebugInformation}");
                }
            }

            // Verify after reindex
            var verifyCount = await GetQCRecordsCountByFoIdAsync(foId);
            Console.WriteLine($"After reindex - ES Count: {verifyCount}");

            _logger.LogInformation($"Reindexed {indexed} QC records for FO {foId}");
            return indexed;
        }
        public async Task<bool> CheckIndexExistsAsync(string indexName)
        {
            var response = await _client.Indices.ExistsAsync(indexName);
            return response.Exists;
        }

        public async Task<long> GetTotalRecordsInIndexAsync(string indexName)
        {
            var response = await _client.CountAsync<object>(c => c
                .Indices(indexName)  // ✅ Use Indices instead of Index
                .Query(q => q.MatchAll())
            );
            return response.Count;
        }

        //Vendor Module
        // ========== REINDEX VENDOR CATALOG (FROM WAREHOUSE LOTS) ==========


        public async Task<object> GetFirstVendorCatalogDocumentAsync()
        {
            try
            {
                var searchRequest = new SearchRequest("vendor_catalog")
                {
                    Size = 1,
                    Query = new MatchAllQuery()
                };

                var response = await _client.SearchAsync<object>(searchRequest);

                if (response.IsValidResponse && response.Documents.Any())
                {
                    var doc = response.Documents.First();
                    var json = System.Text.Json.JsonSerializer.Serialize(doc);
                    using var document = System.Text.Json.JsonDocument.Parse(json);
                    var root = document.RootElement;

                    return new
                    {
                        fieldNames = root.EnumerateObject().Select(p => p.Name).ToList(),
                        sampleData = doc
                    };
                }

                return new { message = "No documents found in vendor_catalog index" };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }
        public async Task<int> ReindexVendorCatalogAsync()
        {
            int indexed = 0;
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
        SELECT 
            wl.c_catalog_product_id,
            cp.c_name,
            cp.c_category,
            cp.c_unit_of_measure,
            cp.c_description,
            cp.c_image_url,
            wl.c_grade,
            AVG(qif.c_fo_assessed_price * 1.10) AS Price,
            SUM(wl.c_quantity_remaining) AS QuantityAvailable
        FROM t_warehouse_lots wl
        INNER JOIN t_catalog_products cp ON wl.c_catalog_product_id = cp.c_id
        INNER JOIN t_quality_inspection_forms qif ON wl.c_quality_inspection_id = qif.c_id
        WHERE LOWER(wl.c_status) = 'available'
          AND qif.c_passed = true
        GROUP BY 
            wl.c_catalog_product_id, cp.c_name, cp.c_category, 
            cp.c_unit_of_measure, cp.c_description, cp.c_image_url, wl.c_grade
        ORDER BY cp.c_name
    ";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var productId = reader.GetInt32(0);
                var grade = reader.IsDBNull(6) ? "nograde" : reader.GetString(6);

                // ✅ KEY FIX: "5_A", "5_B", "5_C" — alag alag ID
                var compositeId = $"{productId}_{grade}";

                var doc = new Dictionary<string, object>
                {
                    ["Id"] = productId,
                    ["Name"] = reader.GetString(1),
                    ["Category"] = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ["UnitOfMeasure"] = reader.IsDBNull(3) ? "kg" : reader.GetString(3),
                    ["Description"] = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ["ImageUrl"] = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ["Grade"] = grade,
                    ["Price"] = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7),
                    ["QuantityAvailable"] = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                    ["IsActive"] = true,
                    ["DocumentType"] = "vendor_catalog",
                    ["IndexedAt"] = DateTime.UtcNow
                };

                var response = await _client.IndexAsync(doc, idx => idx
                    .Index("vendor_catalog")
                    .Id(compositeId)  // ✅ "5_A", "5_B", "5_C"
                );

                if (response.IsValidResponse)
                {
                    indexed++;
                    Console.WriteLine($"✅ Indexed: {doc["Name"]} Grade {grade} → ID: {compositeId}");
                }
                else
                {
                    Console.WriteLine($"❌ Failed: {response.DebugInformation}");
                }
            }

            _logger.LogInformation($"Reindexed {indexed} vendor catalog products");
            return indexed;
        }

        public async Task<SearchResponseModel<CatalogSearchResult>> SearchVendorCatalogForMVCAsync(SearchRequestModel request)
        {
            var response = new SearchResponseModel<CatalogSearchResult>();

            try
            {
                int from = (request.Page - 1) * request.PageSize;

                Query query;
                if (!string.IsNullOrWhiteSpace(request.Query))
                {
                    query = new BoolQuery
                    {
                        Should = new List<Query>
                        {
                            // "ap" → "Apple" (partial prefix match)
                            new MultiMatchQuery
                            {
                                Query = request.Query,
                                Fields = new[] { "Name^3", "Category^2", "Grade" },
                                Type = TextQueryType.PhrasePrefix,
                                Operator = Operator.Or
                            },
                            // "aple" → "Apple" (typo/fuzzy match)
                            new MultiMatchQuery
                            {
                                Query = request.Query,
                                Fields = new[] { "Name^3", "Category^2", "Grade" },
                                Fuzziness = new Fuzziness("AUTO"),
                                Operator = Operator.Or
                            },
                            // "ap*" wildcard match
                            new WildcardQuery(new Field("Name"))
                            {
                                Value = $"{request.Query.ToLower()}*"
                            }
                        },
                        MinimumShouldMatch = 1
                    };
                }
                else
                {
                    query = new MatchAllQuery();
                }

                var searchRequest = new SearchRequest("vendor_catalog")
                {
                    From = from,
                    Size = request.PageSize,
                    Query = query
                };

                var result = await _client.SearchAsync<object>(searchRequest);

                Console.WriteLine($"=== SEARCH DEBUG ===");
                Console.WriteLine($"Query: {request.Query}");
                Console.WriteLine($"Total Records: {result.Total}");

                if (result.IsValidResponse && result.Documents.Any())
                {
                    foreach (var doc in result.Documents)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(doc);
                        using var document = System.Text.Json.JsonDocument.Parse(json);
                        var root = document.RootElement;

                        var catalogResult = new CatalogSearchResult
                        {
                            Id = root.TryGetProperty("Id", out var id) ? id.GetInt32() : 0,
                            Name = root.TryGetProperty("Name", out var name) ? name.GetString() : null,
                            Category = root.TryGetProperty("Category", out var cat) ? cat.GetString() : null,
                            UnitOfMeasure = root.TryGetProperty("UnitOfMeasure", out var unit) ? unit.GetString() : "kg",
                            Description = root.TryGetProperty("Description", out var desc) ? desc.GetString() : null,
                            ImageUrl = root.TryGetProperty("ImageUrl", out var img) ? img.GetString() : null,
                            IsActive = root.TryGetProperty("IsActive", out var active) ? active.GetBoolean() : true,
                            Grade = root.TryGetProperty("Grade", out var grade) ? grade.GetString() : null,
                            Price = root.TryGetProperty("Price", out var price)
                                        ? price.GetDecimal() : 0,
                            QuantityAvailable = root.TryGetProperty("QuantityAvailable", out var qty)
                                        ? qty.GetDecimal() : 0
                        };

                        response.Results.Add(catalogResult);
                    }

                    response.TotalCount = result.Total;
                    response.Page = request.Page;
                    response.PageSize = request.PageSize;
                }

                Console.WriteLine($"Returning {response.Results.Count} results");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching vendor catalog: {ex.Message}");
                _logger.LogError(ex, "Error searching vendor catalog");
            }

            return response;
        }
    }
}