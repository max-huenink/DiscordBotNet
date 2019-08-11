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
                string seconds = ((sw.Elapsed.Seconds < 10) ? "0" : "") + sw.Elapsed.Seconds;
                return $"{days}:{hours}:{minutes}:{seconds}";
            }
        }

        readonly Task syncUptime;
        public IMessageChannel botLogChannel;
        //public IMessageChannel spiritBearLogChannel;

        // Dictionary of servers holding a dictionary of ulong lists for "mute" and "unmute"
        public Dictionary<ulong, Dictionary<string, List<ulong>>> servers = new Dictionary<ulong, Dictionary<string, List<ulong>>>();
        private ulong spiritBearGuildID = 259533308512174081;

        public static Controller Instance;

        static void Main(string[] args) => new Controller().Start().GetAwaiter().GetResult();

        public Controller()
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
                    await Task.Delay(75000);
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
                    servers.TryAdd(guild.Id, new Dictionary<string, List<ulong>>()
                    {
                        { "mute", new List<ulong>() },
                        { "unmute", new List<ulong>() }
                    });
                }
                SetVars();
                await Task.Delay(1);
            };
            client.JoinedGuild += async (guild) =>
             {
                 servers.TryAdd(guild.Id, new Dictionary<string, List<ulong>>()
                    {
                        { "mute", new List<ulong>() },
                        { "unmute", new List<ulong>() }
                    });
                 await Task.Delay(1);
             };
            client.LeftGuild += async (guild) =>
              {
                  servers.Remove(guild.Id);
                  await Task.Delay(1);
              };

            await InstallCommands();
            Init_Events();

            // Tries to login, if it fails the token is probably incorrect (or discord is down)
            try
            {
                await client.LoginAsync(TokenType.Bot, token);
            }
            catch (Exception) // Exits if the token is incorrect
            {
                Console.Clear();
                Console.WriteLine("That token doesn't work, or Discord may be down, please try again.");
                Console.WriteLine("Do you want to reenter your token? y/[n]");
                string correct = Console.ReadLine();
                if (!string.IsNullOrEmpty(correct) && correct.ToLower().First<char>() == 'y')
                {
                    File.Delete(tokenPath);
                }
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

        public async void SetVars()
        {
            //spiritBearLogChannel = client.GetChannel(543571980985565194) as IMessageChannel;
            botLogChannel = client.GetChannel(543875988094844958) as IMessageChannel;
            await Task.Delay(1);
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
            IGuild guild = client.GetGuild(spiritBearGuildID) as IGuild; // Spirit Bear Guild
            ITextChannel announce = await guild.GetSystemChannelAsync(); // Announcement channel
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
            await voice.ModifyAsync((VoiceChannelProperties prop) =>
            {
                prop.Name = $"{winner.Username}\'s Executive Suite";
                prop.Bitrate = 64000;
            }, new RequestOptions { AuditLogReason = "Reset and rename" });

            // Resets permissions to their 'default' values
            await voice.SyncPermissionsAsync(new RequestOptions { AuditLogReason = "Reset permissions" });
            // Edits everyone role permission overwrites
            await voice.AddPermissionOverwriteAsync(everyone,
                new OverwritePermissions(manageChannel: PermValue.Deny, manageRoles: PermValue.Deny, connect: PermValue.Deny, moveMembers: PermValue.Deny),
                new RequestOptions { AuditLogReason = "Reset permissions" });
            // Edits winner role permission overwrites
            await voice.AddPermissionOverwriteAsync(winningRole,
                new OverwritePermissions(connect: PermValue.Allow, moveMembers: PermValue.Allow, manageRoles:PermValue.Allow),
                new RequestOptions { AuditLogReason = "Reset permssions" });

            msg += $"Participants: {string.Join(", ", participants.Select(participant => participant.Username))}\n";
            msg += $"This week's winner is: {winner.Mention}!";

            await (announce as ISocketMessageChannel).SendMessageAsync(msg);
        }
    }
}
