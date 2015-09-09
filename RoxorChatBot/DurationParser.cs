using System;
using System.Text.RegularExpressions;

namespace RoxorChatBot
{
    /// <summary>
    /// Contains logic to exctract duration from YouTube Duration Strings
    /// </summary>
    public static class DurationParser
    {
        /// <summary>
        /// The duration regex expression
        /// </summary>
        private static readonly string durationRegexExpression1 = @"PT(?<hours>[0-9]{0,})H(?<minutes>[0-9]{0,})M(?<seconds>[0-9]{0,})S";
        private static readonly string durationRegexExpression2 = @"PT(?<minutes>[0-9]{0,})M(?<seconds>[0-9]{0,})S";
        private static readonly string durationRegexExpression3 = @"PT(?<seconds>[0-9]{0,})S";
        /// <summary>
        /// Gets the duration.
        /// </summary>
        /// <param name="durationStr">The duration string.</param>
        /// <returns>return ticks of the song</returns>
        public static TimeSpan GetDuration(string durationStr)
        {
            Regex regexNamespaceInitializations = new Regex(durationRegexExpression1, RegexOptions.None);
            Match m = regexNamespaceInitializations.Match(durationStr);
            if (m.Success)
            {
                string hoursStr = m.Groups["hours"].Value;
                string minutesStr = m.Groups["minutes"].Value;
                string secondsStr = m.Groups["seconds"].Value;
                int hours = int.Parse(hoursStr);
                int minutes = int.Parse(minutesStr);
                int seconds = int.Parse(secondsStr);
                return new TimeSpan(hours, minutes, seconds);
            }
            else
            {
                regexNamespaceInitializations = new Regex(durationRegexExpression2, RegexOptions.None);
                m = regexNamespaceInitializations.Match(durationStr);
                if (m.Success)
                {
                    string minutesStr = m.Groups["minutes"].Value;
                    string secondsStr = m.Groups["seconds"].Value;
                    int minutes = int.Parse(minutesStr);
                    int seconds = int.Parse(secondsStr);
                    return new TimeSpan(0, minutes, seconds);
                }
                else
                {
                    regexNamespaceInitializations = new Regex(durationRegexExpression3, RegexOptions.None);
                    m = regexNamespaceInitializations.Match(durationStr);
                    if (m.Success)
                    {
                        string secondsStr = m.Groups["seconds"].Value;
                        int seconds = int.Parse(secondsStr);
                        return new TimeSpan(0, 0, seconds);
                    }
                }

            }
            return new TimeSpan();
        }
    }
}
