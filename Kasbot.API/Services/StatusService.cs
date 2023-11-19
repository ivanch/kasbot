using Status;
using Grpc.Net.Client;

namespace Kasbot.API.Services
{
    public class StatusService
    {
        public StatusService() { }

        public async Task<bool> IsOk()
        {
            using var channel = GrpcChannel.ForAddress("https://localhost:7042");
            var client = new StatusRequester.StatusRequesterClient(channel);

            var reply = await client.SayHelloAsync(new HelloRequest { Name = "GreeterClient" });
            return reply.Message == "Hello GreeterClient";
        }
    }
}
