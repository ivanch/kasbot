using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using NAudio.Wave;
using YoutubeExplode;

namespace Kasbot.Services
{
    public class PlayerService
    {
        public Dictionary<SocketGuild, Media> Clients { get; set; }

        public PlayerService()
        {
            Clients = new Dictionary<SocketGuild, Media>();
        }

        private async Task<Stream> DownloadAudioFromYoutube(string youtubeUrl)
        {
            var youtube = new YoutubeClient();
            var videoId = await youtube.Search.GetVideosAsync(youtubeUrl).FirstOrDefaultAsync();
            var streamInfoSet = await youtube.Videos.Streams.GetManifestAsync(videoId.Id);
            var highestAudioStreamInfo = streamInfoSet.GetAudioStreams().OrderByDescending(s => s.Bitrate).FirstOrDefault();
            var streamVideo = await youtube.Videos.Streams.GetAsync(highestAudioStreamInfo);
            var memoryStream = new MemoryStream();
            await streamVideo.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public async Task Play(SocketCommandContext Context, string arguments)
        {
            IVoiceChannel channel = (Context.User as IVoiceState).VoiceChannel;

            var audioStream = await DownloadAudioFromYoutube(arguments);
            if (audioStream is null)
            {
                await Context.Channel.SendMessageAsync("Failed to download audio from YouTube.");
                return;
            }

            var audioClient = await channel.ConnectAsync();
            using (var mp3Reader = new StreamMediaFoundationReader(audioStream))
            {
                var audioOut = audioClient.CreatePCMStream(AudioApplication.Music);
                await audioClient.SetSpeakingAsync(true);

                var media = new Media
                {
                    AudioClient = audioClient,
                    Message = Context.Message,
                    Name = "",
                    AudioOutStream = audioOut,
                };
                Clients.Add(Context.Guild, media);

                await mp3Reader.CopyToAsync(audioOut);
                await audioClient.SetSpeakingAsync(false);
            }
        }

        public async Task Stop(SocketCommandContext Context)
        {
            if (!Clients.ContainsKey(Context.Guild))
                return;

            var media = Clients[Context.Guild];
            Clients.Remove(Context.Guild);

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
        public IAudioClient AudioClient { get; set; }
        public AudioOutStream AudioOutStream { get; set; }
        public SocketUserMessage Message { get; set; }
    }
}
