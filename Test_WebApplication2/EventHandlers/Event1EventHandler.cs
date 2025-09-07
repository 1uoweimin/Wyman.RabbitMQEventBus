using Wyman.RabbitMQEventBus;

namespace Test_WebApplication2.EventHandlers
{
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
}
