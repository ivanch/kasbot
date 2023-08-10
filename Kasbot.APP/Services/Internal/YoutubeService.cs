using Discord.WebSocket;
using YoutubeExplode.Videos;
using YoutubeExplode;
using Discord.Rest;
using YoutubeExplode.Videos.Streams;
using Kasbot.Models;

namespace Kasbot.Services.Internal
{
    public class YoutubeService
    {
        public YoutubeService()
        {

        }

        public async Task<MediaCollection> DownloadPlaylistMetadataFromYoutube(SocketUserMessage message, string search)
        {
            var collection = new MediaCollection();
            var youtube = new YoutubeClient();

            var playlistInfo = await youtube.Playlists.GetAsync(search);
            await youtube.Playlists.GetVideosAsync(search).ForEachAsync(videoId =>
            {
                var media = new Media
                {
                    Name = videoId.Title,
                    Length = videoId.Duration ?? new TimeSpan(0),
                    VideoId = videoId.Id,
                    Message = message,
                    Flags = new Flags()
                };

                collection.Medias.Add(media);
            });

            collection.CollectionName = playlistInfo.Title;

            return collection;
        }

        public async Task<Media> DownloadMetadataFromYoutube(Media media)
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

        public SearchType GetSearchType(string query)
        {
            if (string.IsNullOrEmpty(query))
                return SearchType.None;

            if (query.StartsWith("http://") || query.StartsWith("https://"))
            {
                if (query.Contains("playlist?list="))
                    return SearchType.PlaylistURL;

                // need to add 'else if' for ChannelURL

                return SearchType.VideoURL;
            }

            return SearchType.StringSearch;
        }
    }

    public enum SearchType
    {
        None,
        StringSearch,
        VideoURL,
        PlaylistURL,
        ChannelURL
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
