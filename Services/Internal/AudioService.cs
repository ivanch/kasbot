using System.Diagnostics;

namespace Kasbot.Services.Internal
{
    public class AudioService
    {
        public AudioService() { }

        public Process CreateStream()
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
    }
}
