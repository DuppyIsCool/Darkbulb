using Discord.WebSocket;
using System.Threading.Tasks;
namespace DarkbulbBot
{
    class CommandHandler
    {
        public async Task SlashCommandHandler(SocketSlashCommand command)
        {
            if (command.Data.Name.Equals("set-channel"))
            {
                var guildId = command.GuildId;
                if (!guildId.HasValue)
                {
                    await command.RespondAsync("This command can only be used within a guild.");
                    return;
                }

                var user = command.User as SocketGuildUser;
                if (user == null || !user.GuildPermissions.Administrator)
                {
                    await command.RespondAsync("You do not have permission to execute this command. Only administrators can run this command.");
                    return;
                }

                var channelId = command.Channel.Id;

                var configurations = ChannelManager.LoadConfigurations();
                configurations[guildId.Value] = channelId;
                ChannelManager.SaveConfigurations(configurations);

                await command.RespondAsync($"Channel updated successfully. All notifications will be sent to <#{channelId}>.");
            }
        }
    }
}
