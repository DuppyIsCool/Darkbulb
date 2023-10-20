using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace DarkbulbBot
{
    class ChannelManager
    {
        private static readonly string ConfigFilePath = "channel_config.json";

        public static Dictionary<ulong, ulong> LoadConfigurations()
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                return JsonConvert.DeserializeObject<Dictionary<ulong, ulong>>(json);
            }

            return new Dictionary<ulong, ulong>();
        }

        public static void SaveConfigurations(Dictionary<ulong, ulong> configurations)
        {
            var json = JsonConvert.SerializeObject(configurations, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
        }
    }
}
