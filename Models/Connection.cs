using Discord;
using Discord.Audio;
using Kasbot.Services.Internal;

namespace Kasbot.Models
{
    public class Connection
    {
        public IAudioClient AudioClient { get; set; }
        public IVoiceChannel AudioChannel { get; set; }
        public Stream? CurrentAudioStream { get; set; }
        public Queue<Media> Queue { get; set; } = new Queue<Media>();
    }
}
