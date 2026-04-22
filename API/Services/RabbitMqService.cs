using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using API.Models.Notification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class RabbitMqService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<RabbitMqService> _logger;
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _virtualHost;

        public RabbitMqService(IConfiguration config, ILogger<RabbitMqService> logger)
        {
            _config = config;
            _logger = logger;

            // Read configuration properly
            _host = _config["RabbitMQ:Host"] ?? "localhost";
            _port = int.Parse(_config["RabbitMQ:Port"] ?? "5672");
            _username = _config["RabbitMQ:Username"] ?? "guest";
            _password = _config["RabbitMQ:Password"] ?? "guest";
            _virtualHost = _config["RabbitMQ:VirtualHost"] ?? "/";

            _logger.LogInformation("RabbitMqService initialized with Host: {Host}, VHost: {VHost}", _host, _virtualHost);
        }
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        private async Task EnsureConnectionAsync()
        {
            if (_connection != null && _channel != null &&
                _connection.IsOpen && _channel.IsOpen)
                return;

            await _connectionLock.WaitAsync();

            try
            {
                if (_connection != null && _channel != null &&
                    _connection.IsOpen && _channel.IsOpen)
                    return;

                var factory = new ConnectionFactory
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

                _logger.LogInformation(
                    "Connecting to RabbitMQ at {Host}:{Port}, VHost: {VHost}",
                    _host, _port, _virtualHost
                );

                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

                _logger.LogInformation("RabbitMQ connection established successfully");
            }
            finally
            {
                _connectionLock.Release();
            }
        }
        // Send to SPECIFIC user
        public async Task PublishToUserAsync(
           int userId,
           string title,
           string message,
           string type,
           string? refType = null,
           int? refId = null)
        {
            try
            {
                await EnsureConnectionAsync();

                var notification = new vm_Notification
                {
                    TargetUserId = userId,
                    TargetRole = "",
                    Title = title,
                    Message = message,
                    Type = type,
                    ReferenceType = refType,
                    ReferenceId = refId,
                    CreatedAt = DateTime.UtcNow
                };

                // ✅ FIX: Single queue
                var queueName = "notifications_user";

                await PublishAsync(queueName, notification);

                _logger.LogInformation(
                    "Published notification to user {UserId}",
                    userId
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish notification to user {UserId}", userId);
                throw;
            }
        }
        // Send to ALL users with a specific ROLE
        public async Task PublishToRoleAsync(string role, string title, string message, string type,
            string? refType = null, int? refId = null)
        {
            try
            {
                await EnsureConnectionAsync();

                var notification = new vm_Notification
                {
                    TargetUserId = 0, // 0 means broadcast to all in role
                    TargetRole = role.ToLower(),
                    Title = title,
                    Message = message,
                    Type = type,
                    ReferenceType = refType,
                    ReferenceId = refId,
                    CreatedAt = DateTime.UtcNow
                };

                string queueName = $"notifications_{role.ToLower()}";
                await PublishAsync(queueName, notification);
                _logger.LogInformation("Published broadcast notification to role {Role}: {Title}", role, title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish broadcast notification to role {Role}", role);
                throw;
            }
        }

        private async Task PublishAsync(string queue, vm_Notification notification)
        {
            await EnsureConnectionAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var body = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(notification, options)
            );

            await _channel!.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            var properties = new BasicProperties
            {
                Persistent = true,
                DeliveryMode = DeliveryModes.Persistent
            };

            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: queue,
                mandatory: false,
                basicProperties: properties,
                body: body
            );
        }
        public async Task DisposeAsync()
        {
            try
            {
                if (_channel != null && _channel.IsOpen)
                    await _channel.CloseAsync();

                if (_connection != null && _connection.IsOpen)
                    await _connection.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing RabbitMQ connection");
            }
        }
    }
}