using Discord.Audio;
using System.Diagnostics;

namespace Kasbot.Services.Internal
{
    public class AudioService
    {
        public AudioService() { }

        public void StartAudioTask(Stream inputStream, IAudioClient outputAudioClient, Action<Stream> onStartAudio, Action<Task> onFinish)
        {
            var ffmpeg = CreateFFmpeg();

            Task stdin = new Task(() =>
            {
                using (var output = inputStream)
                {
                    try
                    {
                        output.CopyTo(ffmpeg.StandardInput.BaseStream);
                        ffmpeg.StandardInput.Close();
                    }
                    catch { }
                    finally
                    {
                        output.Flush();
                    }
                }
            });

            Task stdout = new Task(() =>
            {
                using (var output = ffmpeg.StandardOutput.BaseStream)
                using (var discord = outputAudioClient.CreatePCMStream(AudioApplication.Music))
                {
                    try
                    {
                        onStartAudio.Invoke(ffmpeg.StandardOutput.BaseStream);
                        output.CopyTo(discord);
                    }
                    catch { }
                    finally
                    {
                        discord.Flush();
                    }
                }
            });

            stdin.Start();
            stdout.Start();

            stdin.ContinueWith(onFinish);
            stdout.ContinueWith(onFinish);

            Task.WaitAll(stdin, stdout);

            ffmpeg.Close();
        }

        private Process CreateFFmpeg()
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
