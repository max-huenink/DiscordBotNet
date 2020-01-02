using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using System.Linq;
using System.Collections.Generic;

namespace discordBot.Services
{
    public class AudioService
    {
        private readonly ConcurrentDictionary<ulong, IAudioClient> ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();

        public async Task JoinAudio(IGuild guild, IVoiceChannel target)
        {
            if (ConnectedChannels.TryGetValue(guild.Id, out _))
                return;
            if (target.Guild.Id != guild.Id)
                return;

            var audioClient = await target.ConnectAsync();

            if (ConnectedChannels.TryAdd(guild.Id, audioClient))
            {
                // If you add a method to log happenings from this service,
                // you can uncomment these commented lines to make use of that.
                //await Log(LogSeverity.Info, $"Connected to voice on {guild.Name}.");
            }
        }

        private async Task UpdateSends()
        {
            foreach (var clientKVP in ConnectedChannels)
            {
                IAudioClient client = clientKVP.Value;
                IEnumerable<IGuildUser> usersInClientChannel = (await (client as IVoiceChannel).GetUsersAsync().FlattenAsync()).Where(u => !u.IsBot);
                ConnectedChannels.Where(c => !c.Value.Equals(client));
            }
        }

        public async Task LeaveAudio(IGuild guild)
        {
            if (ConnectedChannels.TryRemove(guild.Id, out IAudioClient client))
            {
                await client.StopAsync();
                //await Log(LogSeverity.Info, $"Disconnected from voice on {guild.Name}.");
            }
        }

        // Not necessary for my purposes yet, this is for playing music
        public async Task SendAudioAsync(IGuild guild, IMessageChannel channel, string path)
        {
            // Your task: Get a full path to the file if the value of 'path' is only a filename.
            if (!File.Exists(path))
            {
                await channel.SendMessageAsync("File does not exist.");
                return;
            }
            IAudioClient client;
            if (ConnectedChannels.TryGetValue(guild.Id, out client))
            {
                //await Log(LogSeverity.Debug, $"Starting playback of {path} in {guild.Name}");
                using (var ffmpeg = CreateProcess(path))
                using (var stream = client.CreatePCMStream(AudioApplication.Music))
                {
                    try { await ffmpeg.StandardOutput.BaseStream.CopyToAsync(stream); }
                    finally { await stream.FlushAsync(); }
                }
            }
        }

        private Process CreateProcess(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg.exe",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
        }
    }
}
