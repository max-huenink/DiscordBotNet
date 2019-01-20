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
            if (content == null) // Exit if there wasn't an argument
                return;

            // Mentioned channel ids
            var channelID = Context.Message.MentionedChannelIds;
            // Mentioned role ids
            var roleID = Context.Message.MentionedRoleIds;
            // Mentioned member ids
            var memberID = Context.Message.MentionedUserIds;

            // Splits the content by spaces
            string[] splitContent = content.Split(new char[] { ' ' });
            // Gets the last argument
            string lastArg = splitContent.LastOrDefault();

            if (string.IsNullOrEmpty(lastArg))
                return;

            // The message author as a guild user
            IGuildUser author = Context.Message.Author as IGuildUser;
            // The destination user
            IGuildUser destUser = null;
            // The destination channel
            IVoiceChannel destChannel = null;

            // Attempts to parse the last argument, removing non-digit chars, to an id
            ulong.TryParse(new string(lastArg.Where(c => char.IsLetterOrDigit(c)).ToArray()), out ulong lastArgID);
            
            // Attempt to find destination user based on lastArgID
            destUser = await Context.Guild.GetUserAsync(lastArgID);
            // Attempt to find destination channel based on lastArgID
            destChannel = await Context.Guild.GetVoiceChannelAsync(lastArgID);

            if (destChannel == null) // If the destination channel is null
            {
                if (destUser == null) // If the destination user is ALSO null, exit
                {
                    await ReplyAsync("No voice channel or user found.");
                    return;
                }
                if (destUser.VoiceChannel == null) // If the destination user is not in a voice channel
                {
                    await ReplyAsync($"Specified user, {destUser.Username}, is not in a voice channel.");
                    return;
                }
                // Destination user must be not null and in a voice channel, set their channel to the destination channel
                destChannel = destUser.VoiceChannel;
            }

            // Potential teleport candidates
            var candidates = await Context.Guild.GetUsersAsync();
            // A list of IGuildUser to teleport
            List<IGuildUser> teleport = new List<IGuildUser>();
            // The afk voice channel of the server
            IVoiceChannel afkChannel = await Context.Guild.GetAFKChannelAsync();

            foreach (ulong id in memberID) // Add every mentioned user to the teleport list
                teleport.Add(await Context.Guild.GetUserAsync(id));

            if (splitContent.Length == 1) // If there was only one argument, add the author to the teleport list
                teleport.Add(author);

            if (IsBotMod(author)) // If the author is a bot mod
            {
                if (splitContent.Length == 1 && destUser==null) // If there wasn't a user specified and there was only one argument, teleport everyone in voice
                    foreach (IGuildUser user in candidates.Where(a => !string.IsNullOrEmpty(a.VoiceSessionId)))
                        teleport.Add(user);

                if (roleID.Count > 0)// If a role was specified, teleport every user with specified role who is in voice
                    foreach (ulong id in roleID)
                        foreach (IGuildUser user in candidates.Where(a => !string.IsNullOrEmpty(a.VoiceSessionId) && a.RoleIds.Any(b => b == id)))
                            teleport.Add(user);
            }

            // Removes duplicates
            teleport = teleport.Distinct().ToList();

            // Removes the destination user and any user in the afk channel from the teleport list
            teleport.RemoveAll(a => a == destUser || a.VoiceChannel == afkChannel || a.VoiceChannel == destChannel);

            if (!author.GetPermissions(destChannel).MoveMembers) // If the author doesn't have move permissions for the destination channel, exit
            {
                await ReplyAsync($"You do not have move permission for channel \"{destChannel.Name}\"");
                return;
            }

            foreach (var user in teleport) // Teleport every user in the list
                await user.ModifyAsync(a => a.Channel = new Optional<IVoiceChannel>(destChannel));

            await ReplyAsync($"Teleported {teleport.Count} user(s) to \"{destChannel.Name}\"");
        }

        [Command("uptime")]
        public async Task Uptime()
        {
            await ReplyAsync($"I have been running for {Program.swElapsed}.");
        }

        [Command("help")]
        public async Task Help()
        {
            await ReplyAsync($"Here is a list of available commands:\n" +
                $"\tm!tp\n\t\tMoves the user to the specified user or voice channel, specify a voice channel by its ID" +
                $"\tm!uptime\n\t\tReports how long the bot has been running" +
                $"\tm!help\n\t\tShows this help dialog.");
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
            Environment.Exit(0);
        }

        [Command("repeat")]
        public async Task Repeat([Remainder]string input)
        {
            await ReplyAsync($"{Context.Message.Author.Mention} said: {input}");
        }
    }
}
