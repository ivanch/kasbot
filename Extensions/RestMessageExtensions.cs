using Discord;
using Discord.Rest;

namespace Kasbot.Extensions
{
    public static class RestMessageExtensions
    {
        public static async Task TryDeleteAsync(this RestMessage message, RequestOptions options = null)
        {
            try
            {
                await message.DeleteAsync(options);
            }
            catch { }
        }

    }
}
