using Wyman.RabbitMQEventBus;

namespace Test_WebApplication2.EventHandlers
{
    [IntegrationEventName("Event2")]
    public class Event2EventHandler : IntegrationEventDynamicHandler
    {
        private readonly ILogger<Event2EventHandler> _logger;

        public Event2EventHandler(ILogger<Event2EventHandler> logger)
        {
            _logger = logger;
        }

        public override Task HandleDynamicAsync(string eventName, dynamic eventData)
        {
            _logger.LogInformation($"eventName:{eventName}\teventData-name:{eventData.Name};eventData-msg:{eventData.Msg}");
            return Task.CompletedTask;
        }
    }
}
