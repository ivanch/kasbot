using Discord;
using Discord.Audio;
using Discord.Commands;
using Kasbot.Extensions;
using System.Diagnostics;

namespace Kasbot.Services
{
    public class PlayerService
    {
        public Dictionary<ulong, Connection> Clients { get; set; }
        public YoutubeService YoutubeService { get; set; }

        public PlayerService(YoutubeService youtubeService)
        {
            this.YoutubeService = youtubeService;

            Clients = new Dictionary<ulong, Connection>();
        }

        private async Task<Connection> CreateConnection(ulong guildId, IVoiceChannel voiceChannel)
        {
            var conn = new Connection();
            IAudioClient audioClient = await voiceChannel.ConnectAsync(selfDeaf: true);

            audioClient.Disconnected += (ex) => Stop(guildId);
            audioClient.StreamDestroyed += (ex) => Stop(guildId);

            conn.AudioClient = audioClient;
            conn.AudioChannel = voiceChannel;

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

            conn = await CreateConnection(Context.Guild.Id, (Context.User as IVoiceState).VoiceChannel);
            await Enqueue(Context.Guild.Id, conn, media);
        }

        private async Task Enqueue(ulong guildId, Connection conn, Media media)
        {
            var startPlay = conn.Queue.Count == 0;

            switch (YoutubeService.GetSearchType(media.Search))
            {
                case SearchType.StringSearch:
                case SearchType.VideoURL:
                    media = await YoutubeService.DownloadMetadataFromYoutube(media);

                    if (media.VideoId == null)
                    {
                        await media.Channel.SendTemporaryMessageAsync($"No video found for \"{media.Search}\".");
                        return;
                    }

                    conn.Queue.Enqueue(media);
                    if (startPlay)
                        await PlayNext(guildId);
                    else
                    {
                        var message = $"Queued **{media.Name}** *({media.Length.TotalMinutes:00}:{media.Length.Seconds:00})*";
                        media.QueueMessage = await media.Channel.SendMessageAsync(message);
                    }

                    break;
                case SearchType.ChannelURL:
                case SearchType.PlaylistURL:
                    var collection = await YoutubeService.DownloadPlaylistMetadataFromYoutube(media.Message, media.Search);

                    collection.Medias.ForEach(m => conn.Queue.Enqueue(m));
                    
                    await media.Channel.SendMessageAsync($"Queued **{collection.Medias.Count}** items from *{collection.CollectionName}* playlist.");

                    if (startPlay)
                        await PlayNext(guildId);

                    break;
                case SearchType.None:
                default:
                    break;
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
                await Stop(guildId);
                return;
            }

            // since we can't verify if the bot was disconnected by a websocket error, we do this check
            if (Clients[guildId].AudioClient.ConnectionState == ConnectionState.Disconnected)
            {
                var voiceChannel = Clients[guildId].AudioChannel;
                Clients.Remove(guildId);
                await CreateConnection(guildId, voiceChannel);
            }

            var mp3Stream = await YoutubeService.DownloadAudioFromYoutube(nextMedia);

            if (mp3Stream == null)
            {
                await Stop(guildId);
                return;
            }

            var audioClient = Clients[guildId].AudioClient;
            var ffmpeg = CreateStream();

            var message = $"⏯ Playing: **{nextMedia.Name}** *({nextMedia.Length.TotalMinutes:00}:{nextMedia.Length.Seconds:00})*";
            nextMedia.PlayMessage = await nextMedia.Channel.SendMessageAsync(message);

            if (nextMedia.QueueMessage != null)
            {
                await nextMedia.QueueMessage.TryDeleteAsync();
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
                    }
                }
            });

            stdin.Start();
            stdout.Start();

            await stdin.ContinueWith(async ac =>
            {
                if (ac.Exception != null)
                {
                    await nextMedia.Channel.SendTemporaryMessageAsync("Error in input stream: " + ac.Exception.ToString());
                }
            });
            await stdout.ContinueWith(async ac =>
            {
                if (ac.Exception != null)
                {
                    await nextMedia.Channel.SendTemporaryMessageAsync("Error while playing: " + ac.Exception.ToString());
                }
            });

            Task.WaitAll(stdin, stdout);

            ffmpeg.Close();

            await nextMedia.PlayMessage.TryDeleteAsync();

            if (Clients[guildId].Queue.Count > 0)
                Clients[guildId].Queue.Dequeue();

            await PlayNext(guildId);
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
                await v.PlayMessage.TryDeleteAsync();
            }

            media.Queue.Clear();

            if (media.CurrentAudioStream != null)
                media.CurrentAudioStream.Close();
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
        public IVoiceChannel AudioChannel { get; set; }
        public Stream? CurrentAudioStream { get; set; }
        public Queue<Media> Queue { get; set; } = new Queue<Media>();
    }
}
