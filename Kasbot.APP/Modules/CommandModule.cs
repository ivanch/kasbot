using Discord;
using Discord.Commands;
using Kasbot.Models;
using Kasbot.Services;
using Kasbot.Services;

namespace Kasbot.Modules
{
    public class CommandModule : ModuleBase<ShardedCommandContext>
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
            Console.WriteLine("Joining on " + Context.Guild.Name);

            string youtubeUrl = text;
            IVoiceChannel channel = (Context.User as IVoiceState).VoiceChannel;
            if (channel is null)
            {
                throw new Exception("You must be connect in a voice channel!");
            }

            var flags = new Flags();
            var withoutFlags = flags.Parse(text);
            await PlayerService.Play(Context, withoutFlags.Trim(), flags);
        }

        [Command("join", RunMode = RunMode.Async)]
        public async Task JoinAsync()
        {
            Console.WriteLine("Joining on " + Context.Guild.Name);

            IVoiceChannel channel = (Context.User as IVoiceState).VoiceChannel;
            if (channel is null)
            {
                throw new Exception("You must be connect in a voice channel!");
            }

            await PlayerService.Join(Context);
        }

        [Command("stop", RunMode = RunMode.Async)]
        public async Task StopAsync()
        {
            await PlayerService.Stop(Context.Guild.Id);
        }

        [Command("skip", RunMode = RunMode.Async)]
        public async Task SkipAsync()
        {
            await PlayerService.Skip(Context.Guild.Id);
        }

        [Command("leave", RunMode = RunMode.Async)]
        public async Task LeaveAsync()
        {
            await PlayerService.Leave(Context.Guild.Id);
        }

        [Alias("r")]
        [Command("repeat", RunMode = RunMode.Async)]
        public async Task RepeatAsync()
        {
            await PlayerService.Repeat(Context.Guild.Id);
        }
    }
}
