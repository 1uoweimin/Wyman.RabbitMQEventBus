using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Wyman.RabbitMQEventBus;

/// <summary>
/// RabbitMQ扩展类，用于配置和使用RabbitMQ事件总线。
/// </summary>
public static class RabbitMQExtensions
{
    /// <summary>
    /// 添加RabbitMQ事件总线服务。
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configSection"></param>
    /// <param name="queueName"></param>
    /// <param name="assemblies"></param>
    /// <returns></returns>
    public static IServiceCollection AddRabbitMQEventBus(this IServiceCollection services, IConfigurationSection configSection, string queueName, params Assembly[] assemblies)
    {
        var eventHandlerTypes = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IIntegrationEventHandler).IsAssignableFrom(t))
            .AsEnumerable();

        return AddRabbitMQEventBus(services, configSection, queueName, eventHandlerTypes);
    }

    /// <summary>
    /// 添加RabbitMQ事件总线服务。
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configSection"></param>
    /// <param name="queueName"></param>
    /// <param name="eventHandlerTypes"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddRabbitMQEventBus(this IServiceCollection services, IConfigurationSection configSection, string queueName, IEnumerable<Type> eventHandlerTypes)
    {
        services.Configure<RabbitMqOption>(configSection);

        foreach (var handlerType in eventHandlerTypes)
        {
            services.AddScoped(handlerType);
        }

        services.AddSingleton<IEventBus>(sp =>
        {
            var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var rabbitMqOption = sp.GetRequiredService<IOptions<RabbitMqOption>>().Value;

            var connectionFactory = new ConnectionFactory() { HostName = rabbitMqOption.HostName };
            if (!string.IsNullOrWhiteSpace(rabbitMqOption.UserName)) connectionFactory.UserName = rabbitMqOption.UserName;
            if (!string.IsNullOrWhiteSpace(rabbitMqOption.Password)) connectionFactory.Password = rabbitMqOption.Password;
            if (rabbitMqOption.Port.HasValue) connectionFactory.Port = rabbitMqOption.Port.Value;
            if (!string.IsNullOrWhiteSpace(rabbitMqOption.VirtualHost)) connectionFactory.VirtualHost = rabbitMqOption.VirtualHost;

            if (string.IsNullOrWhiteSpace(queueName)) throw new ArgumentNullException(nameof(queueName));

            var rabbitMQEventBus = new RabbitMQEventBus(serviceScopeFactory, connectionFactory, queueName, loggerFactory, rabbitMqOption);

            foreach (var eventHandlerType in eventHandlerTypes)
            {
                var integrationEventNameAttributes = eventHandlerType.GetCustomAttributes<IntegrationEventNameAttribute>();
                if (!integrationEventNameAttributes.Any())
                {
                    throw new InvalidOperationException($"There shoule be at least one IntegrationEventNameAttribute on {eventHandlerType}");
                }
                foreach (var attribute in integrationEventNameAttributes)
                {
                    rabbitMQEventBus.SubscribeAsync(attribute.Name, eventHandlerType).GetAwaiter().GetResult();
                }
            }

            return rabbitMQEventBus;
        });
        return services;
    }

    /// <summary>
    /// 使用RabbitMQ事件总线中间件。
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IApplicationBuilder UseRabbitMQEventBus(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetService<IEventBus>();
        if (eventBus == null)
        {
            throw new InvalidOperationException("IEventBus is not registered. Please call AddRabbitMQEventBus before UseRabbitMQEventBus.");
        }
        return app;
    }
}
