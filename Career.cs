
namespace DarkbulbBot
{
    class Career
    {
        public string ID { get; set; }
        public string Title { get; set; }
        public string Craft { get; set; }
        public string ProductTeam { get; set; }
        public string Office { get; set; }
        public string Url { get; set; }
        public string Datetime { get; set; }
        public string GetCareerDetails(bool isRemoved = false)
        {
            string statusEmoji = isRemoved ? "❌" : "🆕";
            string actionText = isRemoved ? "Removed Job Posting" : "New Job Posting";

            string details = $"{statusEmoji} **{actionText}!**\n";

            if (isRemoved || string.IsNullOrEmpty(Url))
            {
                details += $"**Title:** {Title}\n";
            }
            else
            {
                details += $"**Title:** [{Title}](<{Url}>)\n";
            }

            details += $"**Craft:** {Craft}\n";
            details += $"**Product Team:** {ProductTeam}\n";
            details += $"**Location:** {Office}\n";
            details += $"**Date Retrieved**: {Datetime}";

            return details;
        }
    }
}
