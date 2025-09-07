using System.Text.Json;

namespace Wyman.RabbitMQEventBus;

/// <summary>
/// 集成事件处理器的泛型类型处理器基类，用于处理特定类型的集成事件数据。
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class IntegrationEventGenericTypeHandler<T> : IIntegrationEventHandler
{
    public Task HandleAsync(string eventName, string eventData)
    {
        
        T instance = JsonSerializer.Deserialize<T>(eventData) ?? throw new InvalidOperationException("Deserialization returned null.");
        return HandleAsync(eventName, instance);
    }

    /// <summary>
    /// 处理特定类型的集成事件数据的异步方法。
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventData"></param>
    /// <returns></returns>
    public abstract Task HandleAsync(string eventName, T eventData);
}
