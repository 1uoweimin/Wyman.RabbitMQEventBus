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

        // 保存处理器类型注册表，供后台服务启动时订阅使用
        services.AddSingleton(new IntegrationEventHandlerRegistry(eventHandlerTypes.ToArray()));

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

            return new RabbitMQEventBus(serviceScopeFactory, connectionFactory, queueName, loggerFactory, rabbitMqOption);
        });

        // 使用后台托管服务在应用启动后执行事件订阅，避免阻塞构建期
        services.AddHostedService<EventBusInitializerHostedService>();
        return services;
    }
}
