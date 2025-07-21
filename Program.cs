// Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkbulbBot
{
    class Program
    {
        private static DiscordSocketClient _client;
        private readonly CommandHandler _commandHandler = new CommandHandler();

        // Channel config: guildId → channelId
        private static Dictionary<ulong, ulong> _channelConfigurations;

        // Two independent timers
        private static Timer _listingTimer;
        private static Timer _detailTimer;

        // In‑memory cache of active careers
        private static Dictionary<string, Career> _existingCareers;

        // For rotating through detail‑checks
        private static int _detailIndex = 0;
        private const int DetailBatchSize = 15;  // process 15 jobs per 10min

        private static readonly JobScraper _scraper = new JobScraper();

        public static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Log += LogAsync;
            _client.Ready += OnReadyAsync;
            _client.SlashCommandExecuted += _commandHandler.SlashCommandHandler;
            _client.Disconnected += OnDisconnectedAsync;

            var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("BOT_TOKEN not set.");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }

        private Task LogAsync(LogMessage msg)
        {
            Console.WriteLine(msg);
            return Task.CompletedTask;
        }

        private async Task OnReadyAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] Bot ready; registering commands and starting schedules...");

            _channelConfigurations = ChannelManager.LoadConfigurations();
            CareerManager.LoadCareers();
            _existingCareers = CareerManager.GetActiveCareers();

            await RegisterCommandsAsync();

            _listingTimer = new Timer(async _ => await ScrapeListingAndAnnounceAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(30));
            _detailTimer = new Timer(async _ => await CheckDetailBatchAsync(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));
        }

        private async Task RegisterCommandsAsync()
        {
            var setChannel = new SlashCommandBuilder().WithName("set-channel").WithDescription("Sets the channel for bot announcements.");
            var export = new SlashCommandBuilder().WithName("export-history").WithDescription("Export all job history to CSV.");
            var changelog = new SlashCommandBuilder().WithName("changelog").WithDescription("Get the change history for a given job.")
                .AddOption("jobcode", ApplicationCommandOptionType.String, "REQ‑ID", isRequired: true);

            try
            {
                await _client.CreateGlobalApplicationCommandAsync(setChannel.Build());
                await _client.CreateGlobalApplicationCommandAsync(export.Build());
                await _client.CreateGlobalApplicationCommandAsync(changelog.Build());
                Console.WriteLine($"[{DateTime.UtcNow:O}] Slash commands registered.");
            }
            catch (HttpException ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:O}] Failed to register commands: {ex.Message}");
            }
        }

        private Task OnDisconnectedAsync(Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] Disconnected: {ex?.Message}. Reconnecting in 10s...");
            _ = Task.Delay(10000).ContinueWith(_ => _client.StartAsync());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Runs every 30m: fetch listing, detect new/removals, log & announce.
        /// </summary>
        private async Task ScrapeListingAndAnnounceAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] Starting listing scrape...");
            var summaries = await _scraper.ScrapeListingAsync();
            var newJobs = new List<Career>();
            var removedKeys = new HashSet<string>(_existingCareers.Keys);

            // Detect new
            foreach (var sum in summaries)
            {
                var key = $"{sum.RawId}-{sum.Title}-{sum.Craft}-{sum.ProductTeam}-{sum.Office}";
                removedKeys.Remove(key);

                if (!_existingCareers.ContainsKey(key))
                {
                    var detail = await _scraper.FetchJobDetailAsync(sum);
                    var career = new Career
                    {
                        ID = key,
                        Title = detail.Title,
                        Craft = detail.Craft,
                        ProductTeam = detail.ProductTeam,
                        Office = detail.Office,
                        Url = detail.Url,
                        Datetime = DateTime.UtcNow.ToString("U"),
                        JobCode = detail.RealJobId,
                        Description = detail.Description
                    };
                    CareerManager.AddCareer(career);
                    WriteCreatedChangelog(detail);
                    _existingCareers[key] = career;
                    newJobs.Add(career);
                }
            }

            // Detect removed, but collect objects *before* we delete them
            var removedList = new List<Career>();
            foreach (var key in removedKeys)
            {
                var old = _existingCareers[key];
                WriteRemovedChangelog(old);
                removedList.Add(old);
                CareerManager.RemoveCareer(key);
                _existingCareers.Remove(key);
            }

            CareerManager.SaveCareers();
            await AnnounceChangesAsync(newJobs, removedList);

            Console.WriteLine($"[{DateTime.UtcNow:O}] Finished listing scrape: {newJobs.Count} new, {removedKeys.Count} removed.");
        }

        private async Task CheckDetailBatchAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] Starting detail batch (index {_detailIndex})...");
            var keys = _existingCareers.Keys.ToList();
            if (!keys.Any())
            {
                Console.WriteLine($"[{DateTime.UtcNow:O}] No careers to process in detail batch.");
                return;
            }
            var batch = keys.Skip(_detailIndex).Take(DetailBatchSize).ToList();
            if (batch.Count < DetailBatchSize)
                batch.AddRange(keys.Take(DetailBatchSize - batch.Count));
            _detailIndex = (_detailIndex + DetailBatchSize) % keys.Count;
            int processed = 0;
            foreach (var key in batch)
            {
                processed++;
                var career = _existingCareers[key];
                var sum = new ScrapedJobSummary
                {
                    RawId = career.ID.Split('-')[0],
                    Title = career.Title,
                    Craft = career.Craft,
                    ProductTeam = career.ProductTeam,
                    Office = career.Office,
                    Url = career.Url
                };
                var detail = await _scraper.FetchJobDetailAsync(sum);
                if (detail.Description != career.Description)
                {
                    AppendDescriptionChangelog(career, detail);
                    career.Description = detail.Description;
                    career.Datetime = DateTime.UtcNow.ToString("U");
                    await AnnounceDescriptionChangeAsync(detail);
                }
            }
            CareerManager.SaveCareers();
            Console.WriteLine($"[{DateTime.UtcNow:O}] Finished detail batch. Processed {processed} jobs.");
        }

        #region Changelog Helpers

        private void WriteCreatedChangelog(ScrapedJob detail)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "changelogs");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{detail.RealJobId}-changelog.json");
            var rec = new JObject
            {
                ["Timestamp"] = DateTime.UtcNow.ToString("o"),
                ["Action"] = "Created",
                ["Title"] = detail.Title,
                ["Craft"] = detail.Craft,
                ["ProductTeam"] = detail.ProductTeam,
                ["Office"] = detail.Office,
                ["Url"] = detail.Url,
                ["Description"] = detail.Description
            };
            File.WriteAllText(file, JsonConvert.SerializeObject(new[] { rec }, Formatting.Indented));
        }

        private void WriteRemovedChangelog(Career c)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "changelogs");
            var file = Path.Combine(dir, $"{c.JobCode}-changelog.json");
            if (!File.Exists(file)) return;
            var history = JArray.Parse(File.ReadAllText(file));
            history.Add(new JObject
            {
                ["Timestamp"] = DateTime.UtcNow.ToString("o"),
                ["Action"] = "Removed"
            });
            File.WriteAllText(file, JsonConvert.SerializeObject(history, Formatting.Indented));
        }

        private void AppendDescriptionChangelog(Career old, ScrapedJob detail)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "changelogs");
            var file = Path.Combine(dir, $"{old.JobCode}-changelog.json");
            var history = JArray.Parse(File.ReadAllText(file));
            history.Add(new JObject
            {
                ["Timestamp"] = DateTime.UtcNow.ToString("o"),
                ["Action"] = "DescriptionUpdated",
                ["OldDescription"] = old.Description,
                ["NewDescription"] = detail.Description
            });
            File.WriteAllText(file, JsonConvert.SerializeObject(history, Formatting.Indented));
        }

        #endregion

        #region Announcements

        /// <summary>
        /// Announces new and removed jobs to each configured channel.
        /// </summary>
        private async Task AnnounceChangesAsync(List<Career> newJobs, List<Career> removedJobs)
        {
            foreach (var kv in _channelConfigurations)
            {
                var guild = _client.GetGuild(kv.Key);
                var channel = guild?.GetTextChannel(kv.Value);
                if (channel == null) continue;

                // Announce new jobs
                foreach (var j in newJobs)
                {
                    var msg = j.GetCareerDetails();
                    await channel.SendMessageAsync(msg);
                }

                // Announce removals
                foreach (var old in removedJobs)
                {
                    var msg = old.GetCareerDetails(isRemoved: true);
                    await channel.SendMessageAsync(msg);
                }
            }
        }

        private async Task AnnounceDescriptionChangeAsync(ScrapedJob detail)
        {
            foreach (var kv in _channelConfigurations)
            {
                var guild = _client.GetGuild(kv.Key);
                var channel = guild?.GetTextChannel(kv.Value);
                if (channel == null) continue;
                var msg = $"✏️ Description updated for **{detail.Title}** ({detail.RealJobId})";
                await channel.SendMessageAsync(msg);
            }
        }

        #endregion
    }
}
