using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Kasbot.Services;
using Kasbot.Services.Internal;
using Microsoft.Extensions.DependencyInjection;
using TextCommandFramework.Services;

namespace TextCommandFramework
{
    class Program
    {
        private static string TOKEN = Environment.GetEnvironmentVariable("TOKEN");

        static void Main(string[] args)
        {
            if (TOKEN == null)
            {
                throw new Exception("Discord Bot Token was not found.");
            }

            new Program().MainAsync().GetAwaiter().GetResult();
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
            return new ServiceCollection()
                .AddSingleton(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
                    TotalShards = 3
                })
                .AddSingleton<DiscordShardedClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<YoutubeService>()
                .AddSingleton<AudioService>()
                .AddSingleton<PlayerService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<PictureService>()
                .BuildServiceProvider();
        }
    }
}
