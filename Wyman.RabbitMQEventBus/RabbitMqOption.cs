namespace Wyman.RabbitMQEventBus;

/// <summary>
/// RabbitMQ集成事件选项类，用于配置RabbitMQ连接和交换机信息。
/// </summary>
internal class RabbitMqOption
{
    /// <summary>
    /// 主机名或IP地址。
    /// </summary>
    public required string HostName { get; set; }

    /// <summary>
    /// 交换机
    /// </summary>
    public required string ExchangeName { get; set; }

    /// <summary>
    /// 用户名，默认为 "guest"。
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 密码，默认为 "guest"。
    /// </summary>
    public string? Password { get; set; }

    public int? Port { get; set; }

    /// <summary>
    /// 用于指定 RabbitMQ 的资源隔离空间，便于多租户和权限管理。
    /// </summary>
    public string? VirtualHost { get; set; }

    /// <summary>
    /// 每个消费者最多能同时处理的未确认消息数。
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>
    /// 队列是否持久化（服务器重启后队列是否保留）。
    /// </summary>
    public bool QueueDurable { get; set; } = true;

    /// <summary>
    /// 消费失败的消息是否让消息重新入队（true 重新入队，false 丢弃或进入死信队列）。
    /// </summary>
    public bool RequeueOnFailure { get; set; } = false;
}

