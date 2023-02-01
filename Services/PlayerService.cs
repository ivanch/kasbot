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
        public Dictionary<SocketGuild, Media> Clients { get; set; }

        public PlayerService()
        {
            Clients = new Dictionary<SocketGuild, Media>();
        }

        private async Task<string> DownloadAudioFromYoutube(string youtubeUrl)
        {
            var youtube = new YoutubeClient();
            var videoId = await youtube.Search.GetVideosAsync(youtubeUrl).FirstOrDefaultAsync();
            var streamInfoSet = await youtube.Videos.Streams.GetManifestAsync(videoId.Id);
            var streamInfo = streamInfoSet.GetAudioOnlyStreams().GetWithHighestBitrate();
            var streamVideo = await youtube.Videos.Streams.GetAsync(streamInfo);
            var fileName = $"{videoId.Id}.mp3";

            using (var fileStream = new FileStream(fileName, FileMode.Create))
            {
                await streamVideo.CopyToAsync(fileStream);
            }
            return fileName;
        }

        public async Task Play(SocketCommandContext Context, string arguments)
        {
            IVoiceChannel channel = (Context.User as IVoiceState).VoiceChannel;

            string filename;

            try
            {
                filename = await DownloadAudioFromYoutube(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Error while downloading video from YouTube!");
            }
            
            var audioClient = await channel.ConnectAsync();

            using (var ffmpeg = CreateStream(filename))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = audioClient.CreatePCMStream(AudioApplication.Mixed))
            {
                try
                {
                    var media = new Media()
                    {
                        AudioClient = audioClient,
                        AudioOutStream = discord,
                        FileName = filename,
                        Message = Context.Message,
                        Name = ""
                    };
                    Clients.Add(Context.Guild, media);
                    await output.CopyToAsync(discord);
                }
                finally
                {
                    await discord.FlushAsync();
                    File.Delete(filename);
                }
            }
        }

        private Process CreateStream(string filename)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i {filename} -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            });

            if (process == null || process.HasExited)
            {
                throw new Exception("Sorry, ffmpeg killed himself! Bah.");
            }

            return process;
        }

        public async Task Stop(SocketCommandContext Context)
        {
            if (!Clients.ContainsKey(Context.Guild))
                return;

            var media = Clients[Context.Guild];
            Clients.Remove(Context.Guild);

            File.Delete(media.FileName);
            await Context.Message.DeleteAsync();
            await media.Message.DeleteAsync();
            await media.AudioOutStream.DisposeAsync();
            await media.AudioOutStream.ClearAsync(new CancellationToken());
            await media.AudioClient.StopAsync();
        }

    }

    public class Media
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public IAudioClient AudioClient { get; set; }
        public AudioOutStream AudioOutStream { get; set; }
        public SocketUserMessage Message { get; set; }
    }
}
