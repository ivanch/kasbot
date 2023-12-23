using Kasbot.App.Services.Internal;
using Serilog;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Kasbot.Services.Internal
{
    public class YoutubeService
    {
        private ILogger Logger { get; set; }

        public YoutubeService(ILogger logger)
        {
            this.Logger = logger;
        }

        public async Task<MediaCollection> FetchPlaylist(Media rawMedia)
        {
            var collection = new MediaCollection();
            var youtube = new YoutubeClient();

            Logger.Debug($"Fetching playlist from YouTube: {rawMedia.Search}");

            var playlistInfo = await youtube.Playlists.GetAsync(rawMedia.Search);
            await youtube.Playlists.GetVideosAsync(rawMedia.Search).ForEachAsync(videoId =>
            {
                var media = new Media
                {
                    Name = videoId.Title,
                    Length = videoId.Duration ?? new TimeSpan(0),
                    VideoId = videoId.Id,
                    Message = rawMedia.Message,
                    Flags = rawMedia.Flags
                };

                collection.Medias.Add(media);
            });

            collection.CollectionName = playlistInfo.Title;

            Logger.Debug($"Fetched playlist from YouTube: {rawMedia.Search}");

            return collection;
        }

        public async Task<Media> FetchSingleMedia(Media media)
        {
            Logger.Debug($"Fetching single media: {media.Search}");

            var youtube = new YoutubeClient();

            IVideo? videoId;

            if (media.Search.StartsWith("http://") || media.Search.StartsWith("https://"))
                videoId = await youtube.Videos.GetAsync(media.Search);
            else
                videoId = await youtube.Search.GetVideosAsync(media.Search).FirstOrDefaultAsync();

            if (videoId == null)
            {
                Logger.Error($"No video found for \"{media.Search}\".");
                return media;
            }

            Logger.Debug($"Found video: {videoId.Title}");

            media.Name = videoId.Title;
            media.Length = videoId.Duration ?? new TimeSpan(0);
            media.VideoId = videoId.Id;

            return media;
        }

        public async Task<MemoryStream?> DownloadAudioFromYoutube(Media media)
        {
            if (media.VideoId == null)
            {
                return null;
            }

            var memoryStream = new MemoryStream();
            var youtube = new YoutubeClient();

            var streamInfoSet = await youtube.Videos.Streams.GetManifestAsync((VideoId)media.VideoId);
            var streamInfo = streamInfoSet.GetAudioOnlyStreams().GetWithHighestBitrate();
            var streamVideo = await youtube.Videos.Streams.GetAsync(streamInfo);
            streamVideo.Position = 0;

            streamVideo.CopyTo(memoryStream);
            memoryStream.Position = 0;

            return memoryStream;
        }
    }
}
