using System.Text;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace DarkbulbBot
{
    public class Career
    {
        public string ID { get; set; }
        public string Title { get; set; }
        public string Craft { get; set; }
        public string ProductTeam { get; set; }
        public string Office { get; set; }
        public string Url { get; set; }
        public string Datetime { get; set; }

        // Job Code (REQ-xxxxx or sometimes just a number)
        public string JobCode { get; set; }

        // Full description text
        public string Description { get; set; }

        /// <summary>
        /// Returns an inline diff between old and new descriptions, prefixed with + for additions, - for removals.
        /// Currently unused, diffs too large for discord messages and this may just result in unneeded noise..
        /// </summary>
        public static string GetDescriptionDiff(string oldDesc, string newDesc)
        {
            var diffBuilder = new InlineDiffBuilder(new Differ());
            var model = diffBuilder.BuildDiffModel(oldDesc ?? string.Empty, newDesc ?? string.Empty);

            var sb = new StringBuilder();
            foreach (var line in model.Lines)
            {
                string prefix = line.Type switch
                {
                    ChangeType.Unchanged => " ",
                    ChangeType.Inserted => "+",
                    ChangeType.Deleted => "-",
                    _ => "?"
                };
                sb.AppendLine(prefix + line.Text);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Formats the career details for announcements.
        /// Uses bold text and Markdown‑embedded links.
        /// </summary>
        public string GetCareerDetails(bool isRemoved = false)
        {
            string statusEmoji = isRemoved ? "❌" : "🆕";
            string actionText = isRemoved ? "Removed Job Posting" : "New Job Posting";

            var sb = new StringBuilder();
            sb.AppendLine($"{statusEmoji} **{actionText}!**");

            // Title as clickable link if available
            if (!isRemoved && !string.IsNullOrEmpty(Url))
                sb.AppendLine($"**Title:** [{Title}](<{Url}>)");
            else
                sb.AppendLine($"**Title:** {Title}");

            sb.AppendLine($"**Craft:** {Craft}");
            sb.AppendLine($"**Product Team:** {ProductTeam}");
            sb.AppendLine($"**Location:** {Office}");
            sb.AppendLine($"**Date Retrieved:** {Datetime}");
            sb.AppendLine($"**Job Code:** {JobCode}");

            return sb.ToString();
        }
    }
}
