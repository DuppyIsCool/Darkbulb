// CommandHandler.cs
using System.Text;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace DarkbulbBot
{
    public class CommandHandler
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
                    await command.RespondAsync("Only Lord Veigar's most esteemed (server admins) can run this command.");
                    return;
                }

                var channelId = command.Channel.Id;
                var configurations = ChannelManager.LoadConfigurations();
                configurations[guildId.Value] = channelId;
                ChannelManager.SaveConfigurations(configurations);

                await command.RespondAsync($"Channel updated successfully. All notifications will be sent to <#{channelId}>.");
                return;
            }
            else if (command.Data.Name.Equals("changelog"))
            {
                // only admins may run /changelog
                if (!(command.User is SocketGuildUser user2) || !user2.GuildPermissions.Administrator)
                {
                    await command.RespondAsync("Only Lord Veigar's most esteemed (server admins) can run this command.", ephemeral: true);
                    return;
                }

                var jobCode = command.Data.Options.First(o => o.Name == "jobcode").Value.ToString();
                var changelogDir = Path.Combine(AppContext.BaseDirectory, "changelogs");
                var filePath = Path.Combine(changelogDir, $"{jobCode}-changelog.json");

                if (!File.Exists(filePath))
                {
                    await command.RespondAsync($"No changelog found for `{jobCode}`.", ephemeral: true);
                    return;
                }

                await command.RespondWithFileAsync(
                    File.OpenRead(filePath),
                    $"{jobCode}-changelog.json",
                    $"Here’s the changelog for **{jobCode}**:"
                );
            }
            else if (command.Data.Name.Equals("export"))
            {
                // — Admin check —
                if (!(command.User is SocketGuildUser guildUser) ||
                    !guildUser.GuildPermissions.Administrator)
                {
                    await command.RespondAsync("Only Lord Veigar's most esteemed (server admins) can run this command.", ephemeral: true);
                    return;
                }

                // — Find all changelog files —
                var changelogDir = Path.Combine(AppContext.BaseDirectory, "changelogs");
                if (!Directory.Exists(changelogDir))
                {
                    await command.RespondAsync("No changelogs directory found.", ephemeral: true);
                    return;
                }

                var files = Directory.GetFiles(changelogDir, "*-changelog.json");
                if (files.Length == 0)
                {
                    await command.RespondAsync("No changelog files to export.", ephemeral: true);
                    return;
                }

                // — Build CSV —
                var sb = new StringBuilder();
                sb.AppendLine("JobCode,Action,Timestamp,Title,Craft,ProductTeam,Office,Url");

                foreach (var file in files)
                {
                    var jobCode = Path.GetFileName(file).Replace("-changelog.json", "");
                    var entries = JArray.Parse(File.ReadAllText(file));

                    foreach (JObject e in entries)
                    {
                        string action = e.Value<string>("Action") ?? "";
                        string ts = e.Value<string>("Timestamp") ?? "";
                        string title = e.Value<string>("Title") ?? "";
                        string craft = e.Value<string>("Craft") ?? "";
                        string productTeam = e.Value<string>("ProductTeam") ?? "";
                        string office = e.Value<string>("Office") ?? "";
                        string url = e.Value<string>("Url") ?? "";

                        // simple CSV-escaping
                        string Q(string s) => $"\"{s.Replace("\"", "\"\"")}\"";

                        sb
                          .Append(jobCode).Append(',')
                          .Append(action).Append(',')
                          .Append(ts).Append(',')
                          .Append(Q(title)).Append(',')
                          .Append(Q(craft)).Append(',')
                          .Append(Q(productTeam)).Append(',')
                          .Append(Q(office)).Append(',')
                          .Append(Q(url))
                          .AppendLine();
                    }
                }

                // — Send the file —
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                using var ms = new MemoryStream(bytes, writable: false);
                await command.RespondWithFileAsync(
                    ms,
                    "job-history.csv",
                    "Here's the report:"
                );
            }
        }
    }
}
