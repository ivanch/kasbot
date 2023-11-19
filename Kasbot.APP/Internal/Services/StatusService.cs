using Grpc.Core;
using Microsoft.Extensions.Logging;
using Status;

namespace Kasbot.App.Internal.Services
{
    public class StatusService : StatusRequester.StatusRequesterBase
    {
        private readonly ILogger _logger;

        public StatusService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<StatusService>();
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Sending hello to {request.Name}");
            return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
        }
    }
}
