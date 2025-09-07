namespace Wyman.RabbitMQEventBus;

/// <summary>
/// 集成事件名称特性，用于标记集成事件类的名称。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class IntegrationEventNameAttribute: Attribute
{
    public IntegrationEventNameAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 集成事件名称。
    /// </summary>
    public string Name { get; }
}

