using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MyServiceBus.Domains.Sessions;

namespace MyServiceBus.Server.Controllers
{
    public class GreetingController : Controller
    {
        [HttpPost("Greeting")]
        public long Index([FromForm][Required]string name, [FromForm][Required]string clientVersion)
        {
            var grpcSession = ServiceLocator.GrpcSessionsList.GenerateNewSession(name, clientVersion);
            return grpcSession.Id;
        }
        
        [HttpPost("Greeting/Ping")]
        public IActionResult Index([FromForm][Required]long sessionId)
        {
            var session = ServiceLocator.GrpcSessionsList.TryGetSession(sessionId, DateTime.UtcNow);
            
            if (session == null)
                return Forbid();
            
            return Content(session.Id.ToString());
        }
    }
}