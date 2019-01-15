using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.API;
using System.Threading.Tasks;

namespace discordBot
{
    public class RequireBotMod : PreconditionAttribute
    {
        protected readonly static ulong[] BotMods = new ulong[] { 259532984909168659, 212687824816701441 };
        protected bool IsBotMod(IUser user) => BotMods.Contains(user.Id);

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services) =>
            Task.Run(() => IsBotMod(context.User) ?
                PreconditionResult.FromSuccess() :
                PreconditionResult.FromError($"{context.User.Username} is not a bot mod"));
    }

    public abstract class Commands : ModuleBase
    {
        protected readonly static ulong[] BotMods = new ulong[] { 259532984909168659, 212687824816701441 };
        protected bool IsBotMod(IUser user) => BotMods.Contains(user.Id);
    }

    public class TextCommands : Commands
    {
        [Command("tp")]
        public async Task TP([Remainder]string content)
        {
            if (content == null)
                return;

            var channelID = Context.Message.MentionedChannelIds;
            var roleID = Context.Message.MentionedRoleIds;
            var memberID = Context.Message.MentionedUserIds;

            string[] splitContent = content.Split(new char[] { ' ' });
            string lastArg = splitContent.LastOrDefault();
            char lastArgType = lastArg.FirstOrDefault(a => a == '#' || a == '@');

            IGuildUser destUser = null;
            IVoiceChannel destChannel = null;
            
            ulong lastArgID = 0;
            if (!ulong.TryParse(lastArg.Remove(lastArg.Length - 1).Remove(0, 2), out lastArgID))
            {
                if (!ulong.TryParse(lastArg, out lastArgID))
                {
                    await ReplyAsync("Couldn't find the id.");
                    return;
                }
            }

            if (string.IsNullOrEmpty(lastArgType.ToString())) // assume just id
            {
                await ReplyAsync("Can't work with just an id");
                return;
            }
            else if (lastArgType == '@') // member
            {
                destUser = await Context.Guild.GetUserAsync(lastArgID);
            }
            else if (lastArgType == '#') // channel
            {
                destChannel = await Context.Guild.GetVoiceChannelAsync(lastArgID);
            }

            if (destChannel == null)
            {
                if (destUser == null)
                {
                    await ReplyAsync("No voice channel or user found");
                    return;
                }
                if (destUser.VoiceChannel == null)
                {
                    await ReplyAsync("User is not in voice");
                    return;
                }

                destChannel = destUser.VoiceChannel;
            }

            foreach (ulong id in memberID)
            {
                IGuildUser user = await Context.Guild.GetUserAsync(id);
                await user.ModifyAsync(a => a.Channel = new Optional<IVoiceChannel>(destChannel));
            }
            if (IsBotMod(Context.Message.Author))
            {
                var users = await Context.Guild.GetUsersAsync();
                if (splitContent.Length == 1)
                {
                    foreach (IGuildUser user in users)
                    {
                        if (string.IsNullOrEmpty(user.VoiceSessionId))
                            continue;
                        await user.ModifyAsync(b => b.Channel = new Optional<IVoiceChannel>(destChannel));
                    }
                    return;
                }
                if (roleID.Count >= 0)
                {
                    foreach (ulong id in roleID)
                    {
                        foreach (IGuildUser user in users)
                        {
                            if (string.IsNullOrEmpty(user.VoiceSessionId))
                                continue;
                            if (user.RoleIds.Any(a => a == id))
                                await user.ModifyAsync(b => b.Channel = new Optional<IVoiceChannel>(destChannel));
                        }
                    }
                }
                return;
            }
            await ReplyAsync("You are not a bot mod.");
        }
    }

    [Group("debug")]
    [RequireBotMod()]
    public class DebugCommands : Commands
    {
        [Command("roles")]
        public async Task GetRoles()
        {
            IGuild g = Context.Guild;
            if (g == null) { return; }
            await ReplyAsync(
                "Roles:\n" +
                g.Roles.OrderByDescending(p => p.Position).
                    Aggregate<IRole, string>(string.Empty, (a, b) => (a += $"{b.Position} {b.Name}: {b.Id}\n")));
        }

        [Command("echo")]
        public async Task Echo([Remainder]string input)
        {
            await ReplyAsync(input);
        }

        [Command("exit")]
        public async Task Exit()
        {
            await Context.Client.StopAsync();
            await Exit();
        }
    }
}
