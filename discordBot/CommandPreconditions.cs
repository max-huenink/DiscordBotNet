using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

    public class RequireBotOwner : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services) =>
            Task.Run(() => BotUsers.Owner == context.User.Id ?
                PreconditionResult.FromSuccess() :
                PreconditionResult.FromError($"{context.User.Username} is not the owner"));
    }

    public class CheckIgnore : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services) =>
            Task.Run(() => BotUsers.IsIgnored(context.User) ?
                //PreconditionResult.FromError($"{context.User.Username} is an ignored user") :
                PreconditionResult.FromError("Did you hear something?") :
                PreconditionResult.FromSuccess());
    }

    public class AmBot : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services) =>
            Task.Run(() =>
                context.User.Id == context.Client.CurrentUser.Id ?
                PreconditionResult.FromError("") :
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
}
