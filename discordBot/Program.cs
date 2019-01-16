using System;
using System.Threading.Tasks;
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

        static void Main(string[] args) => new Program().Start(args).GetAwaiter().GetResult();

        public Program()
        {
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
            client.Log += Logger;
            //client.MessageReceived += MessageReceived;

            await InstallCommands();

            await client.LoginAsync(TokenType.Bot, "MzgzMzA5MDU1Mzk0ODQwNTc3.DPkc0g.ehU_ZfUQpxe9wt83nimB-_d3LX0");
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
