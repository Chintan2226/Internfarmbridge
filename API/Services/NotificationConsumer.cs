using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using API.BAL;
using API.Models.Notification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace API.Services
{
    public class NotificationConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;
        private readonly ILogger<NotificationConsumer> _logger;
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _virtualHost;

        // Define queues for each role
        private static readonly string[] RoleQueues = new string[] 
        { 
            "notifications_admin", 
            "notifications_farmer", 
            "notifications_vendor", 
            "notifications_fieldofficer" 
        };
        
        private static readonly string UserQueue = "notifications_user";

        public NotificationConsumer(
            IServiceProvider serviceProvider,
            IConfiguration config,
            ILogger<NotificationConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _config = config;
            _logger = logger;
            
            // Read configuration - CRITICAL: Use the same VirtualHost as producer
            _host = _config["RabbitMQ:Host"] ?? "localhost";
            _port = int.Parse(_config["RabbitMQ:Port"] ?? "5672");
            _username = _config["RabbitMQ:Username"] ?? "guest";
            _password = _config["RabbitMQ:Password"] ?? "guest";
            _virtualHost = _config["RabbitMQ:VirtualHost"] ?? "/";
            
            _logger.LogInformation("NotificationConsumer initialized with Host: {Host}, Port: {Port}, VHost: {VHost}", 
                _host, _port, _virtualHost);
        }

        private ConnectionFactory GetFactory()
        {
            return new ConnectionFactory
            {
                HostName = _host,
                Port = _port,
                UserName = _username,
                Password = _password,
                VirtualHost = _virtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60)
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait for API to fully start
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                IConnection? connection = null;
                IChannel? channel = null;
                
                try
                {
                    var factory = GetFactory();
                    
                    _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}, VHost: {VHost}", 
                        _host, _port, _virtualHost);
                    
                    connection = await factory.CreateConnectionAsync(stoppingToken);
                    channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                    // Declare role-based queues
                    foreach (var queue in RoleQueues)
                    {
                        await channel.QueueDeclareAsync(
                            queue: queue, 
                            durable: true, 
                            exclusive: false, 
                            autoDelete: false,
                            cancellationToken: stoppingToken
                        );
                        _logger.LogInformation("Queue ready: {Queue}", queue);
                    }
                    
                    // Declare user queue
                    await channel.QueueDeclareAsync(
                        queue: UserQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        cancellationToken: stoppingToken
                    );
                    _logger.LogInformation("Queue ready: {Queue}", UserQueue);

                    // Create consumer for role-based queues
                    foreach (var queue in RoleQueues)
                    {
                        var consumer = new AsyncEventingBasicConsumer(channel);
                        consumer.ReceivedAsync += async (model, ea) =>
                        {
                            await ProcessRoleQueueMessageAsync(queue, ea);
                        };

                        await channel.BasicConsumeAsync(
                            queue: queue, 
                            autoAck: true, 
                            consumer: consumer,
                            cancellationToken: stoppingToken
                        );
                        
                        _logger.LogInformation("Consumer started for queue: {Queue}", queue);
                    }
                    
                    // Create consumer for user queue
                    var userConsumer = new AsyncEventingBasicConsumer(channel);
                    userConsumer.ReceivedAsync += async (model, ea) =>
                    {
                        await ProcessUserQueueMessageAsync(ea);
                    };

                    await channel.BasicConsumeAsync(
                        queue: UserQueue,
                        autoAck: true,
                        consumer: userConsumer,
                        cancellationToken: stoppingToken
                    );
                    
                    _logger.LogInformation("Consumer started for queue: {Queue}", UserQueue);
                    _logger.LogInformation("All RabbitMQ consumers started successfully");

                    // Keep the service running
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Notification consumer is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Consumer crashed. Retrying in 10s...");
                    await Task.Delay(10000, stoppingToken);
                }
                finally
                {
                    channel?.Dispose();
                    connection?.Dispose();
                }
            }
        }

        private async Task ProcessRoleQueueMessageAsync(string queueName, BasicDeliverEventArgs ea)
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var notification = JsonSerializer.Deserialize<vm_Notification>(message, options);

                if (notification == null)
                {
                    _logger.LogWarning("Failed to deserialize notification message from {Queue}", queueName);
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var notificationHelper = scope.ServiceProvider.GetRequiredService<NotificationHelper>();

                // Extract role from queue name (notifications_admin -> admin)
                string role = queueName.Replace("notifications_", "");
                
                // Get all user IDs for this role
                var userIds = await GetUserIdsByRoleAsync(role);
                
                if (userIds.Count == 0)
                {
                    _logger.LogWarning("No active users found for role {Role}", role);
                    return;
                }
                
                // Create notification for each user in the role
                foreach (var userId in userIds)
                {
                    await notificationHelper.CreateNotificationAsync(
                        userId,
                        notification.Title,
                        notification.Message,
                        notification.Type,
                        notification.ReferenceType,
                        notification.ReferenceId
                    );
                }
                
                _logger.LogInformation("Broadcast notification sent to {Count} users with role {Role}: {Title}", 
                    userIds.Count, role, notification.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing role queue message from {Queue}", queueName);
            }
        }

        private async Task ProcessUserQueueMessageAsync(BasicDeliverEventArgs ea)
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var notification = JsonSerializer.Deserialize<vm_Notification>(message, options);

                if (notification == null)
                {
                    _logger.LogWarning("Failed to deserialize user notification message");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var notificationHelper = scope.ServiceProvider.GetRequiredService<NotificationHelper>();

                await notificationHelper.CreateNotificationAsync(
                    notification.TargetUserId,
                    notification.Title,
                    notification.Message,
                    notification.Type,
                    notification.ReferenceType,
                    notification.ReferenceId
                );
                
                _logger.LogInformation("User notification stored for user {TargetUserId}: {Title}", 
                    notification.TargetUserId, notification.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing user queue message");
            }
        }

        private async Task<List<int>> GetUserIdsByRoleAsync(string role)
        {
            var userIds = new List<int>();
            var connectionString = _config.GetConnectionString("DefaultConnection");
            
            try
            {
                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                
                var sql = "SELECT c_id FROM t_users WHERE c_role = @role AND c_is_active = true";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@role", role);
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    userIds.Add(reader.GetInt32(0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user IDs for role {Role}", role);
            }
            
            return userIds;
        }
    }
}