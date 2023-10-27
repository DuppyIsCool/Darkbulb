using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;
using HtmlAgilityPack;
using System.Threading;

namespace DarkbulbBot
{
    class Program
    {
        public static Task Main(string[] args) => new Program().MainAsync();
        public static DiscordSocketClient client;
        private CommandHandler commandHandler;
        private Dictionary<ulong,ulong> channelConfigurations;
        private static  Dictionary<string, Career> removedCareers = new Dictionary<string, Career>();
        private static readonly Dictionary<string, Career> newCareers = new Dictionary<string, Career>();
        //private Dictionary<
        private static Timer _timer;
        public async Task MainAsync()
        {
            
            commandHandler = new CommandHandler();
            client = new DiscordSocketClient();

            

            //Register our Log and Create Commands
            client.Log += Log;
            client.Ready += Create_Commands;
            client.SlashCommandExecuted += commandHandler.SlashCommandHandler;
            client.Ready += BeginScraping;
            // Path to the config.json file
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

            // Read the content of the config.json file
            string configContent;
            using (StreamReader reader = new StreamReader(configFilePath))
            {
                configContent = reader.ReadToEnd();
            }

            // Parse the JSON content
            JObject configJson = JObject.Parse(configContent);

            // Access the key from JSON
            var token = configJson["TOKEN"].ToString();

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            
            
            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        public async Task BeginScraping() 
        {
            CareerManager.LoadCareers();
            _timer = new Timer(ScrapeCareersCallback, null, TimeSpan.Zero, TimeSpan.FromHours(2));
        }
        public async Task Create_Commands()
        {
            Console.WriteLine("Setting up commands");

            var setChannelCommand = new SlashCommandBuilder();
            setChannelCommand.WithName("set-channel");
            setChannelCommand.WithDescription("Sets the channel for bot output");
            try
            {
                await client.CreateGlobalApplicationCommandAsync(setChannelCommand.Build());

                Console.WriteLine("Finished setting up commands");
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                Console.WriteLine(json);
            }  
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async void ScrapeCareersCallback(object state)
        {
            Console.WriteLine("Began loading channel config");
            channelConfigurations = ChannelManager.LoadConfigurations();
            Console.WriteLine($"Loaded {channelConfigurations.Count} channels");
            Console.WriteLine("Began Auto Scrap Callback");
            await ScrapeCareersAsync();

            // Creating local copies of the dictionaries before entering the main loop
            var localNewCareers = new Dictionary<string, Career>(newCareers);
            var localRemovedCareers = new Dictionary<string, Career>(removedCareers);

            foreach (var config in channelConfigurations)
            {
                var guildId = config.Key;
                var channelId = config.Value;
                Console.WriteLine($"Messaging for guild id {guildId}");
                var guild = client.GetGuild(guildId);
                if (guild != null)
                {
                    var textChannel = guild.GetTextChannel(channelId);
                    if (textChannel != null)
                    {
                        // Get the bot as a user
                        var botUser = guild.CurrentUser;

                        // Get the bot's permissions in the text channel
                        var permissions = textChannel.GetPermissionOverwrite(botUser);

                        // Check if the bot has permission to send messages
                        if (permissions.HasValue && permissions.Value.SendMessages == PermValue.Allow)
                        {
                            foreach (var career in localNewCareers.Values)
                            {
                                var careerDetails = career.GetCareerDetails();
                                var message = await textChannel.SendMessageAsync(careerDetails, false, null);
                            }

                            foreach (var career in localRemovedCareers.Values)
                            {
                                var careerDetails = career.GetCareerDetails(true);
                                var message = await textChannel.SendMessageAsync(careerDetails, false, null);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Bot lacks permission to send messages in channel {channelId} of guild {guildId}");
                        }
                    }
                }
            }

            Console.WriteLine("Finished Messaging");
            // Clear the new and removed careers dictionaries after processing
            newCareers.Clear();
            removedCareers.Clear();
        }



        public async Task ScrapeCareersAsync()
        {
            Console.WriteLine("Began scrapping jobs");
            string BaseJobUrl = "https://www.riotgames.com/en/work-with-us/job/";
            var url = "https://www.riotgames.com/en/work-with-us/jobs";
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);

            removedCareers = new Dictionary<string, Career>(CareerManager.GetActiveCareers());

            foreach (var job in htmlDocument.DocumentNode.SelectNodes("//a[contains(@class, 'job-row__inner')]"))
            {
                var href = job.GetAttributeValue("href", "");
                var parts = href.Split('/');
                var jobNum = parts[parts.Length - 1]; // Extracting the job ID from the href
                var jobUrl = BaseJobUrl + jobNum; // Constructing the full URL

                var title = HtmlEntity.DeEntitize(job.SelectSingleNode(".//div[contains(@class, 'job-row__col--primary')]").InnerText.Trim());
                var secondaryCols = job.SelectNodes(".//div[contains(@class, 'job-row__col--secondary')]");
                var craft = HtmlEntity.DeEntitize(secondaryCols[0].InnerText.Trim());
                var productTeam = HtmlEntity.DeEntitize(secondaryCols[1].InnerText.Trim());
                var office = HtmlEntity.DeEntitize(secondaryCols[2].InnerText.Trim());
                DateTime now = DateTime.Now;
                var jsonID = $"{jobNum}-{title}-{craft}-{office}";

                if (CareerManager.GetCareer(jsonID) == null)
                {
                    Career tempCareer = new Career
                    {
                        ID = jsonID,
                        Title = title,
                        Craft = craft,
                        ProductTeam = productTeam,
                        Office = office,
                        Url = jobUrl,
                        Datetime = now.ToString("U")
                };

                    CareerManager.AddCareer(tempCareer);
                    newCareers.Add(jobNum, tempCareer);
                }
                else
                {
                    removedCareers.Remove(jsonID);
                }
            }

            //Remove the old jobs from our list of active careers.
            foreach(var job in removedCareers) 
            {
                CareerManager.RemoveCareer(job.Value.ID);
            }

            Console.WriteLine("Finished scrapping jobs, found "+newCareers.Count+ " new jobs with "+removedCareers.Count + " removals");
            CareerManager.SaveCareers();
        }
    }
}
