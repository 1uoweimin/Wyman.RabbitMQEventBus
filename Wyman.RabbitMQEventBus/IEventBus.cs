namespace Wyman.RabbitMQEventBus;

/// <summary>
/// 事件总线接口，用于发布和订阅事件。
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 发布事件到事件总线。
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventData"></param>
    /// <returns></returns>
    Task PublishAsync(string eventName, object? eventData);

    /// <summary>
    /// 订阅事件从事件总线。
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="handlerType"></param>
    /// <returns></returns>
    Task SubscribeAsync(string eventName, Type handlerType);

    /// <summary>
    /// 取消订阅事件从事件总线。
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="handlerType"></param>
    /// <returns></returns>
    Task UnsubscribeAsync(string eventName, Type handlerType);
}
