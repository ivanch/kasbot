using Discord;
using Discord.Audio;
using Discord.Commands;
using Kasbot.Services;
using NAudio.Wave;
using TextCommandFramework.Services;
using YoutubeExplode;

namespace TextCommandFramework.Modules
{
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        public PictureService PictureService { get; set; }
        public PlayerService PlayerService { get; set; }

        [Command("ping")]
        [Alias("pong", "hello")]
        public Task PingAsync()
            => ReplyAsync("pong!");

        [Command("cat")]
        public async Task CatAsync()
        {
            var stream = await PictureService.GetCatPictureAsync();
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "cat.png");
        }

        [Command("echo")]
        public Task EchoAsync([Remainder] string text)
            => ReplyAsync('\u200B' + text);

        [Command("play", RunMode = RunMode.Async)]
        public async Task PlayAsync([Remainder] string text)
        {
            var user = Context.User;
            if (user.IsBot) return;

            string youtubeUrl = text;
            IVoiceChannel channel = (Context.User as IVoiceState).VoiceChannel;
            if (channel is null)
            {
                await Context.Channel.SendMessageAsync("You need to be in a voice channel to use this command.");
                return;
            }

            await PlayerService.Play(Context, text);
        }

        [Command("stop", RunMode = RunMode.Async)]
        public async Task StopAsync()
        {
            var user = Context.User;
            if (user.IsBot) return;

            await PlayerService.Stop(Context);
        }

        [Command("guild_only")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Sorry, this command must be ran from within a server, not a DM!")]
        public Task GuildOnlyCommand()
            => ReplyAsync("Nothing to see here!");
    }
}
