using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace discordBot
{
    class Program
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly IServiceProvider services;
        public Random rand;
        private readonly string tokenPath;
        private readonly string token;
        public readonly Stopwatch sw = new Stopwatch();
        
        // A string that specifies how long the bot has been running based on when it connected
        public string swElapsed
        {
            get
            {
                string days = ((sw.Elapsed.Days < 10) ? "0" : "") + sw.Elapsed.Days;
                string hours = ((sw.Elapsed.Hours < 10) ? "0" : "") + sw.Elapsed.Hours;
                string minutes = ((sw.Elapsed.Minutes < 10) ? "0" : "") + sw.Elapsed.Minutes;
                return $"{days}:{hours}:{minutes}";
            }
        }

        public Dictionary<ulong, List<ulong>> toMuteID = new Dictionary<ulong, List<ulong>>();
        public Dictionary<ulong, List<ulong>> toUnmuteID = new Dictionary<ulong, List<ulong>>();

        public static Program Instance;

        readonly Task syncUptime;
        public IMessageChannel botLogChannel;

        static void Main(string[] args) => new Program().Start().GetAwaiter().GetResult();

        public Program()
        {
            if (Instance == null)
                Instance = this;
            // Sets things needed for Program()
            rand = new Random();
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
                MessageCacheSize = 100,
            });
            commands = new CommandService();
            services = new ServiceCollection().BuildServiceProvider();

            tokenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "token.txt");
            // Checks if the token file exists and either creates the file and asks for token, or reads token from file
            if (!File.Exists(tokenPath))
            {
                Console.WriteLine("What is the bot token?");
                Console.Write("Token: ");
                token = Console.ReadLine();
                Console.Clear();
                // Writes the token to the file
                File.WriteAllText(tokenPath, token);
            }
            else
            {
                // Reads the first line from the token file
                token = File.ReadLines(tokenPath).FirstOrDefault();
            }

            // Sets the task, T, to update the "playing" status with uptime every 45 seconds
            syncUptime = new Task(async () =>
            {
                sw.Start();
                while (true)
                {
                    await UpdateUptime();
                    await Task.Delay(60000);
                }
            });
        }

        public async Task Start()
        {
            // Adds the logger
            client.Log += Logger;
            client.Ready += async () =>
            {
                foreach (var guild in client.Guilds)
                {
                    toMuteID.TryAdd(guild.Id, new List<ulong>());
                    toUnmuteID.TryAdd(guild.Id, new List<ulong>());
                }
                await Task.Delay(1);
            };
            client.JoinedGuild += async (guild) =>
             {
                 toMuteID.Add(guild.Id, new List<ulong>());
                 toUnmuteID.Add(guild.Id, new List<ulong>());
                 await Task.Delay(1);
             };
            client.LeftGuild += async (guild) =>
              {
                  toUnmuteID.Remove(guild.Id);
                  toMuteID.Remove(guild.Id);
                  await Task.Delay(1);
              };
            await InstallCommands();

            // Tries to login, if it fails the token is probably incorrect (or discord is down)
            try
            {
                await client.LoginAsync(TokenType.Bot, token);
            }
            catch (Exception) // Restarts the program if the token is incorrect
            {
                Console.Clear();
                Console.WriteLine("That token doesn't work, or Discord may be down, please try again.");
                File.Delete(tokenPath);
                Console.ReadLine();
                Environment.Exit(1);
            }

            await client.StartAsync();

            await Task.Delay(-1);
        }

        public async Task InstallCommands()
        {
            // Hook the MessageReceived Event into our Command Handler
            client.MessageReceived += HandleCommand;

            client.UserVoiceStateUpdated += MovedMember;
            client.UserJoined += UserJoin;
            client.UserLeft += UserLeft;
            client.MessageUpdated += HandleUpdate;
            client.Ready += SetVars;

            client.Connected += async () =>
            {
                syncUptime.Start();
                await Task.Delay(1);
            };

            //client.MessageDeleted += HandleDelete;

            // Discover all of the commands in this assembly and load them.
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

            MyScheduler.NewTask(new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 6, 0, 0).
                AddDays(7 - (int)DateTime.Now.DayOfWeek), TimeSpan.FromDays(7),
            async () =>
            {
                await RoleLottery();
            }, $"lottery{GetHashCode()}");
        }

        public async Task SetVars()
        {
            botLogChannel = client.GetChannel(538787160674009088) as IMessageChannel;
            await Task.Delay(1);
        }

        public async Task UserJoin(SocketGuildUser user) =>
            await botLogChannel.SendMessageAsync($"`{user.Username}` joined `{user.Guild}`");

        public async Task UserLeft(SocketGuildUser user) =>
            await botLogChannel.SendMessageAsync($"`{user.Username}` left `{user.Guild}`");

        public async Task MovedMember(SocketUser user, SocketVoiceState state1, SocketVoiceState state2)
        {
            IGuildUser guser = user as IGuildUser;
            if (toMuteID.TryGetValue(guser.GuildId, out List<ulong> muteIDs))
                if (muteIDs.Contains(user.Id))
                {
                    await guser.ModifyAsync(properties => properties.Mute = true);
                    muteIDs.Remove(user.Id);
                }
            if (toUnmuteID.TryGetValue(guser.GuildId, out List<ulong> unmuteIDs))
                if(unmuteIDs.Contains(user.Id))
                {
                    await guser.ModifyAsync(properties => properties.Mute = false);
                    unmuteIDs.Remove(user.Id);
                }

            string preText = $"`{user.Username}` ";
            string message = "";

            // If state 1 is null/empty
            if (state1.VoiceChannel == null)
                message = $"joined `{state2.VoiceChannel.Name}` in `{state2.VoiceChannel.Guild}`";

            // If state 2 is null/empty
            else if (state2.VoiceChannel == null)
                message = $"left `{state1.VoiceChannel.Name}` in `{state1.VoiceChannel.Guild}`";

            // If state 1 and state 2 are different
            else if (state1.ToString() != state2.ToString())
            {
                message = $"switched from " +
                $"`{state1.VoiceChannel.Name}` to `{state2.VoiceChannel.Name}` " +
                $"in `{state2.VoiceChannel.Guild.Name}`";
            }

            if (!string.IsNullOrEmpty(message))
                await botLogChannel.SendMessageAsync($"{preText} {message}");
        }

        public async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasStringPrefix("m!", ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            var context = new CommandContext(client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await commands.ExecuteAsync(context, argPos, services);
            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ErrorReason);
            // Update the uptime
            await UpdateUptime();
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

        public async Task UpdateUptime()
        {
            await client.SetGameAsync($"m!help for {swElapsed}", type: ActivityType.Playing);
        }

        private Task Logger(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        public async Task RoleLottery()
        {
            IGuild guild = client.GetGuild(259533308512174081) as IGuild; // Spirit Bear Guild
            IGuildChannel announce = await guild.GetChannelAsync(335460607279235072); // Announcement channel
            IVoiceChannel voice = await guild.GetVoiceChannelAsync(434092857415041024); // The winner's voice channel
            IRole everyone = guild.EveryoneRole; // The everyone role
            IRole participantRole = guild.GetRole(411281455331672064); // The lottery role
            IRole winningRole = guild.GetRole(335456437352529921); // The winning role
            // All users in the guild
            var users = await guild.GetUsersAsync();

            // All possible participants
            IEnumerable<IGuildUser> participants = users.Where(user => user.RoleIds.Any(roleID => roleID == participantRole.Id));
            // Everyone who currently has the winning role
            IEnumerable<IGuildUser> currentWinners = users.Where(user => user.RoleIds.Any(roleID => roleID == winningRole.Id));

            // Removes any current winner from the participants list
            participants.ToList().RemoveAll(participant => currentWinners.Any(currentWinner => participant == currentWinner));

            string msg = "Lottery:\n";

            // Adds who the role was removed from to the message
            msg += $"Took away {string.Join(", ", currentWinners.Select(user => user.Username))}\'s {winningRole.Name}\n";

            // Removes the winning role from anyone who currently has it
            foreach (var user in currentWinners)
                await user.RemoveRoleAsync(winningRole, new RequestOptions { AuditLogReason = $"Previous {winningRole.Name}" });

            // Randomly selects the winner
            IGuildUser winner = participants.ElementAt(rand.Next(0, participants.Count()));

            // Gives the winner their role
            await winner.AddRoleAsync(winningRole, new RequestOptions { AuditLogReason = $"The new {winningRole.Name} is in town" });

            // Edits the winner's voice channel name
            await voice.ModifyAsync((VoiceChannelProperties p) =>
            {
                p.Name = $"{winner.Username}\'s Executive Suite";
                p.Bitrate = 64000;
            }, new RequestOptions { AuditLogReason = "Reset and rename" });

            // Resets permissions to their 'default' values
            await voice.SyncPermissionsAsync(new RequestOptions { AuditLogReason = "Reset permissions" });
            // Edits everyone role permission overwrites
            await voice.AddPermissionOverwriteAsync(everyone,
                new OverwritePermissions(connect: PermValue.Deny, moveMembers: PermValue.Deny),
                new RequestOptions { AuditLogReason = "Reset permissions" });
            // Edits winner role permission overwrites
            await voice.AddPermissionOverwriteAsync(winningRole,
                new OverwritePermissions(connect: PermValue.Allow, moveMembers: PermValue.Allow),
                new RequestOptions { AuditLogReason = "Reset permssions" });

            msg += $"Participants: {string.Join(", ", participants.Select(participant => participant.Username))}\n";
            msg += $"This week's winner is: {winner.Mention}!";

            await (announce as ISocketMessageChannel).SendMessageAsync(msg);
        }
    }
}
