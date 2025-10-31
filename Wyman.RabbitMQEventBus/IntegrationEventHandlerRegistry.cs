namespace Wyman.RabbitMQEventBus;

/// <summary>
/// 保存集成事件处理器类型的注册表，供启动时统一订阅。
/// </summary>
internal class IntegrationEventHandlerRegistry
{
    public IntegrationEventHandlerRegistry(params Type[] handlerTypes)
    {
        HandlerTypes = handlerTypes ?? Array.Empty<Type>();
    }

    public IReadOnlyCollection<Type> HandlerTypes { get; }
}


