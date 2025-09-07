namespace Wyman.RabbitMQEventBus;

/// <summary>
/// 集成事件处理器接口，用于处理集成事件。
/// </summary>
public interface IIntegrationEventHandler
{
    /// <summary>
    /// 处理集成事件的异步方法。
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventData"></param>
    /// <returns></returns>
    Task HandleAsync(string eventName, string eventData);
}

