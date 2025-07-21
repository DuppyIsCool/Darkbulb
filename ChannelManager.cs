using Newtonsoft.Json;

namespace DarkbulbBot
{
    class ChannelManager
    {
        private static readonly string ConfigFilePath = "configs/channel_config.json";

        public static Dictionary<ulong, ulong> LoadConfigurations()
        {
            if (!File.Exists(ConfigFilePath))
            {
                // create an empty JSON file so subsequent reads and writes work
                File.WriteAllText(ConfigFilePath, "{}");
                return new Dictionary<ulong, ulong>();
            }

            var json = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<Dictionary<ulong, ulong>>(json)
                   ?? new Dictionary<ulong, ulong>();
        }

        public static void SaveConfigurations(Dictionary<ulong, ulong> configurations)
        {
            var json = JsonConvert.SerializeObject(configurations, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
        }
    }
}
