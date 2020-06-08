using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyServiceBus.Server.Models;

namespace MyServiceBus.Server.Controllers
{
    public class TopicsController : Controller
    {
        [HttpGet("Topics")]
        public IEnumerable<TopicInfoModel> GetAll()
        {
            var topics = ServiceLocatorApi.TopicsList.Get();

            return topics.Select(TopicInfoModel.Create);
        }

        [HttpPost("Topics/Create")]
        public async Task<IActionResult> Create([FromForm][Required]string topicId, [FromForm][Required]int maxCachedMessages)
        {
            await ServiceLocatorApi.TopicsManagement.AddIfNotExistsAsync(topicId);
            return Json(GetAll());
        }

        [HttpPost("Topics/NewMessage")]
        public async Task<IActionResult> NewMessage([FromForm][Required]long sessionId, [FromForm]string topicId, [FromForm][Required]string messageBase64, [FromForm][Required]bool persistImmediately)
        {
            var session = ServiceLocatorApi.SessionsList.GetSession(sessionId, DateTime.UtcNow);
            if (session == null)
                return Forbid();
            
            var bytes = Convert.FromBase64String(messageBase64); 
            var result = await ServiceLocatorApi.MyServiceBusPublisher.PublishAsync(session, topicId, new[] {bytes}, DateTime.UtcNow, persistImmediately);
            return Json(new {result});
        }
    }
}