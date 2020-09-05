using System;
using System.Text.RegularExpressions;
using ShokoJellyfin.Providers.API.Models;

namespace ShokoJellyfin.Providers
{
    public class Helper
    {
        public static string GetImageUrl(Image image)
        {
            return image != null ? $"http://{Plugin.Instance.Configuration.Host}:{Plugin.Instance.Configuration.Port}/api/v3/Image/{image.Source}/{image.Type}/{image.ID}" : null;
        }

        public static string SummarySanitizer(string summary) // Based on ShokoMetadata which is based on HAMA's
        {
            var config = Plugin.Instance.Configuration;
            
            if (config.SynopsisCleanLinks)
                Regex.Replace(summary, @"https?:\/\/\w+.\w+(?:\/?\w+)? \[([^\]]+)\]", "");
            
            if (config.SynopsisCleanMiscLines)
                Regex.Replace(summary, @"^(\*|--|~) .*", "", RegexOptions.Multiline);
            
            if (config.SynopsisRemoveSummary)
                Regex.Replace(summary, @"\n(Source|Note|Summary):.*", "", RegexOptions.Singleline);
            
            if (config.SynopsisCleanMultiEmptyLines)
                Regex.Replace(summary, @"\n\n+", "", RegexOptions.Singleline);

            return summary;
        }
    }
}