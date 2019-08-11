using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace discordBot
{
    partial class Controller
    {

        public static Dictionary<ulong, ulong> associations = new Dictionary<ulong, ulong>()
            {
                {351872239316238338, 610188130234007553},
                {351874146147500034, 610188147078463519},
                {351874163104808960, 610188161028456522},
            };

        public RequestOptions channelAssoc = new RequestOptions { AuditLogReason = "Voice & Text Channel Associations" };

        public void Init_Events()
        {
            client.UserVoiceStateUpdated += VoiceStateChangeForLogging;
            client.UserVoiceStateUpdated += VoiceStateChangeForText;
            client.UserJoined += UserJoinGuild;
            client.UserLeft += UserLeftGuild;
            //client.MessageUpdated += HandleUpdate;

            client.Connected += async () =>
            {
                syncUptime.Start();
                await Task.Delay(1);
            };
            client.ChannelUpdated += ChannelUpdateForText;
        }

        public async Task UserJoinGuild(SocketGuildUser user)
        {
            await botLogChannel.SendMessageAsync($"`{user.Username}` joined `{user.Guild}`");
            /*
            if (user.Guild.Id == spiritBearGuildID)
                await spiritBearLogChannel.SendMessageAsync($"`{user.Username}` joined the server");
            */
        }

        public async Task UserLeftGuild(SocketGuildUser user)
        {
            await botLogChannel.SendMessageAsync($"`{user.Username}` left `{user.Guild}`");
            /*
            if (user.Guild.Id == spiritBearGuildID)
                await spiritBearLogChannel.SendMessageAsync($"`{user.Username}` left the server");
            */
        }

        public async Task VoiceStateChangeForLogging(SocketUser user, SocketVoiceState state1, SocketVoiceState state2)
        {
            IGuildUser guser = user as IGuildUser;
            if (servers.TryGetValue(guser.GuildId, out Dictionary<string, List<ulong>> serverUser))
            {
                if (serverUser["mute"].Contains(user.Id))
                {
                    await guser.ModifyAsync(properties => properties.Mute = true);
                    serverUser["mute"].Remove(user.Id);
                }
                if (serverUser["unmute"].Contains(user.Id))
                {
                    await guser.ModifyAsync(properties => properties.Mute = false);
                    serverUser["unmute"].Remove(user.Id);
                }
            }

            string preText = $"`{user.Username}` ";
            string message = "";

            // If state 1 is null/empty
            if (state1.VoiceChannel == null)
                message = $"joined `{state2.VoiceChannel.Name}`";

            // If state 2 is null/empty
            else if (state2.VoiceChannel == null)
                message = $"left `{state1.VoiceChannel.Name}`";

            // If state 1 and state 2 are different
            else if (state1.ToString() != state2.ToString())
            {
                message = $"switched from " +
                $"`{state1.VoiceChannel.Name}` to `{state2.VoiceChannel.Name}`";
            }
            if (!string.IsNullOrEmpty(message))
            {
                /*
                if (guser.GuildId == spiritBearGuildID)
                {
                    await spiritBearLogChannel.SendMessageAsync($"{preText} {message}.");
                }
                */
                message += $" in `{guser.Guild.Name}`";
                await botLogChannel.SendMessageAsync($"{preText} {message}.");
            }
        }

        public async Task VoiceStateChangeForText(SocketUser user, SocketVoiceState state1, SocketVoiceState state2)
        {
            IGuildUser guser = user as IGuildUser;

            if (state1.ToString() != state2.ToString())
            {
                ITextChannel leaveChannel = await GetAssocChannel((state1 as IVoiceState).VoiceChannel);
                ITextChannel joinChannel = await GetAssocChannel((state2 as IVoiceState).VoiceChannel);

                if (leaveChannel != null)
                {
                    await leaveChannel.RemovePermissionOverwriteAsync(user, channelAssoc);
                }
                if (joinChannel != null)
                {
                    await joinChannel.AddPermissionOverwriteAsync(user,
                                         new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow),
                                         channelAssoc);
                }
            }
        }

        public async Task ChannelUpdateForText(SocketChannel channel1, SocketChannel channel2)
        {
            IVoiceChannel vc1 = channel1 as IVoiceChannel;
            IVoiceChannel vc2 = channel2 as IVoiceChannel;
            if (vc1 != null && vc2 != null &&
                vc1.Name != vc2.Name)
            {
                ITextChannel assoc = await GetAssocChannel(vc1);
                if (assoc != null)
                {
                    await assoc.ModifyAsync((TextChannelProperties tcp) =>
                    {
                        tcp.Name = vc2.Name;
                    }, channelAssoc);
                }
            }
        }

        public async Task<ITextChannel> GetAssocChannel(IVoiceChannel state)
        {
            if (state == null)
                return null;

            IGuild guild = state.Guild;
            if (associations.TryGetValue(state.Id, out ulong textChannelID))
            {
                return await guild.GetTextChannelAsync(textChannelID);
            }
            return null;
        }

        public async Task HandleUpdate(Cacheable<IMessage, ulong> cacheMsg, SocketMessage message, ISocketMessageChannel channel)
        {
            await HandleCommand(message);
        }

        public async Task HandleDelete(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            IMessage msg = await message.GetOrDownloadAsync();
            await botLogChannel.SendMessageAsync($"The deleted message was from {msg.Author} and was: ```{msg.Content}```");
        }

    }
}
