using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Kasbot.Services
{
    public class PlayerService
    {
        public Dictionary<ulong, Connection> Clients { get; set; }

        public PlayerService()
        {
            Clients = new Dictionary<ulong, Connection>();
        }

        private async Task<MemoryStream?> DownloadAudioFromYoutube(Media media)
        {
            var memoryStream = new MemoryStream();
            var youtube = new YoutubeClient();

            IVideo? videoId = null;
            
            if (media.Search.StartsWith("http://") || media.Search.StartsWith("https://"))
                videoId = await youtube.Videos.GetAsync(media.Search);
            else
                videoId = await youtube.Search.GetVideosAsync(media.Search).FirstOrDefaultAsync();

            if (videoId == null)
            {
                return null;
            }

            var streamInfoSet = await youtube.Videos.Streams.GetManifestAsync(videoId.Id);
            var streamInfo = streamInfoSet.GetAudioOnlyStreams().GetWithHighestBitrate();
            var streamVideo = await youtube.Videos.Streams.GetAsync(streamInfo);
            streamVideo.Position = 0;
            
            streamVideo.CopyTo(memoryStream);
            memoryStream.Position = 0;

            media.Name = videoId.Title;
            media.Length = videoId.Duration.GetValueOrDefault();

            return memoryStream;
        }

        private Connection CreateConnection(IAudioClient audioClient, ulong guildId)
        {
            var conn = new Connection()
            {
                AudioClient = audioClient,
            };

            if (Clients.ContainsKey(guildId))
                Clients.Remove(guildId);

            Clients.Add(guildId, conn);

            return conn;
        }

        public async Task Play(SocketCommandContext Context, string arguments)
        {
            var media = new Media()
            {
                Message = Context.Message,
                Search = arguments,
                Name = ""
            };

            if (Clients.TryGetValue(Context.Guild.Id, out var conn))
            {
                conn.Queue.Enqueue(media);
                if (conn.Queue.Count == 1)
                {
                    await PlayNext(Context.Guild.Id);
                }

                return;
            }

            IVoiceChannel channel = (Context.User as IVoiceState).VoiceChannel;

            var audioClient = await channel.ConnectAsync();
            audioClient.ClientDisconnected += (id) => AudioClient_ClientDisconnected(Context.Guild.Id);
            audioClient.Disconnected += (ex) => AudioClient_ClientDisconnected(Context.Guild.Id);

            conn = CreateConnection(audioClient, Context.Guild.Id);

            conn.Queue.Enqueue(media);
            if (conn.Queue.Count == 1)
            {
                await PlayNext(Context.Guild.Id);
            }
        }

        private async Task PlayNext(ulong guildId)
        {
            if (!Clients.ContainsKey(guildId) || Clients[guildId].Queue.Count == 0)
            {
                return;
            }

            var nextMedia = Clients[guildId].Queue.FirstOrDefault();

            if (nextMedia == null)
            {
                Clients[guildId].Queue.Dequeue();
                await Stop(guildId);
                return;
            }

            var mp3Stream = await DownloadAudioFromYoutube(nextMedia);

            if (mp3Stream == null)
            {
                await Stop(guildId);
                return;
            }

            var audioClient = Clients[guildId].AudioClient;
            var ffmpeg = CreateStream();

            var message = $"⏯ Playing: {nextMedia.Name} **({nextMedia.Length.TotalMinutes:00}:{nextMedia.Length.Seconds:00})**";
            nextMedia.PlayMessage = await nextMedia.Message.Channel.SendMessageAsync(message);

            Task stdin = new Task(() =>
            {
                using (var input = mp3Stream)
                {
                    try
                    {
                        input.CopyTo(ffmpeg.StandardInput.BaseStream);
                        ffmpeg.StandardInput.Close();
                    }
                    catch { }
                    finally
                    {
                        input.Flush();
                        input.Dispose();
                    }
                }
            });

            Task stdout = new Task(() =>
            {
                using (var output = ffmpeg.StandardOutput.BaseStream)
                using (var discord = audioClient.CreatePCMStream(AudioApplication.Music))
                {
                    try
                    {
                        Clients[guildId].CurrentAudioStream = output;
                        output.CopyTo(discord);
                    }
                    catch { }
                    finally
                    {
                        discord.Flush();
                        output.Close();
                    }
                }
            });

            stdin.Start();
            stdout.Start();

            Task.WaitAll(stdin, stdout);

            ffmpeg.Close();

            await nextMedia.Message.DeleteAsync();
            await nextMedia.PlayMessage.DeleteAsync();

            Clients[guildId].Queue.Dequeue();

            await PlayNext(guildId);
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

        public Task Skip(ulong guildId)
        {
            if (!Clients.ContainsKey(guildId))
                return Task.CompletedTask;

            var media = Clients[guildId];

            if (media.CurrentAudioStream == null)
                return Task.CompletedTask;

            media.CurrentAudioStream.Close();

            return Task.CompletedTask;
        }

        public async Task Stop(ulong guildId)
        {
            if (!Clients.ContainsKey(guildId))
                return;

            var media = Clients[guildId];

            foreach (var v in media.Queue)
            {
                await RemoveMediaMessages(v);
            }

            if (media.AudioClient != null)
                await media.AudioClient.StopAsync();

            Clients.Remove(guildId);
        }

        private async Task RemoveMediaMessages(Media media)
        {
            try
            {
                if (media.Message != null)
                    await media.Message.DeleteAsync();
            }
            catch { }
            try
            {
                if (media.PlayMessage != null)
                    await media.PlayMessage.DeleteAsync();
            } catch { }
        }
    }

    public class Connection
    {
        public IAudioClient AudioClient { get; set; }
        public Stream? CurrentAudioStream { get; set; }
        public Queue<Media> Queue { get; set; } = new Queue<Media>();
    }

    public class Media
    {
        public string Search { get; set; }
        public string Name { get; set; }
        public TimeSpan Length { get; set; }
        public SocketUserMessage Message { get; set; }
        public RestUserMessage PlayMessage { get; set; }
    }
}
