using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System.Diagnostics;
using YoutubeExplode;
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

        private async Task<List<Media>> DownloadPlaylistMetadataFromYoutube(SocketUserMessage message, string search)
        {
            var list = new List<Media>();
            var youtube = new YoutubeClient();

            var playlistInfo = await youtube.Playlists.GetAsync(search);
            await youtube.Playlists.GetVideosAsync(search).ForEachAsync(videoId =>
            {
                var media = new Media();

                media.Name = videoId.Title;
                media.Length = videoId.Duration ?? new TimeSpan(0);
                media.VideoId = videoId.Id;
                media.Message = message;

                list.Add(media);
            });

            await message.Channel.SendMessageAsync($"Queued **{list.Count}** items from *{playlistInfo.Title}* playlist.");

            return list;
        }

        private async Task<Media> DownloadMetadataFromYoutube(Media media)
        {
            var youtube = new YoutubeClient();

            IVideo? videoId;

            if (media.Search.StartsWith("http://") || media.Search.StartsWith("https://"))
                videoId = await youtube.Videos.GetAsync(media.Search);
            else
                videoId = await youtube.Search.GetVideosAsync(media.Search).FirstOrDefaultAsync();

            if (videoId == null)
            {
                return media;
            }

            media.Name = videoId.Title;
            media.Length = videoId.Duration ?? new TimeSpan(0);
            media.VideoId = videoId.Id;

            return media;
        }

        private async Task<MemoryStream?> DownloadAudioFromYoutube(Media media)
        {
            if (media.VideoId == null)
            {
                return null;
            }

            var memoryStream = new MemoryStream();
            var youtube = new YoutubeClient();

            var streamInfoSet = await youtube.Videos.Streams.GetManifestAsync((VideoId) media.VideoId);
            var streamInfo = streamInfoSet.GetAudioOnlyStreams().GetWithHighestBitrate();
            var streamVideo = await youtube.Videos.Streams.GetAsync(streamInfo);
            streamVideo.Position = 0;
            
            streamVideo.CopyTo(memoryStream);
            memoryStream.Position = 0;

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
                await Enqueue(Context.Guild.Id, conn, media);
                return;
            }

            IVoiceChannel channel = (Context.User as IVoiceState).VoiceChannel;

            var audioClient = await channel.ConnectAsync();
            audioClient.Disconnected += (ex) => AudioClient_ClientDisconnected(Context.Guild.Id);

            conn = CreateConnection(audioClient, Context.Guild.Id);
            await Enqueue(Context.Guild.Id, conn, media);
        }

        private async Task Enqueue(ulong guildId, Connection conn, Media media)
        {
            if (media.Search.StartsWith("https://") && media.Search.Contains("playlist?list="))
            {
                var startPlay = conn.Queue.Count == 0;
                var medias = await DownloadPlaylistMetadataFromYoutube(media.Message, media.Search);

                medias.ForEach(m => conn.Queue.Enqueue(m));

                if (startPlay)
                {
                    await PlayNext(guildId);
                }

                return;
            }

            media = await DownloadMetadataFromYoutube(media);

            if (media.VideoId == null)
            {
                var message = await media.Message.Channel.SendMessageAsync($"No video found for \"{media.Search}\".");
                await Task.Delay(3_000);
                await message.DeleteAsync();
                return;
            }

            conn.Queue.Enqueue(media);
            if (conn.Queue.Count == 1)
            {
                await PlayNext(guildId);
            }
            else
            {
                var message = $"Queued **{media.Name}** *({media.Length.TotalMinutes:00}:{media.Length.Seconds:00})*";
                media.QueueMessage = await media.Message.Channel.SendMessageAsync(message);
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
                Clients[guildId].Queue.Clear();
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

            var message = $"⏯ Playing: **{nextMedia.Name}** *({nextMedia.Length.TotalMinutes:00}:{nextMedia.Length.Seconds:00})*";
            nextMedia.PlayMessage = await nextMedia.Message.Channel.SendMessageAsync(message);

            if (nextMedia.QueueMessage != null)
            {
                await nextMedia.QueueMessage.DeleteAsync();
            }

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

            await nextMedia.PlayMessage.DeleteAsync();

            if (Clients[guildId].Queue.Count > 0)
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
                throw new Exception("Sorry, ffmpeg killed itself in a tragic accident!");
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

            foreach (var v in media.Queue.Skip(1))
            {
                await RemoveMediaMessages(v);
            }

            media.Queue.Clear();

            if (media.CurrentAudioStream != null)
                media.CurrentAudioStream.Close();
        }

        private async Task RemoveMediaMessages(Media media)
        {
            try
            {
                if (media.PlayMessage != null)
                    await media.PlayMessage.DeleteAsync();
            }
            catch { }
        }

        public async Task Leave(ulong guildId)
        {
            if (!Clients.ContainsKey(guildId))
                return;

            await Stop(guildId);
            var media = Clients[guildId];

            if (media.AudioClient != null)
                await media.AudioClient.StopAsync();

            Clients.Remove(guildId);
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

        public VideoId? VideoId { get; set; }
        public SocketUserMessage Message { get; set; }
        public RestUserMessage PlayMessage { get; set; }
        public RestUserMessage? QueueMessage { get; set; }
    }
}
