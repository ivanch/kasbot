using Discord.Rest;
using Discord.WebSocket;
using Kasbot.Models;
using Kasbot.Services.Internal;
using Serilog;
using YoutubeExplode.Videos;

namespace Kasbot.App.Services.Internal
{
    public class MediaService
    {
        private YoutubeService YoutubeService { get; set; }
        private SpotifyService SpotifyService { get; set; }
        private ILogger Logger { get; set; }


        public MediaService(YoutubeService youtubeService, SpotifyService spotifyService, ILogger logger)
        {
            this.YoutubeService = youtubeService;
            this.SpotifyService = spotifyService;
            this.Logger = logger;
        }

        public async Task<Media> FetchSingleMedia(Media media, SearchType mediaType)
        {
            if (mediaType == SearchType.SpotifyTrack)
            {
                Logger.Debug($"Fetching single media: {media.Search}");
                media = await SpotifyService.FetchSingleMedia(media);
            }

            Logger.Debug($"Fetching single media (YouTube): {media.Search}");
            media = await YoutubeService.FetchSingleMedia(media);

            if (media.VideoId == null)
            {
                Logger.Error($"No video found for \"{media.Search}\".");
                throw new Exception($"No video found for \"{media.Search}\".");
            }

            return media;
        }

        public async Task<MediaCollection> FetchMediaCollection(Media rawMedia, SearchType mediaType)
        {
            var collection = new MediaCollection();

            if (mediaType == SearchType.SpotifyPlaylist)
            {
                Logger.Debug($"Fetching playlist from Spotify: {rawMedia.Search}");
                collection = await SpotifyService.FetchPlaylist(rawMedia);
                var tasks = collection.Medias.ToList().Select(media => YoutubeService.FetchSingleMedia(media));
                var results = await Task.WhenAll(tasks);
                Logger.Debug($"Fetched playlist from Spotify: {rawMedia.Search}");
            }

            if (mediaType == SearchType.SpotifyAlbum)
            {
                Logger.Debug($"Fetching album from Spotify: {rawMedia.Search}");
                collection = await SpotifyService.FetchAlbum(rawMedia);
                var tasks = collection.Medias.ToList().Select(media => YoutubeService.FetchSingleMedia(media));
                var results = await Task.WhenAll(tasks);
                Logger.Debug($"Fetched album from Spotify: {rawMedia.Search}");
            }

            if (mediaType == SearchType.YoutubePlaylist)
            {
                Logger.Debug($"Fetching playlist from YouTube: {rawMedia.Search}");
                collection = await YoutubeService.FetchPlaylist(rawMedia);
                Logger.Debug($"Fetched playlist from YouTube: {rawMedia.Search}");
            }

            return collection;
        }

        public async Task<MemoryStream?> DownloadAudioFromYoutube(Media media)
        {
            return await YoutubeService.DownloadAudioFromYoutube(media);
        }
    }

    public class Media
    {
        public string Search { get; set; }

        public string Name { get; set; }
        public TimeSpan Length { get; set; }
        public Flags Flags { get; set; }

        public VideoId? VideoId { get; set; }
        public RestUserMessage PlayMessage { get; set; }
        public RestUserMessage? QueueMessage { get; set; }

        private SocketUserMessage message;
        public SocketUserMessage Message
        {
            get => message;
            set
            {
                message = value;
                Channel = value.Channel;
            }
        }
        public ISocketMessageChannel Channel { get; private set; }
    }

    public class MediaCollection
    {
        public string CollectionName { get; set; }
        public List<Media> Medias { get; set; } = new List<Media>();
    }
}
