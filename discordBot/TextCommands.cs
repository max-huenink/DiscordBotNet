using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace discordBot
{
    [CheckIgnore()]
    [AmBot()]
    public class TextCommands : ModuleBase
    {
        [Command("remind")]
        public async Task Remind(string time, [Remainder]string reminder = "nothing")
        {
            if (string.IsNullOrEmpty(time)) // Exit if there wasn't an argument
                return;

            int seconds = 0;

            char modifier = time.FirstOrDefault(character => char.IsLetter(character));
            if (!int.TryParse(string.Join("", time.Where(character => char.IsDigit(character))), out seconds))
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
                case 's':
                case default(char):
                    break;
                default:
                    seconds = 10;
                    break;
                    // seconds and default keep seconds as seconds
            }

            if (seconds <= 0) // If seconds are negative, set default to one hour
                seconds = 60;

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

            string message = $"Hey! Listen! {Context.Message.Author.Mention}. " +
                $"You asked me to remind you about \"{reminder}\", here's your reminder!";

            DateTime remindTime = DateTime.Now.AddDays(days).
                AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);

            MyScheduler.RunOnce(remindTime,
                async () =>
                {
                    await ReplyAsync(message);
                });

            await ReplyAsync($"Okay, I will remind you about \"{reminder}\" " +
                $"in {days} days, {hours} hours, {minutes} minutes, and {seconds} seconds.");
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
            await ReplyAsync($"I have been running for {Controller.Instance.swElapsed}.");
        }

        [Command("sl")]
        public async Task SL()
        {
            await ReplyAsync(embed: new EmbedBuilder().WithImageUrl("https://d2m4k8kmjwceyr.cloudfront.net/app/uploads/2019/01/sl.gif").Build());
        }

        [Command("roll")]
        public async Task Roll(int sides = 6)
        {
            int die1 = Controller.Instance.rand.Next(1, sides);
            int die2 = Controller.Instance.rand.Next(1, sides);

            await ReplyAsync($"{Context.User.Username} rolled a {die1} and a {die2}.");
            if (die1 == die2 && !(Context.Channel is IDMChannel))
            {
                BotUsers.IgnoredUsers.Add(Context.User.Id);
                await ReplyAsync($"Hey {Context.User.Mention} you will be muted for 30 seconds, " +
                    $"and your commands ignored.");

                IGuildUser guser = Context.Message.Author as IGuildUser;
                if (guser.VoiceChannel == null)
                {
                    if (Controller.Instance.servers.TryGetValue(guser.GuildId, out Dictionary<string, List<ulong>> serverMembers))
                        serverMembers["mute"].Add(guser.Id);
                }
                else
                    await guser.ModifyAsync(properties => properties.Mute = true);
                MyScheduler.RunOnce(DateTime.Now.AddSeconds(30),
                    async () =>
                    {
                        if (guser.VoiceChannel == null)
                        {
                            // If they haven't been muted yet, remove them from the 'to mute' list and don't add to 'to unmute' list
                            if (Controller.Instance.servers.TryGetValue(guser.GuildId, out Dictionary<string, List<ulong>> serverMembers))
                                // If the user wasn't removed from the list then they were already muted, so add to unmute list
                                if (!serverMembers["mute"].Remove(guser.Id))
                                    serverMembers["unmute"].Add(guser.Id);
                        }
                        else
                            await guser.ModifyAsync(properties => properties.Mute = false);

                        BotUsers.IgnoredUsers.Remove(Context.User.Id);
                        await ReplyAsync($"Hey {Context.User.Mention}, you are now unmuted and I will listen to your commands.");
                    });
            }
        }

        [Command("echo")]
        public async Task Echo([Remainder]string input)
        {
            await ReplyAsync(input);
        }

        [Command("help")]
        public async Task Help()
        {
            await ReplyAsync($"Here is a list of available commands:\n" +
                $"m!remind `integer``{{w,d,h,m,s}}` `message`\n" +
                    $"\tReminds you about `message` after specified length of time.\n" +
                    $"\tFor example: `m!remind 11h Hello world` will send `Hello world` after 11 hours.\n" +
                $"\nm!tp\n\tMoves the user to the specified user or voice channel, specify a voice channel by its ID\n" +
                    $"\tFor example: `m!tp @user1 @user2` will move `user1` to `user2`'s voice channel.\n" +
                $"\nm!roll `sides`\n" +
                    $"\tRolls two dice with `sides` number of sides (default 6)\n" +
                    $"\tIf you roll doubles your commands are ignored and you are voice muted for 5 minutes\n" +
                $"\nm!echo `message`\n" +
                    $"\tEchos your message\n" +
                $"\nm!uptime\n" +
                    $"\tReports how long the bot has been running\n" +
                $"\nm!help\n" +
                    $"\tShows this help dialog.");
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
            IGuild guild = Context.Guild;
            if (guild == null) { return; }
            await ReplyAsync(
                "Roles:\n" +
                guild.Roles.OrderByDescending(p => p.Position).
                    Aggregate(string.Empty, (text, role) => text += $"{role.Position} {role.Name}: {role.Id}\n"));
        }

        [Command("roleperms")]
        public async Task GetRolePermissions()
        {
            await ReplyAsync("Not implemented");
            /*
            IGuild guild = Context.Guild;
            if (guild == null) { return; }
            IOrderedEnumerable<IRole> ordered = guild.Roles.OrderByDescending(p => p.Position);
            string message = "Roles & Permissions:";
            foreach (IRole role in ordered)
            {
                message += $"{role.Position} {role.Name}\n" +
                    $"\tView Audit Log: {role.Permissions.ViewAuditLog}\n" +
                    $"\tManage Roles: {role.Permissions.ManageRoles}\n" +
                    $"\tManage Channels: {role.Permissions.ManageChannels}\n" +
                    $"\tManage Nicknames: {role.Permissions.ManageNicknames}\n" +
                    $"\tSend TTS: {role.Permissions.SendTTSMessages}\n" +
                    $"\tManage Messages: {role.Permissions.ManageMessages}\n" +
                    $"\tMention Everyone: {role.Permissions.MentionEveryone}\n" +
                    $"\tMute Members: {role.Permissions.MuteMembers}\n" +
                    $"\tDeafen Members: {role.Permissions.DeafenMembers}\n" +
                    $"\tPriority Speaker: {role.Permissions.PrioritySpeaker}\n";
                await ReplyAsync(message);
                message = "";
            }
            */
        }

        [Command("servers")]
        public async Task GetServers()
        {
            await ReplyAsync(
                "Servers:\n" +
                (Context.Client as DiscordSocketClient).Guilds.Aggregate(string.Empty, (text, guild) => text += $"{guild}\n"));
        }

        [Command("help")]
        public async Task Help()
        {
            await ReplyAsync($"Available debug commands:\n" +
                $"m!debug roles\n" +
                    $"\tLists all roles with id in the hierarchy order\n" +
                $"m!debug roleperms\n" +
                    $"\tLists all roles and what permissions they have" +
                $"\nm!debug servers\n" +
                    $"\tLists all servers this part is in\n" +
                $"\nm!debug help\n" +
                    $"\tDisplays this help" +
                $"\nm!debug owner\n" +
                    $"\tAre you the owner?");
        }
    }

    //[Group("owner")]
    [RequireBotOwner()]
    public class OwnerCommands : ModuleBase
    {
        [Command("reroll_lottery")]
        public async Task ReRollLotto()
        {
            await Controller.Instance.RoleLottery();
            await ReplyAsync("Rerolled lottery.");
        }

        [Command("unmute")]
        public async Task Unmute()
        {
            BotUsers.IgnoredUsers.Clear();
            await ReplyAsync("All ignored users are now un-ignored.");
        }

        [Command("download")]
        public async Task Download([Remainder]string args = "")
        {
            var channelIDs = Context.Message.MentionedChannelIds;
            var channels = new List<ITextChannel>();
            if (args.Length > 0 && args.Split(new char[] { ' ' })[0] == "all")
            {
                // Assume downloading all channels
                channels.AddRange(await Context.Guild.GetTextChannelsAsync());
            }
            else if (channelIDs.Count == 0)
            {
                // No channel was specified, download the contents of the current channel
                channels.Add((ITextChannel)Context.Channel);
            }
            else
            {
                // Download contents of all mentioned channels
                foreach (var channelID in channelIDs)
                {
                    channels.Add(await Context.Guild.GetTextChannelAsync(channelID));
                }
            }
            foreach (var channel in channels)
            {
                var messages = await channel.GetMessagesAsync(int.MaxValue,
                    options: new RequestOptions
                    {
                        RetryMode = RetryMode.RetryRatelimit,
                        AuditLogReason = "Because I can",
                    }).FlattenAsync();

                List<string> text = new List<string>();
                foreach (IMessage message in messages.OrderBy(t => t.Timestamp))
                {
                    string content = message.Content;
                    if (message is IUserMessage msg)
                    {
                        content = msg.Resolve(everyoneHandling: TagHandling.Name);
                    }
                    text.Add($"[{message.Timestamp}] " +
                        $"{message.Author.Username}: " +
                        $"{content} " +
                        $"{(message.Attachments.Count > 0 ? message.Attachments.Aggregate("", (e, a) => $"{e}{Environment.NewLine}{a.Url}") : string.Empty)}");
                }
                string filePath = Path.Combine(new string[]
                    {
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Servers",
                        Context.Guild.Name,
                        "Channels",
                        channel.Name,
                        $"messageDownload_{DateTime.Now.ToString("yyyyMMdd")}_{DateTime.Now.Ticks}.txt",
                    });
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                await File.WriteAllLinesAsync(filePath, text);
                await ReplyAsync($"Downloaded history of {channel.Name} to {Path.GetFileName(filePath)}");
            }
            await ReplyAsync("Done");
        }

        [Command("wordCloud")]
        public async Task WordCloud(string textChannel = "", bool doCount = false)
        {
            ITextChannel channel = (ITextChannel)Context.Channel;

            var channelIDs = Context.Message.MentionedChannelIds;
            if (channelIDs.Count == 1)
            {
                foreach (var channelID in channelIDs)
                {
                    channel = await Context.Guild.GetTextChannelAsync(channelID);
                }
            }
            var messages = await channel.GetMessagesAsync(int.MaxValue,
                options: new RequestOptions
                {
                    RetryMode = RetryMode.RetryRatelimit,
                    AuditLogReason = "Because I can",
                }).FlattenAsync();

            List<string> text = new List<string>();
            foreach (IMessage message in messages.OrderBy(t => t.Timestamp))
            {
                if (message is IUserMessage msg)
                {
                    text.Add(msg.Resolve(everyoneHandling: TagHandling.Name));
                }
                else
                {
                    text.Add(message.Content);
                }
            }
            string filePath = Path.Combine(new string[]
                {
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Servers",
                        Context.Guild.Name,
                        "Channels",
                        channel.Name,
                        $"messageDownload_{DateTime.Now.ToString("yyyyMMdd")}.txt",
                });
            if (!doCount)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                await File.WriteAllTextAsync(filePath, string.Join(' ', text));
                await ReplyAsync($"Downloaded words in {channel.Name} to {Path.GetFileName(filePath)}");
            }
            else
            {
                Dictionary<string, int> words = new Dictionary<string, int>();
                IEnumerable<string> allText = string.Join(' ', text).Split(' ');
                foreach (string Word in allText)
                {
                    string word = Word.ToLower().Trim();
                    if (!words.TryAdd(word, 1))
                    {
                        words[word]++;
                    }
                }
                int dispCount = 0;
                string dispMsg = string.Empty;
                List<string> printTxt = new List<string>();
                foreach (var kvp in words.OrderByDescending(i=>i.Value))
                {
                    string word = kvp.Key;
                    if (word.Contains("http") && word.Contains("://"))
                    {
                        word = $"<{word}>";
                    }
                    dispMsg += $"{word}: {kvp.Value}\n";
                    printTxt.Add($"{word}: {kvp.Value}");
                    //dispCount++;
                    //if (dispCount > 40)
                    //{
                    //    await ReplyAsync(dispMsg);
                    //    dispCount = 0;
                    //    dispMsg = string.Empty;
                    //}
                }
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                await File.WriteAllLinesAsync(filePath, printTxt);
                await ReplyAsync($"Downloaded words in {channel.Name} to {Path.GetFileName(filePath)}");
            }
        }

        [Command("count")]
        public async Task Count()
        {
            var messages = await Context.Channel.GetMessagesAsync(int.MaxValue,
                    options: new RequestOptions
                    {
                        RetryMode = RetryMode.RetryRatelimit,
                        AuditLogReason = "Because I can",
                    }).FlattenAsync();
            int allMessages = messages.Count();
            var users = await Context.Channel.GetUsersAsync().FlattenAsync();
            string msg = $"Total messages: {allMessages}\n";
            foreach (var user in users)
            {
                int userMessages = messages.Count(i => i.Author.Id == user.Id);
                if (userMessages > 300)
                {
                    msg += $"{user.Username} has sent {userMessages} / {allMessages} or {100f * ((float)userMessages / (float)allMessages)} % of the messages in this channel.\n";
                }
            }
            await ReplyAsync(msg);
        }

        [Command("exit")]
        public async Task Exit()
        {
            await ReplyAsync("Exiting...");
            await Context.Client.StopAsync();
            Environment.Exit(0);
        }

        [Command("help")]
        public async Task OwnerHelp()
        {
            await ReplyAsync($"Owner commands:\n" +
                $"m!owner reroll_lottery\n" +
                    $"\tRerolls the spirit bear lottery\n" +
                $"\nm!owner unmute\n" +
                    $"\tRemoves all users from the ignore list\n" +
                $"\nm!owner download [`channel`|all]\n" +
                    $"\tDownloads all messages in all channels, specified channel(s), or current channel\n" +
                $"\nm!owner count [`channel`]\n" +
                    $"\tCounts how many messages have been sent by each user in the current or specified channel" +
                $"\nm!owner exit\n" +
                    $"\tExits the bot safely\n" +
                $"\nm!owner help\n" +
                    $"\tDisplays this help text");
        }
    }
}
