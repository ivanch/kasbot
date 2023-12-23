using System.Web;

namespace Kasbot.App.Services.Internal
{
    public static class UrlResolver
    {
        private const string SpotifyUrl = "open.spotify.com";

        public static SearchType GetSearchType(string query)
        {
            if (string.IsNullOrEmpty(query))
                return SearchType.None;

            if (IsURL(query))
            {
                if (IsSpotifyUrl(query))
                {
                    if (query.Contains("/track/"))
                        return SearchType.SpotifyTrack;

                    if (query.Contains("/album/"))
                        return SearchType.SpotifyAlbum;

                    if (query.Contains("/playlist/"))
                        return SearchType.SpotifyPlaylist;

                    if (query.Contains("/artist/"))
                        return SearchType.SpotifyArtist;
                }

                if (query.Contains("playlist?list="))
                    return SearchType.YoutubePlaylist;

                if (query.Contains("list="))
                    return SearchType.VideoPlaylistURL;

                return SearchType.VideoURL;
            }

            return SearchType.StringSearch;
        }

        public static string GetVideoId(string url)
        {
            if (url.Contains("v="))
            {
                var uri = new Uri(url);
                var query = HttpUtility.ParseQueryString(uri.Query);
                return query["v"] ?? string.Empty;
            }

            if (url.Contains("youtu.be/"))
            {
                var uri = new Uri(url);
                return uri.Segments[1];
            }

            return url;
        }

        public static string GetSpotifyResourceId(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            var uri = new Uri(url);
            return uri.Segments[uri.Segments.Length - 1];
        }

        private static bool IsURL(string url)
        {
            return url.StartsWith("http://") || url.StartsWith("https://");
        }

        private static bool IsSpotifyUrl(string url)
        {
            return url.Contains(SpotifyUrl);
        }
    }

    public enum SearchType
    {
        None,
        StringSearch,
        VideoURL,
        VideoPlaylistURL,
        YoutubePlaylist,
        SpotifyTrack,
        SpotifyAlbum,
        SpotifyPlaylist,
        SpotifyArtist
    }
}
