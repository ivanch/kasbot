using Discord;
using Discord.Commands;
using Kasbot.Services;
using TextCommandFramework.Services;

namespace TextCommandFramework.Modules
{
    public class PublicModule : ModuleBase<ShardedCommandContext>
    {
        public PictureService PictureService { get; set; }
        public PlayerService PlayerService { get; set; }

        [Command("cat")]
        public async Task CatAsync()
        {
            var stream = await PictureService.GetCatPictureAsync();
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "cat.png");
        }

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

            await PlayerService.Stop(Context.Guild.Id);
        }

        [Command("skip", RunMode = RunMode.Async)]
        public async Task SkipAsync()
        {
            var user = Context.User;
            if (user.IsBot) return;

            await PlayerService.Skip(Context.Guild.Id);
        }

        [Command("leave", RunMode = RunMode.Async)]
        public async Task LeaveAsync()
        {
            var user = Context.User;
            if (user.IsBot) return;

            await PlayerService.Leave(Context.Guild.Id);
        }
    }
}
