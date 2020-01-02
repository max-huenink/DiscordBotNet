using Discord;
using Discord.Commands;
using Discord.WebSocket;
using discordBot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace discordBot
{
    class VoiceCommands : ModuleBase
    {
        private AudioService Service => Controller.Instance.Audio;

        [Command("relay", RunMode = RunMode.Async)]
        public async Task VoiceRelay()
        {
            var vc = (Context.User as IVoiceState).VoiceChannel;
            if (vc == null)
            {
                await ReplyAsync($"{Context.User.Username} is not in a voice channel.");
                return;
            }
            await Service.JoinAudio(Context.Guild, vc);
        }

        [Command("leave", RunMode = RunMode.Async)]
        public async Task LeaveVoice()
        {
            await Service.LeaveAudio(Context.Guild);
        }
    }
}
