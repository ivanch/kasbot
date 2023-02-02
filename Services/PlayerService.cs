using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace Kasbot.Services
{
    public class PlayerService
    {
        public Dictionary<ulong, Media> Clients { get; set; }

        public PlayerService()
        {
            Clients = new Dictionary<ulong, Media>();
        }

        private async Task<MemoryStream> DownloadAudioFromYoutube(string youtubeUrl)
        {
            var memoryStream = new MemoryStream();
            var youtube = new YoutubeClient();

            var videoId = await youtube.Search.GetVideosAsync(youtubeUrl).FirstOrDefaultAsync();
            var streamInfoSet = await youtube.Videos.Streams.GetManifestAsync(videoId.Id);
            var streamInfo = streamInfoSet.GetAudioOnlyStreams().GetWithHighestBitrate();
            var streamVideo = await youtube.Videos.Streams.GetAsync(streamInfo);
            streamVideo.Position = 0;
            
            streamVideo.CopyTo(memoryStream);
            memoryStream.Position = 0;

            return memoryStream;
        }

        public async Task Play(SocketCommandContext Context, string arguments)
        {
            IVoiceChannel channel = (Context.User as IVoiceState).VoiceChannel;

            var mp3Stream = await DownloadAudioFromYoutube(arguments);
            var ffmpeg = CreateStream();

            var audioClient = await channel.ConnectAsync();
            audioClient.ClientDisconnected += AudioClient_ClientDisconnected;
            var media = new Media()
            {
                AudioClient = audioClient,
                Message = Context.Message,
                Name = ""
            };
            Clients.Add(Context.Guild.Id, media);

            Task stdin = new Task(() =>
            {
                using (var input = mp3Stream)
                {
                    input.CopyTo(ffmpeg.StandardInput.BaseStream);
                }
            });

            Task stdout = new Task(() =>
            {
                using (var output = ffmpeg.StandardOutput.BaseStream)
                using (var discord = audioClient.CreatePCMStream(AudioApplication.Music))
                {
                    output.CopyTo(discord);
                    discord.Flush();
                }
            });

            stdin.Start();
            stdout.Start();

            Task.WaitAll(stdin, stdout);

            ffmpeg.WaitForExit();
        }

        private async Task AudioClient_ClientDisconnected(ulong arg)
        {
            await Stop(arg);
        }

        private Process CreateStream()
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            });

            if (process == null || process.HasExited)
            {
                throw new Exception("Sorry, ffmpeg killed himself in a tragic accident!");
            }

            return process;
        }

        public async Task Stop(ulong guildId)
        {
            if (!Clients.ContainsKey(guildId))
                return;

            var media = Clients[guildId];

            await media.Message.DeleteAsync();
            await media.AudioClient.StopAsync();

            Clients.Remove(guildId);
        }
    }

    public class Media
    {
        public string Name { get; set; }
        public int Length { get; set; }
        public string FileName { get; set; }
        public IAudioClient AudioClient { get; set; }
        public SocketUserMessage Message { get; set; }
    }
}
