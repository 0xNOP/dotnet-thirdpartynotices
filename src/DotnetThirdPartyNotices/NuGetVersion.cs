using System;
using System.Linq;

namespace DotnetThirdPartyNotices;

// based on https://github.com/NuGetArchive/NuGet.Versioning/blob/0f25e04c3a33d2dff11cbb97e1c0827cf5bf6da6/src/NuGet.Versioning/NuGetVersionFactory.cs
internal static class NuGetVersion
{
    public static bool IsValid(string value)
    {
        if (value == null) return false;

        // trim the value before passing it in since we not strict here
        var sections = ParseSections(value.Trim());

        // null indicates the string did not meet the rules
        if (sections == null || string.IsNullOrEmpty(sections.Item1)) return false;
        var versionPart = sections.Item1;

        if (versionPart.IndexOf('.') < 0)
        {
            // System.Version requires at least a 2 part version to parse.
            versionPart += ".0";
        }

        if (!Version.TryParse(versionPart, out _)) return false;
        // labels
        if (sections.Item2 != null && !sections.Item2.All(s => IsValidPart(s, false)))
        {
            return false;
        }

        return sections.Item3 == null || IsValid(sections.Item3, true);
    }

    internal static bool IsLetterOrDigitOrDash(char c)
    {
        int x = c;

        // "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-"
        return (x >= 48 && x <= 57) || (x >= 65 && x <= 90) || (x >= 97 && x <= 122) || x == 45;
    }

    internal static bool IsValid(string s, bool allowLeadingZeros)
    {
        return s.Split('.').All(p => IsValidPart(p, allowLeadingZeros));
    }

    internal static bool IsValidPart(string s, bool allowLeadingZeros)
    {
        return IsValidPart(s.ToCharArray(), allowLeadingZeros);
    }

    internal static bool IsValidPart(char[] chars, bool allowLeadingZeros)
    {
        var result = chars.Length != 0;

        // 0 is fine, but 00 is not. 
        // 0A counts as an alpha numeric string where zeros are not counted
        if (!allowLeadingZeros && chars.Length > 1 && chars[0] == '0' && chars.All(char.IsDigit))
        {
            // no leading zeros in labels allowed
            result = false;
        }
        else
        {
            result &= chars.All(IsLetterOrDigitOrDash);
        }

        return result;
    }

    internal static Tuple<string, string[], string> ParseSections(string value)
    {
        string versionString = null;
        string[] releaseLabels = null;
        string buildMetadata = null;

        var dashPos = -1;
        var plusPos = -1;

        var chars = value.ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            var end = (i == chars.Length - 1);

            if (dashPos < 0)
            {
                if (!end && chars[i] != '-' && chars[i] != '+') continue;
                var endPos = i + (end ? 1 : 0);
                versionString = value.Substring(0, endPos);

                dashPos = i;

                if (chars[i] == '+')
                {
                    plusPos = i;
                }
            }
            else if (plusPos < 0)
            {
                if (!end && chars[i] != '+') continue;
                var start = dashPos + 1;
                var endPos = i + (end ? 1 : 0);
                var releaseLabel = value.Substring(start, endPos - start);

                releaseLabels = releaseLabel.Split('.');

                plusPos = i;
            }
            else if (end)
            {
                var start = plusPos + 1;
                var endPos = i + (end ? 1 : 0);
                buildMetadata = value.Substring(start, endPos - start);
            }
        }

        return new Tuple<string, string[], string>(versionString, releaseLabels, buildMetadata);
    }
}