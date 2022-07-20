// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Bicep.Core.ApiVersions
{
    public static class ApiVersionHelper
    {
        //asdfg remove unneeded




        public static StringComparer Comparer = LanguageConstants.ResourceTypeComparer;

        private static readonly int ApiVersionDateLength = "2000-01-01".Length;

        private static readonly Regex VersionPattern = new Regex(@"^((?<version>(\d{4}-\d{2}-\d{2}))(?<suffix>-(preview|alpha|beta|rc|privatepreview))?$)", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        //asdfg?
        //public static (string date, string suffixWithHypen)? TryParse(string apiVersion)
        //{
        //    MatchCollection matches = VersionPattern.Matches(apiVersion);
        //    if (matches.Count == 1)
        //    {
        //        Match match = matches[0];
        //        string? version = match.Groups["version"].Value;
        //        string? suffix = match.Groups["suffix"].Value;

        //        if (version is not null)
        //        {
        //            return (version, string.IsNullOrEmpty(suffix) ? "" : suffix.ToLowerInvariant());
        //        }
        //    }

        //    return null;
        //}

        public static (string? date, string? suffixWithHypen) TryParse(string apiVersion)
        {
            MatchCollection matches = VersionPattern.Matches(apiVersion);
            if (matches.Count == 1)
            {
                Match match = matches[0];
                string? version = match.Groups["version"].Value;
                string? suffix = match.Groups["suffix"].Value;

                if (version is not null)
                {
                    return (version, string.IsNullOrEmpty(suffix) ? null : suffix.ToLowerInvariant());
                }
            }

            return (null, null);
        }

        public static (string date, string suffix) Parse(string apiVersion)
        {
            var (date, suffix) = TryParse(apiVersion);

            if (date == null)
            {
                throw new ArgumentException($"Unexpected API version {apiVersion}");
            }

            return (date, suffix ?? String.Empty);
        }

        public static string Format(DateTime date, string? suffix = null)
        {
            var result = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}", date);
            return string.IsNullOrEmpty(suffix) ? result : result + suffix;
        }

        // Assumes a and b are valid api-version strings
        // Compares just the date in an API version
        // Positive means a > b
        public static int CompareApiVersionDates(string a, string b)
        {
            // Since apiVersions are in the form yyyy-MM-dd{-*}, we can do a simple string comparison against the
            // date portion.
            return a.Substring(0, ApiVersionDateLength).CompareTo(b.Substring(0, ApiVersionDateLength));
        }

        //// Assumes apiVersion is a valid api-version string
        //public static bool IsPreviewVersion(string apiVersion)
        //{
        //    return apiVersion.Length > ApiVersionDateLength;
        //}

        //// Assumes apiVersion is a valid api-version string
        //public static bool IsStableVersion(string apiVersion)
        //{
        //    return !IsPreviewVersion(apiVersion);
        //}

        public static DateTime ParseDateFromString(string dateString)
        {
            return DateTime.ParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static DateTime ParseDateFromApiVersion(string apiVersion)
        {
            (string? date, string? _) = TryParse(apiVersion);
            if (date is null)
            {
                throw new Exception($"Invalid API version '{apiVersion}'");
            }

            return ParseDateFromString(date);
        }

        //public static IEnumerable<string> FilterPreview(IEnumerable<string> apiVersions)
        //{
        //    return apiVersions.Where(v => ApiVersionHelper.IsPreviewVersion(v)).ToArray();
        //}

        //public static IEnumerable<string> FilterNonPreview(IEnumerable<string> apiVersions)
        //{
        //    return apiVersions.Where(v => !ApiVersionHelper.IsPreviewVersion(v)).ToArray();
        //}
    }
}
