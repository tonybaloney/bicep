// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Bicep.Core.ApiVersion
{
    public static class ApiVersionHelper
    {
        public static StringComparer Comparer = LanguageConstants.ResourceTypeComparer;

        private static readonly int ApiVersionDateLength = "2000-01-01".Length;

        //asdfg  test case insensitive
        private static readonly Regex VersionPattern = new Regex(@"^((?<version>(\d{4}-\d{2}-\d{2}))(?<suffix>-(preview|alpha|beta|rc|privatepreview))?$)", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        public static (string? date, string? suffix) TryParse(string apiVersion)
        {
            MatchCollection matches = VersionPattern.Matches(apiVersion);

            foreach (Match match in matches)
            {
                string? version = match.Groups["version"].Value;
                string? suffix = match.Groups["suffix"].Value;

                if (version is not null)
                {
                    return (version, suffix.ToLowerInvariant());
                }
            }

            return (null, null);
        }

        public static string Format(DateTime date, string? suffix = null)
        {
            var result = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-mm-dd}", date);
            return string.IsNullOrEmpty(suffix) ? result : result + suffix;
        }

        // Assumes a and b are valid api-version strings
        public static int CompareApiVersionDates(string a, string b)
        {
            // Since apiVersions are in the form yyyy-mm-dd{-*}, we can do a simple string comparison against the
            // date portion.

            return a.Substring(0, ApiVersionDateLength).CompareTo(b.Substring(0, ApiVersionDateLength));
        }

        // Assumes apiVersion is a valid api-version string
        public static bool IsPreviewVersion(string apiVersion)
        {
            return apiVersion.Length > ApiVersionDateLength;
        }

        public static DateTime ParseDate(string apiVersion) //asdfg test
        {
            (string? date, string? _) = TryParse(apiVersion);
            if (date is null)
            {
                throw new Exception($"Invalid API version '{apiVersion}'");
            }
            return DateTime.ParseExact(date, "yyyy-mm-dd", CultureInfo.InvariantCulture);
        }

        //asdfg
        //public DateTime? ApiVersionToDate(string apiVersion)
        //{
        //    (string? version, string? _) = apiVersionProvider.GetApiVersionAndPrefix(apiVersion);

        //    if (version is not null)
        //    {
        //        return DateTime.Parse(version);
        //    }

        //    return null;
        //}
    }
}
