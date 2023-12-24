using Discord;
using Discord.Audio;
using Discord.Commands;
using Kasbot.App.Services.Internal;
using Kasbot.Extensions;
using Kasbot.Models;
using Kasbot.Services.Internal;
using Serilog;

namespace Kasbot.Services
{
    public class PlayerService
    {
        private Dictionary<ulong, Connection> Clients { get; set; }
        private AudioService AudioService { get; set; }
        private MediaService MediaService { get; set; }
        private ILogger Logger { get; set; }

        public PlayerService(AudioService audioService, MediaService mediaService, ILogger logger)
        {
            Clients = new Dictionary<ulong, Connection>();

            AudioService = audioService;
            MediaService = mediaService;
            this.Logger = logger;
        }

        private async Task<Connection> CreateConnection(ulong guildId, IVoiceChannel voiceChannel)
        {
            var conn = new Connection();
            IAudioClient audioClient = await voiceChannel.ConnectAsync(selfDeaf: true);

            conn.AudioClient = audioClient;
            conn.AudioChannel = voiceChannel;

            if (Clients.ContainsKey(guildId))
                Clients.Remove(guildId);

            Clients.Add(guildId, conn);

            return conn;
        }

        public async Task Play(ShardedCommandContext Context, string arguments, Flags flags)
        {
            var media = new Media()
            {
                Message = Context.Message,
                Search = arguments.Trim(),
                Flags = flags,
                Name = string.Empty,
            };
            var guildId = Context.Guild.Id;
            var userVoiceChannel = (Context.User as IVoiceState).VoiceChannel;

            if (Clients.TryGetValue(guildId, out var conn))
            {
                if (conn.AudioChannel.Id != userVoiceChannel.Id)
                {
                    await Stop(guildId);
                    conn = await CreateConnection(guildId, userVoiceChannel);
                }
            }
            else
            {
                conn = await CreateConnection(guildId, userVoiceChannel);
            }

            await Enqueue(guildId, conn, media);
        }

        private async Task Enqueue(ulong guildId, Connection conn, Media media)
        {
            var startPlay = conn.Queue.Count == 0;

            var mediaType = UrlResolver.GetSearchType(media.Search);

            Logger.Debug($"Enqueueing {media.Search} as {mediaType}");

            switch (mediaType)
            {
                case SearchType.StringSearch:
                case SearchType.VideoURL:
                case SearchType.VideoPlaylistURL:
                case SearchType.SpotifyTrack:
                    Logger.Debug($"Fetching {media.Search} as {mediaType}");

                    media = await MediaService.FetchSingleMedia(media, mediaType);

                    if (!startPlay && !media.Flags.Silent)
                    {
                        var message = $"Queued **{media.Name}** *({media.Length.TotalMinutes:00}:{media.Length.Seconds:00})*";
                        media.QueueMessage = await media.Channel.SendMessageAsync(message);
                    }

                    Logger.Debug($"Enqueueing {media.Search} as {mediaType}");

                    conn.Queue.Enqueue(media);

                    break;
                case SearchType.YoutubePlaylist:
                case SearchType.SpotifyPlaylist:
                case SearchType.SpotifyAlbum:
                    Logger.Debug($"Fetching {media.Search} as {mediaType}");

                    var mediaCollection = await MediaService.FetchMediaCollection(media, mediaType);

                    mediaCollection.Medias.ForEach(m => conn.Queue.Enqueue(m));

                    Logger.Debug($"Enqueueing {media.Search} as {mediaType}");

                    await media.Channel.SendMessageAsync($"Queued **{mediaCollection.Medias.Count}** items from *{mediaCollection.CollectionName}* playlist.");

                    break;
            }

            if (startPlay)
                await PlayNext(guildId);
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

            // since we can't verify if the bot was disconnected by a websocket error, we do this check which should do enough
            if (Clients[guildId].AudioClient.ConnectionState == ConnectionState.Disconnected)
            {
                var voiceChannel = Clients[guildId].AudioChannel;
                Clients.Remove(guildId);
                await CreateConnection(guildId, voiceChannel);
            }

            Logger.Debug($"Downloading {nextMedia.Name}");

            var mp3Stream = await MediaService.DownloadAudioFromYoutube(nextMedia);

            Logger.Debug($"Playing {nextMedia.Name}");

            if (mp3Stream == null)
            {
                Logger.Error($"Failed to download {nextMedia.Name}");
                await Stop(guildId);
                return;
            }

            var audioClient = Clients[guildId].AudioClient;

            Logger.Information($"Playing {nextMedia.Name}");

            if (!nextMedia.Flags.Silent)
            {
                var message = $"⏯ Playing: **{nextMedia.Name}** *({nextMedia.Length.TotalMinutes:00}:{nextMedia.Length.Seconds:00})*";
                nextMedia.PlayMessage = await nextMedia.Channel.SendMessageAsync(message);
            }

            if (nextMedia.QueueMessage != null)
            {
                await nextMedia.QueueMessage.TryDeleteAsync();
            }

            AudioService.StartAudioTask(mp3Stream, audioClient,
                (outAudioStream) =>
                {
                    Clients[guildId].CurrentAudioStream = outAudioStream;
                },
                async (ac) =>
                {
                    if (ac.Exception != null)
                    {
                        Logger.Error(ac.Exception, $"Error in stream: {ac.Exception.Message}");
                        await nextMedia.Channel.SendTemporaryMessageAsync("Error in stream: " + ac.Exception.ToString());
                    }
                });

            await nextMedia.PlayMessage.TryDeleteAsync();

            if (Clients[guildId].Queue.Count > 0 &&
                !Clients[guildId].Queue.First().Flags.Repeat)
                Clients[guildId].Queue.Dequeue();

            await PlayNext(guildId);
        }

        public Task Skip(ulong guildId)
        {
            if (!Clients.ContainsKey(guildId))
                throw new Exception("Bot is not connected!");

            var media = Clients[guildId];

            if (media.CurrentAudioStream == null)
                throw new Exception("There is no audio playing!");

            media.CurrentAudioStream.Close();

            return Task.CompletedTask;
        }

        public async Task Stop(ulong guildId)
        {
            if (!Clients.ContainsKey(guildId))
                throw new Exception("Bot is not connected!");

            var media = Clients[guildId];

            foreach (var v in media.Queue.Skip(0))
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
                throw new Exception("Bot is not connected!");

            await Stop(guildId);
            var media = Clients[guildId];

            if (media.AudioClient != null)
                await media.AudioClient.StopAsync();

            Clients.Remove(guildId);
        }

        public async Task Repeat(ulong guildId)
        {
            if (!Clients.ContainsKey(guildId))
                throw new Exception("Bot is not connected!");

            if (Clients[guildId].Queue.Count == 0)
                throw new Exception("The queue is empty!");

            var media = Clients[guildId].Queue.First();
            media.Flags.Repeat = !media.Flags.Repeat;
            await media.Channel.SendTemporaryMessageAsync(media.Flags.Repeat ? "Repeat turned on!" : "Repeat turned off!");
        }

        public async Task Join(ShardedCommandContext Context)
        {
            var guildId = Context.Guild.Id;
            if (Clients.ContainsKey(guildId))
                return;

            await CreateConnection(guildId, (Context.User as IVoiceState).VoiceChannel);
        }
    }
}
