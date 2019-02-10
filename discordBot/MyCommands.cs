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
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services) =>
            Task.Run(() => BotUsers.IsBotMod(context.User) ?
                PreconditionResult.FromSuccess() :
                PreconditionResult.FromError($"{context.User.Username} is not a bot mod"));
    }

    public class CheckIgnore : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services) =>
            Task.Run(() => BotUsers.IsIgnored(context.User) ?
                //PreconditionResult.FromError($"{context.User.Username} is an ignored user") :
                PreconditionResult.FromError("Did you hear something?") :
                PreconditionResult.FromSuccess());
    }

    public static class BotUsers
    {
        public readonly static ulong Owner = 259532984909168659;
        public readonly static ulong[] Mods = new ulong[] { Owner, 212687824816701441 };
        public static bool IsBotMod(IUser user) => Mods.Contains(user.Id);

        public static List<ulong> IgnoredUsers = new List<ulong>();
        public static bool IsIgnored(IUser user) => IgnoredUsers.Contains(user.Id);
    }

    [CheckIgnore()]
    public class TextCommands : ModuleBase
    {
        [Command("remind")]
        public async Task Remind([Remainder]string input)
        {
            if (input == null) // Exit if there wasn't an argument
                return;
            string[] splitContent = input.Split(new char[] { ' ' });

            int seconds = 0;

            char modifier = splitContent[0].FirstOrDefault(character => char.IsLetter(character));
            if (!int.TryParse(string.Join("", splitContent[0].Where(character => char.IsDigit(character))), out seconds))
            {
                seconds = 3600;
                modifier = 's';
            }

            switch (modifier)
            {
                case 'w': // week
                    seconds *= 604800;
                    break;
                case 'd': // day
                    seconds *= 86400;
                    break;
                case 'h': // hour
                    seconds *= 3600;
                    break;
                case 'm': // minute
                    seconds *= 60;
                    break;
                // seconds and default keep seconds as seconds
            }

            if (seconds <= 0) // If seconds are negative, set default to one hour
                seconds = 3600;

            int minutes = 0;
            int hours = 0;
            int days = 0;

            if (seconds >= 60)
            {
                minutes = seconds / 60;
                seconds %= 60;
            }
            if (minutes >= 60)
            {
                hours = minutes / 60;
                minutes %= 60;
            }
            if (hours >= 24)
            {
                days = hours / 24;
                hours %= 24;
            }

            string reminder = "nothing";
            if (splitContent.Length > 1)
                reminder = string.Join(' ', splitContent.AsSpan(1).ToArray());

            string message = $"Hey! Listen! {Context.Message.Author.Mention}." +
                $"You asked me to remind you about \"{reminder}\", here's your reminder!";

            DateTime remindTime = DateTime.Now.AddDays(days).
                AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);

            MyScheduler.RunOnce(remindTime,
                async () =>
                {
                    await ReplyAsync(message);
                });

            await ReplyAsync($"Okay, I will remind you about \"{reminder}\"" +
                $"in {days} days, {hours} hours, {minutes} minutes, and {seconds} seconds\nTotal: {seconds}");
        }

        [Command("tp")]
        public async Task TP([Remainder]string content)
        {
            if (content == null) // Exit if there wasn't an argument
                return;
            if (Context.Guild == null) // Checks if the message was sent in a guild
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
            ulong.TryParse(new string(lastArg.Where(character => char.IsLetterOrDigit(character)).ToArray()), out ulong lastArgID);

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

            if (BotUsers.IsBotMod(author)) // If the author is a bot mod
            {
                if (splitContent.Length == 1 && destUser == null) // If there wasn't a user specified and there was only one argument, teleport everyone in voice
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
            teleport.RemoveAll(user => user == destUser || user.VoiceChannel == afkChannel || user.VoiceChannel == destChannel);

            if (!author.GetPermissions(destChannel).MoveMembers) // If the author doesn't have move permissions for the destination channel, exit
            {
                await ReplyAsync($"You do not have move permission for channel \"{destChannel.Name}\"");
                return;
            }

            foreach (var user in teleport) // Teleport every user in the list
                await user.ModifyAsync(userVoiceProperty => userVoiceProperty.Channel = new Optional<IVoiceChannel>(destChannel));

            await ReplyAsync($"Teleported {teleport.Count} user(s) to \"{destChannel.Name}\"");
        }

        [Command("uptime")]
        public async Task Uptime()
        {
            await ReplyAsync($"I have been running for {Program.Instance.swElapsed}.");
        }

        [Command("sl")]
        public async Task SL()
        {
            await ReplyAsync(embed: new EmbedBuilder().WithImageUrl("https://d2m4k8kmjwceyr.cloudfront.net/app/uploads/2019/01/sl.gif").Build());
        }

        [Command("roll")]
        public async Task Roll()
        {
            int die1 = Program.Instance.rand.Next(1, 6);
            int die2 = Program.Instance.rand.Next(1, 6);

            await ReplyAsync($"{Context.User.Username} rolled a {die1} and a {die2}.");
            if (die1 == die2)
            {
                BotUsers.IgnoredUsers.Add(Context.User.Id);
                await ReplyAsync($"Hey {Context.User.Mention} you will be muted for 30 seconds, " +
                    $"and your commands ignored.");

                IGuildUser guser = Context.Message.Author as IGuildUser;
                if (guser.VoiceChannel == null)
                {
                    if (Program.Instance.toMuteID.TryGetValue(guser.GuildId, out List<ulong> muteIDs))
                        muteIDs.Add(guser.Id);
                }
                else
                    await guser.ModifyAsync(properties => properties.Mute = true);
                MyScheduler.RunOnce(DateTime.Now.AddSeconds(30),
                    async () =>
                    {
                        if (guser.VoiceChannel == null)
                        {
                            // If they haven't been muted yet, remove them from the 'to mute' list and don't add to 'to unmute' list
                            if (Program.Instance.toMuteID.TryGetValue(guser.GuildId, out List<ulong> muteIDs))
                                muteIDs.Remove(guser.Id);
                            else if (Program.Instance.toUnmuteID.TryGetValue(guser.GuildId, out List<ulong> unmuteIDs))
                                unmuteIDs.Add(guser.Id);
                        }
                        else
                            await guser.ModifyAsync(properties => properties.Mute = false);

                        BotUsers.IgnoredUsers.Remove(Context.User.Id);
                        await ReplyAsync($"It's been five minutes {Context.User.Mention}.\n" +
                            $"You are now unmuted and I will listen to your commands.");
                    });
            }
        }

        [Command("help")]
        public async Task Help()
        {
            await ReplyAsync($"Here is a list of available commands:\n" +
                $"m!remind `integer``{{w,d,h,m,s}}` `message`\n\tReminds you about `message` after specified length of time.\n" +
                $"\tFor example: `m!remind 11h Hello world` will send `Hello world` after 11 hours.\n" +
                $"m!tp\n\tMoves the user to the specified user or voice channel, specify a voice channel by its ID\n" +
                $"\tFor example: `m!tp @user1 @user2` will move `user1` to `user2`'s voice channel.\n" +
                $"m!roll\n\tRolls two dice, if you roll doubles your commands are ignored and you are server muted for 5 minutes" +
                $"m!uptime\n\tReports how long the bot has been running\n" +
                $"m!help\n\tShows this help dialog.");
        }
    }

    [Group("debug")]
    [RequireBotMod()]
    public class DebugCommands : ModuleBase
    {
        /*
        [Command("schedule")]
        public async Task Schedule([Remainder]string input)
        {
            if (input == null) // Exit if there wasn't an argument
                return;
            string[] splitContent = input.Split(new char[] { ' ' });


        }
        */
        [Command("roles")]
        public async Task GetRoles()
        {
            IGuild g = Context.Guild;
            if (g == null) { return; }
            await ReplyAsync(
                "Roles:\n" +
                g.Roles.OrderByDescending(p => p.Position).
                    Aggregate(string.Empty, (a, b) => (a += $"{b.Position} {b.Name}: {b.Id}\n")));
        }

        [Command("echo")]
        public async Task Echo([Remainder]string input)
        {
            await ReplyAsync(input);
        }

        [Command("exit")]
        public async Task Exit()
        {
            // Don't exit unless the author of the command is the Bot Owner
            if (Context.Message.Author.Id != BotUsers.Owner)
                return;

            await Context.Client.StopAsync();
            Environment.Exit(0);
        }
    }
}