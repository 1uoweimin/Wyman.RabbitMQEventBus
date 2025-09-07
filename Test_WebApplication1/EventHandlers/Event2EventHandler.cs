using Wyman.RabbitMQEventBus;

namespace Test_WebApplication1.EventHandlers
{
    // 可以处理多个事件
    [IntegrationEventName("Event2")]
    [IntegrationEventName("Event3")]
    public class Event2EventHandler : IIntegrationEventHandler
    {
        private readonly ILogger<Event2EventHandler> _logger;

        public Event2EventHandler(ILogger<Event2EventHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(string eventName, string eventData)
        {
            _logger.LogInformation($"eventName:{eventName},eventData:{eventData}");
            return Task.CompletedTask;
        }
    }
}
