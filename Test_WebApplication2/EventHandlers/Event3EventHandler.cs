using Test_WebApplication2.Models;
using Wyman.RabbitMQEventBus;

namespace Test_WebApplication2.EventHandlers
{
    [IntegrationEventName("Event3")]
    public class Event3EventHandler : IntegrationEventGenericTypeHandler<EventData3>
    {
        private readonly ILogger<Event3EventHandler> _logger;

        public Event3EventHandler(ILogger<Event3EventHandler> logger)
        {
            _logger = logger;
        }

        public override Task HandleAsync(string eventName, EventData3 eventData)
        {
            _logger.LogInformation($"eventName:{eventName}\teventData:{eventData.ToString()}");
            return Task.CompletedTask;
        }
    }
}
