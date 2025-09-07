using Microsoft.AspNetCore.Mvc;
using Wyman.RabbitMQEventBus;

namespace Test_WebApplication1.Controllers
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
        public ActionResult PublishEvent1(string name, string msg)
        {
           _eventBus.PublishAsync("Event1", new { Name = name, Msg = msg });
            return Ok();
        }

        [HttpGet]
        public async Task<ActionResult> PublishEvent2(string name, string msg)
        {
            await _eventBus.PublishAsync("Event2", new { Name = name, Msg = msg });
            return Ok();
        }

        [HttpGet]
        public ActionResult PublishEvent3(string name, string msg)
        {
            _eventBus.PublishAsync("Event3", new { Name = name, Msg = msg });
            return Ok();
        }
    }
}
