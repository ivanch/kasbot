using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Kasbot.Services;
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

                var client = services.GetRequiredService<DiscordSocketClient>();

                client.Log += LogAsync;
                client.LoggedIn += () => Client_LoggedIn(client);
                client.Ready += () => Client_Ready(client);
                services.GetRequiredService<CommandService>().Log += LogAsync;

                await Connect(client);
                //client.Disconnected += async (ex) => await Connect(client);

                await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

                await Task.Delay(Timeout.Infinite);
            }
        }

        public async Task Connect(DiscordSocketClient client)
        {
            await client.LoginAsync(TokenType.Bot, TOKEN);
            await client.StartAsync();
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

        private Task Client_LoggedIn(DiscordSocketClient client)
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
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
                })
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<YoutubeService>()
                .AddSingleton<PlayerService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<PictureService>()
                .BuildServiceProvider();
        }
    }
}
