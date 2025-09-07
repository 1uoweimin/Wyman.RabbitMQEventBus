using Dynamic.Json;

namespace Wyman.RabbitMQEventBus;

/// <summary>
/// 集成事件处理器的动态处理器基类，用于处理动态类型的集成事件数据。
/// </summary>
public abstract class IntegrationEventDynamicHandler : IIntegrationEventHandler
{
    public Task HandleAsync(string eventName, string eventData)
    {
        dynamic dynamicEventData = DJson.Parse(eventData);
        return HandleDynamicAsync(eventName, dynamicEventData);
    }

    /// <summary>
    /// 处理动态类型的集成事件数据的异步方法。
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventData"></param>
    /// <returns></returns>
    public abstract Task HandleDynamicAsync(string eventName, dynamic eventData);
}
