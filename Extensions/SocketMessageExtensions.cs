using Discord;
using Discord.WebSocket;

namespace Kasbot.Extensions
{
    public static class SocketMessageExtensions
    {
        private const int MessageDelay = 3_000; // in ms

        public static async Task SendTemporaryMessageAsync(this ISocketMessageChannel channel, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None)
        {
            var message = await channel.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds, flags);
            await Task.Delay(MessageDelay);
            await message.DeleteAsync();
        }

        public static async Task SendTemporaryMessageAsync(this IMessageChannel channel, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None)
        {
            var message = await channel.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds, flags);
            await Task.Delay(MessageDelay);
            await message.DeleteAsync();
        }

    }
}
