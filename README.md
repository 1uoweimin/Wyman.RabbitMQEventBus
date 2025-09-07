# Wyman.RabbitMQEventBus
一个基于RabbitMQ的.Net集成事件程序

## 注意
需要先运行 RabbitMQ 程序

## 使用步骤
1. 配置文件

    "EventBus": {
    "HostName": "127.0.0.1",
    "ExchangeName": "Lwm_Exchange",
    "UserName": "admin",
    "Password": "secret"
    }

2. 注册服务

    var eventBusConfig = builder.Configuration.GetSection("EventBus");
    builder.Services.AddRabbitMQEventBus(eventBusConfig, "lwm_queue1", Assembly.GetExecutingAssembly());

3. 使用中间件

    app.UseRabbitMQEventBus();

4. 发布事件

    a.构造函数注入 IEventBus _eventBus
    b.发布事件 _eventBus.PublishAsync("Event1", new { Name = name, Msg = msg });

5. 监听事件

    [IntegrationEventName("Event1")]
    public class Event1EventHandler : IIntegrationEventHandler
    {
        private readonly ILogger<Event1EventHandler> _logger;
        public Event1EventHandler(ILogger<Event1EventHandler> logger)
        {
            _logger = logger;
        }
        public Task HandleAsync(string eventName, string eventData)
        {
            _logger.LogInformation($"eventName:{eventName},eventData:{eventData}");
            return Task.CompletedTask;
        }
    }


