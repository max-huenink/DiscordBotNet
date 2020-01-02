using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using System.Linq;
using System.Collections.Generic;
using Discord.WebSocket;
using Discord.Audio.Streams;

namespace discordBot.Services
{
    public class AudioService
    {
        private readonly ConcurrentDictionary<ulong, IAudioClient> ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();
        System.Threading.CancellationTokenSource cancelSpeaking = new System.Threading.CancellationTokenSource();

        public async Task JoinAudio(IGuild guild, IVoiceChannel target)
        {
            Console.WriteLine("Hello World");
            if (ConnectedChannels.TryGetValue(guild.Id, out _))
                return;
            if (target.Guild.Id != guild.Id)
                return;
            Console.WriteLine("text");
            var audioClient = await target.ConnectAsync();
            Console.WriteLine("Connected");
            
            if (ConnectedChannels.TryAdd(guild.Id, audioClient))
            {
                cancelSpeaking.Cancel();
                Console.WriteLine("Attempting to update sends");
                await UpdateSends(cancelSpeaking.Token);
                cancelSpeaking = new System.Threading.CancellationTokenSource();
                // If you add a method to log happenings from this service,
                // you can uncomment these commented lines to make use of that.
                //await Log(LogSeverity.Info, $"Connected to voice on {guild.Name}.");
            }
        }

        private async Task UpdateSends(System.Threading.CancellationToken newListener)
        {
            foreach (var kvpSpeaker in ConnectedChannels)
            {
                IAudioClient speaker = kvpSpeaker.Value;
                await speaker.SetSpeakingAsync(true);
                AudioOutStream speak = speaker.CreateDirectOpusStream();
                Console.WriteLine("Speaker");
                foreach (var kvpListener in ConnectedChannels.Where(c=>!speaker.Equals(c)))
                {
                    Console.WriteLine("Listener");
                    IAudioClient listener = kvpListener.Value;
                    await kvpListener.Value.CreateOpusStream().CopyToAsync(speaker.CreateOpusStream());
                    var users = (await (listener as IVoiceChannel).GetUsersAsync().FlattenAsync()).Where(u => !u.IsBot);
                    foreach (var user in users)
                    {
                        await ListenUserAsync(user).CopyToAsync(speak);
                    }
                }
                await speak.WriteAsync(new byte[3840], newListener);
                await speaker.SetSpeakingAsync(false);
            }
        }

        private AudioInStream ListenUserAsync(IGuildUser user)
        {
            return (user as SocketGuildUser).AudioStream;
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
