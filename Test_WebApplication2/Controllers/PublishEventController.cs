using Microsoft.AspNetCore.Mvc;
using Wyman.RabbitMQEventBus;

namespace Test_WebApplication2.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class PublishEventController : ControllerBase
    {
        private readonly IEventBus _eventBus;

        public PublishEventController(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        [HttpGet]
        public ActionResult PublishEvent2(string name, string msg)
        {
            _eventBus.PublishAsync("Event2", new { Name = name, Msg = msg });
            return Ok();
        }
    }
}
