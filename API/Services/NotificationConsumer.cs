using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Models.Notification;
using API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace API.Services
{
    public class NotificationConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        public NotificationConsumer(IServiceProvider serviceProvider, IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory()
            {
                HostName = _config["RabbitMQ:Host"],
                UserName = _config["RabbitMQ:Username"],
                Password = _config["RabbitMQ:Password"],
                VirtualHost = _config["RabbitMQ:VirtualHost"],
            };

            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "admin_notifications",
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            var consumer = new AsyncEventingBasicConsumer(channel);

            // Inside ReceivedAsync handler
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // ✅ FIX: Standardize options
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var notification = JsonSerializer.Deserialize<vm_Notification>(message, options);

                    if (notification != null && !string.IsNullOrEmpty(notification.Title))
                    {
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var redis = scope.ServiceProvider.GetRequiredService<RedisService>();
                            await redis.AppendToListAsync("admin:notifs", notification);
                            Console.WriteLine($"[REDIS] Success: {notification.Title} saved.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            };

            await channel.BasicConsumeAsync(
                queue: "admin_notifications",
                autoAck: true,
                consumer: consumer
            );

            // Keep service alive
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
