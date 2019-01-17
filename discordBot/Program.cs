using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace discordBot
{
    class Program
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly IServiceProvider services;
        private Random rand;

        static void Main(string[] args) => new Program().Start(args).GetAwaiter().GetResult();

        public Program()
        {
            rand = new Random();
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
                //MessageCacheSize = 20,
            });
            commands = new CommandService();
            services = new ServiceCollection().BuildServiceProvider();
        }

        public async Task Start(string[] args)
        {
            // Adds the logger
            client.Log += Logger;

            string arg = string.Empty;
            if (args.Length > 0)
            {
                arg = args[0];
            }
            // If there is no argument, install commands and run like normal
            if (string.IsNullOrEmpty(arg))
            {
                await InstallCommands();
            }
            // If "sbRoleLottery" is the first argument, run the lottery instead of normal operation
            else if (arg == "sbRoleLottery")
            {
                client.Ready += RoleLottery;
            }

            await client.LoginAsync(TokenType.Bot, "MzgzMzA5MDU1Mzk0ODQwNTc3.DPkc0g.ehU_ZfUQpxe9wt83nimB-_d3LX0");
            await client.StartAsync();

            await Task.Delay(-1);
        }

        public async Task RoleLottery()
        {
            IGuild g = client.GetGuild(259533308512174081) as IGuild; // Spirit Bear Guild
            IGuildChannel c = await g.GetChannelAsync(335460607279235072); // Announcement channel
            IVoiceChannel v = await g.GetVoiceChannelAsync(434092857415041024); // The winner's voice channel
            IRole e = g.EveryoneRole; // The everyone role
            IRole l = g.GetRole(411281455331672064); // The lottery role
            IRole w = g.GetRole(335456437352529921); // The winning role
            // All users in the guild
            var users = await g.GetUsersAsync();

            // All possible participants
            IEnumerable<IGuildUser> participants = users.Where(a => a.RoleIds.Any(b => b == l.Id));
            // Everyone who currently has the winning role
            IEnumerable<IGuildUser> currentWinners = users.Where(a => a.RoleIds.Any(b => b == w.Id));
            
            // Removes any current winner from the participants list
            participants.ToList().RemoveAll(a => currentWinners.Any(b => a == b));

            string msg = "Lottery:\n";
            
            // Adds who the role was removed from to the message
            msg += $"Took away {string.Join(", ", currentWinners.Select(a => a.Username))}\'s {w.Name}\n";

            // Removes the winning role from anyone who currently has it
            foreach (var user in currentWinners)
                await user.RemoveRoleAsync(w, new RequestOptions { AuditLogReason = $"Previous {w.Name}" });

            // Randomly selects the winner
            IGuildUser winner = participants.ElementAt(rand.Next(0, participants.Count()));

            // Gives the winner their role
            await winner.AddRoleAsync(w, new RequestOptions { AuditLogReason = $"The new {w.Name} is in town" });

            // Edits the winner's voice channel name
            await v.ModifyAsync((VoiceChannelProperties p) =>
            {
                p.Name = $"{winner.Username}\'s Executive Suite";
                p.Bitrate = 64000;
            }, new RequestOptions { AuditLogReason = "Reset and rename" });

            // Resets permissions to their 'default' values
            await v.SyncPermissionsAsync(new RequestOptions { AuditLogReason = "Reset permissions" });
            // Edits everyone role permission overwrites
            await v.AddPermissionOverwriteAsync(e,
                new OverwritePermissions(connect: PermValue.Deny, moveMembers: PermValue.Deny),
                new RequestOptions { AuditLogReason = "Reset permissions" });
            // Edits winner role permission overwrites
            await v.AddPermissionOverwriteAsync(w,
                new OverwritePermissions(connect: PermValue.Allow, moveMembers: PermValue.Allow),
                new RequestOptions { AuditLogReason = "Reset permssions" });

            msg += $"Participants: {string.Join(", ",participants.Select(a=>a.Username))}\n";
            msg += $"This week's winner is: {winner.Username}!";

            await (c as ISocketMessageChannel).SendMessageAsync(msg);

            await client.StopAsync();
            Environment.Exit(0);
        }

        public async Task InstallCommands()
        {
            // Hook the MessageReceived Event into our Command Handler
            client.MessageReceived += HandleCommand;

            //client.MessageDeleted += HandleDelete;
            // Discover all of the commands in this assembly and load them.
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);
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
        }

        public async Task HandleDelete(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            IMessage x = await message.GetOrDownloadAsync();
            await x.Channel.SendMessageAsync($"The deleted message was from {x.Author} and was: {x.Content}");
        }

        private Task Logger(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task MessageReceived(SocketMessage message)
        {
            Console.WriteLine($"Received message from {message.Author.Username} in {(message.Channel as IGuildChannel).Guild}#{message.Channel.Name}. Message contents:\n\t{message.Content}");
            if (message.Content.ToLower() == "ping!")
            {
                await message.Channel.SendMessageAsync("Pong!");
            }
        }
    }
}
