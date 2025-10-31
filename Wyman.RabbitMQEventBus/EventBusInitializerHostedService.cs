using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wyman.RabbitMQEventBus;

/// <summary>
/// 应用启动后初始化事件总线订阅，避免在容器构建期间阻塞或因外部依赖失败导致启动失败。
/// </summary>
internal class EventBusInitializerHostedService : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IntegrationEventHandlerRegistry _registry;
    private readonly ILogger<EventBusInitializerHostedService> _logger;

    public EventBusInitializerHostedService(IEventBus eventBus, IntegrationEventHandlerRegistry registry, ILogger<EventBusInitializerHostedService> logger)
    {
        _eventBus = eventBus;
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var handlerType in _registry.HandlerTypes)
        {
            var integrationEventNameAttributes = handlerType.GetCustomAttributes(typeof(IntegrationEventNameAttribute), inherit: true)
                .Cast<IntegrationEventNameAttribute>()
                .ToArray();

            if (integrationEventNameAttributes.Length == 0)
            {
                _logger.LogError("There should be at least one IntegrationEventNameAttribute on {HandlerType}", handlerType);
                continue;
            }

            foreach (var attribute in integrationEventNameAttributes)
            {
                try
                {
                    await _eventBus.SubscribeAsync(attribute.Name, handlerType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to subscribe handler '{Handler}' to event '{EventName}' during startup", handlerType.Name, attribute.Name);
                }
            }
        }
    }
}


