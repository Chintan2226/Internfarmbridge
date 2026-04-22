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

                _logger.LogInformation("ElasticSearch indexes initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ElasticSearch indexes");
            }
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

                if (result.IsValidResponse && result.Documents.Any())
                {
                    response.Results = MapToCropResults(result.Documents);
                    response.TotalCount = result.Total;
                    response.Page = request.Page;
                    response.PageSize = request.PageSize;
                    response.ProcessingTimeMs = result.Took;
                    response.Query = request.Query;
                }
            }
            catch (Exception ex)
            {
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

                var searchRequest = new SearchRequest("qc_records")
                {
                    From = from,
                    Size = request.PageSize,
                    Query = BuildQCSearchQuery(request.Query, request.FoId, request.Passed, request.Grade)
                };

                var result = await _client.SearchAsync<object>(searchRequest);

                if (result.IsValidResponse && result.Documents.Any())
                {
                    response.Results = MapToQCResults(result.Documents);
                    response.TotalCount = result.Total;
                    response.Page = request.Page;
                    response.PageSize = request.PageSize;
                    response.ProcessingTimeMs = result.Took;
                    response.Query = request.Query;
                }
            }
            catch (Exception ex)
            {
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
            var queries = new List<Query>();

            if (!string.IsNullOrWhiteSpace(query))
            {
                queries.Add(new MultiMatchQuery
                {
                    Query = query,
                    Fields = new[] { "cropName^3", "variety^2", "farmerName^2", "farmAddress" },
                    Fuzziness = new Fuzziness("AUTO")
                });
            }

            if (farmerId.HasValue)
            {
                queries.Add(new TermQuery
                {
                    Field = "farmerId",
                    Value = farmerId.Value
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

            if (!string.IsNullOrEmpty(state))
            {
                queries.Add(new TermQuery
                {
                    Field = "farmState",
                    Value = state
                });
            }

            if (queries.Count == 0)
                return new MatchAllQuery();

            return new BoolQuery { Must = queries };
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
                queries.Add(new MultiMatchQuery
                {
                    Query = query,
                    Fields = new[] { "foName^2", "farmerName^2", "grade" },
                    Fuzziness = new Fuzziness("AUTO")
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
                    results.Add(new CropSearchResult
                    {
                        Id = Convert.ToInt32(dict["id"]),
                        FarmerId = Convert.ToInt32(dict["farmerId"]),
                        FarmerName = dict["farmerName"]?.ToString(),
                        CatalogProductId = Convert.ToInt32(dict["catalogProductId"]),
                        CropName = dict["cropName"]?.ToString(),
                        QuantityAvailable = Convert.ToDecimal(dict["quantityAvailable"]),
                        Unit = dict["unit"]?.ToString(),
                        Variety = dict["variety"]?.ToString(),
                        AskingPrice = Convert.ToDecimal(dict["askingPrice"]),
                        HarvestDate = Convert.ToDateTime(dict["harvestDate"]),
                        FarmAddress = dict["farmAddress"]?.ToString(),
                        FarmState = dict["farmState"]?.ToString(),
                        FarmDistrict = dict["farmDistrict"]?.ToString(),
                        Status = dict["status"]?.ToString()
                    });
                }
            }

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
                    results.Add(new QCSearchResult
                    {
                        Id = Convert.ToInt32(dict["id"]),
                        ProcurementRequestId = Convert.ToInt32(dict["procurementRequestId"]),
                        FoId = Convert.ToInt32(dict["foId"]),
                        FoName = dict["foName"]?.ToString(),
                        Grade = dict["grade"]?.ToString(),
                        AcceptedQuantity = Convert.ToDecimal(dict["acceptedQuantity"]),
                        Passed = Convert.ToBoolean(dict["passed"]),
                        SubmittedAt = Convert.ToDateTime(dict["submittedAt"])
                    });
                }
            }

            return results;
        }
    }
}