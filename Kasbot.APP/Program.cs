using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Kasbot.App.Internal.Services;
using Kasbot.App.Services.Internal;
using Kasbot.Services;
using Kasbot.Services.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace Kasbot
{
    public class Program
    {
        private static string TOKEN = Environment.GetEnvironmentVariable("TOKEN");
        private static int SHARDS = int.Parse(Environment.GetEnvironmentVariable("SHARDS") ?? "0");

        static void Main(string[] args)
        {
            if (TOKEN == null)
            {
                throw new Exception("Discord Bot Token was not found.");
            }
            if (SHARDS == 0)
            {
                Console.WriteLine("Shards amount not found, defaulting to 1.");
                SHARDS = 1;
            }

            Task.Factory.StartNew(() => new Program().RunGrpc(args));

            new Program()
                .MainAsync()
                .GetAwaiter()
                .GetResult();
        }

        private void RunGrpc(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddGrpc();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(System.Net.IPAddress.Any, 5001, listenOptions =>
                {
                    listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
                });
            });

            var app = builder.Build();
            app.MapGrpcService<StatusService>();


            app.Run(url: "https://localhost:7042");
        }

        public async Task MainAsync()
        {
            using (var services = ConfigureServices())
            {
                var client = services.GetRequiredService<DiscordShardedClient>();

                client.Log += LogAsync;
                client.LoggedIn += () => Client_LoggedIn(client);
                client.ShardReady += (shard) => Client_Ready(shard);
                services.GetRequiredService<CommandService>().Log += LogAsync;

                await client.LoginAsync(TokenType.Bot, TOKEN);
                await client.StartAsync();

                await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

                await Task.Delay(Timeout.Infinite);
            }
        }

        private async Task Client_Ready(DiscordSocketClient client)
        {
            var announceLoginGuild = ulong.Parse(Environment.GetEnvironmentVariable("ANNOUNCE_LOGIN_GUILD") ?? "0");
            var announceLoginChannel = ulong.Parse(Environment.GetEnvironmentVariable("ANNOUNCE_LOGIN_CHANNEL") ?? "0");

            if (announceLoginGuild == 0 || announceLoginChannel == 0)
            {
                return;
            }

            var channel = client.GetGuild(announceLoginGuild).GetTextChannel(announceLoginChannel);

            if (channel == null)
            {
                Console.WriteLine("Announce channel not found.");
                return;
            }

            await channel.SendMessageAsync("@everyone LIVE!");
        }

        private Task Client_LoggedIn(DiscordShardedClient client)
        {
            Console.WriteLine("Successfully logged in!");

            return Task.CompletedTask;
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private ServiceProvider ConfigureServices()
        {
            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
                .CreateBootstrapLogger();

            return new ServiceCollection()
                .AddSingleton(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
                    TotalShards = SHARDS
                })
                .AddSerilog(logger)
                .AddSingleton<DiscordShardedClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<SpotifyService>()
                .AddSingleton<YoutubeService>()
                .AddSingleton<MediaService>()
                .AddSingleton<AudioService>()
                .AddSingleton<PlayerService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<PictureService>()
                .BuildServiceProvider();
        }
    }
}
