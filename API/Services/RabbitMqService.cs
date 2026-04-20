using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RabbitMQ.Client;
using API.Models.Notification;
using Microsoft.Extensions.Configuration;

namespace API.Services
{
    public class RabbitMqService
    {
        private readonly IConfiguration _config;

        public RabbitMqService(IConfiguration config)
        {
            _config = config;
        }

        public async Task PublishNotification(vm_Notification notification)
        {
            var factory = new ConnectionFactory()
            {
                HostName = _config["RabbitMQ:Host"],
                UserName = _config["RabbitMQ:Username"],
                Password = _config["RabbitMQ:Password"],
                VirtualHost = _config["RabbitMQ:VirtualHost"],
                Port = int.Parse(_config["RabbitMQ:Port"] ?? "5672"),
            };

            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "admin_notifications",
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            // ✅ FIX: Use options to ensure JsonPropertyName attributes are honored
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            var json = JsonSerializer.Serialize(notification, options);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties { Persistent = true };
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "admin_notifications",
                mandatory: false,
                basicProperties: properties,
                body: body
            );
        }
    }
}
