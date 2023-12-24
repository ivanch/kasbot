using Serilog;
using SpotifyAPI.Web;

namespace Kasbot.App.Services.Internal
{
    public class SpotifyService
    {
        private readonly string spotifyClientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ?? string.Empty;
        private readonly string spotifyClientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET") ?? string.Empty;

        private SpotifyClient? spotifyClient = null;
        private ILogger Logger { get; set; }

        public SpotifyService(ILogger logger)
        {
            this.Logger = logger;

            SetupSpotifyClient();
        }

        private void SetupSpotifyClient()
        {
            if (string.IsNullOrWhiteSpace(spotifyClientId) ||
                string.IsNullOrWhiteSpace(spotifyClientSecret))
            {
                Logger.Warning("Spotify Token was not found. Will disable Spotify integration.");
                return;
            }

            if (RefreshToken().IsFaulted)
            {
                throw new Exception("Failed to create Spotify client.");
            }
        }

        private async Task CheckTokenValid()
        {
            if (spotifyClient == null)
            {
                Logger.Warning("Spotify integration is disabled.");
                throw new Exception("Spotify integration is disabled.");
            }

            try
            {
                await spotifyClient.Browse.GetCategories();
            }
            catch (Exception)
            {
                await RefreshToken();
            }
        }

        private async Task RefreshToken()
        {
            if (spotifyClient == null)
            {
                spotifyClient = null;
            }

            var config = SpotifyClientConfig.CreateDefault();

            var request = new ClientCredentialsRequest(spotifyClientId, spotifyClientSecret);
            var response = await (new OAuthClient(config)).RequestToken(request);

            spotifyClient = new SpotifyClient(config.WithToken(response.AccessToken));
        }

        public async Task<Media> FetchSingleMedia(Media media)
        {
            await CheckTokenValid();

            var trackId = UrlResolver.GetSpotifyResourceId(media.Search);
            var spotifyTrack = await spotifyClient.Tracks.Get(trackId);

            if (spotifyTrack == null)
            {
                Logger.Error($"No track found on Spotify for \"{media.Search}\".");    
                throw new Exception($"No track found on Spotify for \"{media.Search}\".");
            }

            media.Search = spotifyTrack.Name;

            return media;
        }

        public async Task<MediaCollection> FetchPlaylist(Media rawMedia)
        {
            await CheckTokenValid();

            var playlistId = UrlResolver.GetSpotifyResourceId(rawMedia.Search);
            var spotifyPlaylist = await spotifyClient.Playlists.Get(playlistId);

            if (spotifyPlaylist == null || spotifyPlaylist.Tracks == null)
            {
                Logger.Error($"No playlist found on Spotify for \"{rawMedia.Search}\".");
                throw new Exception($"No playlist found on Spotify for \"{rawMedia.Search}\".");
            }

            var collection = new MediaCollection();
            collection.CollectionName = spotifyPlaylist.Name ?? string.Empty;

            if (spotifyPlaylist.Tracks.Items == null || spotifyPlaylist.Tracks.Items.Count == 0)
            {
                Logger.Error($"No tracks found for playlist \"{spotifyPlaylist.Name}\".");
                throw new Exception($"No tracks found for playlist \"{spotifyPlaylist.Name}\".");
            }

            Logger.Debug($"Found {spotifyPlaylist.Tracks.Items.Count} tracks for playlist \"{spotifyPlaylist.Name}\".");

            foreach (var playlistTrack in spotifyPlaylist.Tracks.Items)
            {
                if (playlistTrack.Track is not FullTrack track)
                {
                    continue;
                }

                collection.Medias.Add(new Media
                {
                    Search = track.Name,
                    Message = rawMedia.Message,
                    Flags = rawMedia.Flags,
                });
            }

            return collection;
        }

        public async Task<MediaCollection> FetchAlbum(Media rawMedia)
        {
            await CheckTokenValid();

            var albumId = UrlResolver.GetSpotifyResourceId(rawMedia.Search);
            var spotifyAlbum = await spotifyClient.Albums.Get(albumId);

            if (spotifyAlbum == null || spotifyAlbum.Tracks == null)
            {
                Logger.Error($"No album found on Spotify for \"{rawMedia.Search}\".");
                throw new Exception($"No album found on Spotify for \"{rawMedia.Search}\".");
            }

            var collection = new MediaCollection();
            collection.CollectionName = spotifyAlbum.Name ?? string.Empty;

            if (spotifyAlbum.Tracks.Items == null || spotifyAlbum.Tracks.Items.Count == 0)
            {
                Logger.Error($"No tracks found for album \"{spotifyAlbum.Name}\".");
                throw new Exception($"No tracks found for album \"{spotifyAlbum.Name}\".");
            }

            Logger.Debug($"Found {spotifyAlbum.Tracks.Items.Count} tracks for album \"{spotifyAlbum.Name}\".");

            foreach (var track in spotifyAlbum.Tracks.Items)
            {
                collection.Medias.Add(new Media
                {
                    Search = track.Name,
                    Message = rawMedia.Message,
                    Flags = rawMedia.Flags,
                });
            }

            return collection;
        }
    }
}