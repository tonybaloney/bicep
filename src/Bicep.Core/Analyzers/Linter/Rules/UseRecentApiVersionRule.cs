// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bicep.Core.Analyzers.Linter.ApiVersions;
using Bicep.Core.CodeAction;
using Bicep.Core.Configuration;
using Bicep.Core.Diagnostics;
using Bicep.Core.Parsing;
using Bicep.Core.Resources;
using Bicep.Core.Semantics;
using Bicep.Core.Semantics.Metadata;
using Bicep.Core.Syntax;
using Bicep.Core.TypeSystem;

namespace Bicep.Core.Analyzers.Linter.Rules
{
    public sealed class UseRecentApiVersionRule : LinterRuleBase
    {
        /* asdfg
        <#
.Synopsis
    Ensures the apiVersions are recent (when used in reference functions).
.Description
    Ensures the apiVersions of any resources used within reference functions are recent and non-preview.
#>
param(
    # The resource in the main template
    [Parameter(Mandatory = $true, Position = 0)]
    [string]
    $TemplateText,

    # The resource in the main template
    [Parameter(Mandatory = $true, Position = 0)]
    [PSObject]
    $TemplateObject,

    # All potential resources in Azure (from cache)
    [Parameter(Mandatory = $true, Position = 2)]
    [PSObject]
    $AllAzureResources,

    # Number of days that the apiVersion must be less than 
    [Parameter(Mandatory = $false, Position = 3)]
    [int32]
    $NumberOfDays = 730,

    # Test Run Date - date to use when doing comparisons, if not current date (used for unit testing against and old cache)
    [Parameter(Mandatory = $false, Position = 3)]
    [datetime]
    $TestDate = [DateTime]::Now
)

$foundReferences = $TemplateText | 
?<ARM_Template_Function> -FunctionName 'reference|list\w{0,}'

foreach ($foundRef in $foundReferences) {
    
    $hasApiVersion = $foundRef.Value | ?<ARM_API_Version> -Extract # Find the api version
    if (-not $hasApiVersion) { continue } # if we don't have one, continue.
    $apiVersion = $hasApiVersion.0 
    $hasResourceId = $foundRef.Value | ?<ARM_Template_Function> -FunctionName resourceId
    $hasVariable = $foundRef.value | ?<ARM_Variable> | Select-Object -First 1
    $potentialResourceType = ''

    if ($hasResourceId) {       
        $parameterSegments = @($hasResourceId.Groups["Parameters"].value -split '[(),]' -ne '' -replace "^\s{0,}'" -replace "'\s{0,}$")
        $potentialResourceType = ''
        $resourceTypeStarted = $false
        $potentialResourceType = @(foreach ($seg in $parameterSegments) {
                if ($seg -like '<dot>/*') {
                    $seg
    }
}) -join '/'
    }
    elseif($hasVariable) {
        $foundResource = Find - JsonContent - Key name - Value "*$($hasVariable.Value)*" - InputObject $TemplateObject - Like |
        Where - Object JSONPath - Like * Resources * |
        Select - Object - First 1

        $typeList = @(@($foundResource) + @($foundResource.ParentObject) | Where - Object Type | Select - Object - ExpandProperty Type)
        [Array]::Reverse($typeList)
        $potentialResourceType = $typeList - join '/'
    }

if (-not $potentialResourceType) { continue }
    
    $apiDate = [DateTime]::new($hasApiVersion.Year, $hasApiVersion.Month, $hasApiVersion.Day) # now coerce the apiVersion into a DateTime

    $validApiVersions = @($AllAzureResources.$potentialResourceType | # and see if there's an apiVersion.
        Select - Object - ExpandProperty apiVersions |
        Sort - Object - Descending)

    if (-not $validApiVersions) { 
        $potentialResourceTypes = @($potentialResourceType - split '/')
        for ($i = ($potentialResourceTypes.Count - 1) ; $i - ge 1; $i--) {
            $potentialType = $potentialResourceTypes[0..$i] - join '/'
            if ($AllAzureResources.$potentialType) {
                $validApiVersions = @($AllAzureResources.$potentialType | # and see if there's an apiVersion.
                    Select - Object - ExpandProperty apiVersions |
                    Sort - Object - Descending)            
                break
            }
    }
    if (-not $validApiVersions) {
        continue
        }
}

    # Create a string of recent or allowed apiVersions for display in the error message
    $recentApiVersions = ""

    #add latest stable apiVersion to acceptable list by default
    $stableApiVersions = $validApiVersions | where - object { $_ - notmatch 'preview' } 
    $latestStableApiVersion = $stableApiVersions | Select - Object - First 1

    $recentApiVersions += "        $latestStableApiVersion`n"

    $howOutOfDate = -1
    $n = 0
    foreach ($v in $validApiVersions) {

        $hasDate = $v - match "(?<Year>\d{4,4})-(?<Month>\d{2,2})-(?<Day>\d{2,2})"
        $vDate = [DateTime]::new($matches.Year, $matches.Month, $matches.Day)

        #if the apiVersion is "recent" or the latest one add it to the list (note $validApiVersions is sorted)
# note "recent" means is it new enough that it's allowed by the test
        if ($($TestDate - $vDate).TotalDays -lt $NumberOfDays -or $v -eq $validApiVersions[0]) {
# TODO: when the only recent versions are a preview version and a non-preview of the same date, $recentApiVersions will only contain the preview
# due to sorting, which is incorrect
            $recentApiVersions += "        $v`n"
        }
        if ($v -like "$apiVersion") {
# If this looks like the apiVersion,
            $howOutOfDate = $n         # keep track of how out-of-date it is.
        }
        $n++
    }

#if latest stable is already in list, deduplicate
    $recentApiVersions = $recentApiVersions | Select-Object -Unique

    
# Is the apiVersion even in the list?
    if ($howOutOfDate -eq -1 -and $validApiVersions) {
# Removing the error for this now - this is happening with the latest versions and outdated manifests
# We can assume that if the version is indeed invalid, deployment will fail
# Write-Error "$potentialResourceType is using an invalid apiVersion." -ErrorId ApiReference.Version.Not.Valid -TargetObject $foundRef
# Write-Output "ApiVersion not found for: $($foundRef.Value) and version $($av.apiVersion)" 
# Write-Output "Valid Api Versions found $potentialResourceType :`n$recentApiVersions"
    }

    if ($ApiVersion -like '*-*-*-*') {
# If it's a preview or other special variant, e.g. 2016-01-01-preview

        $moreRecent = $validApiVersions[0..$howOutOfDate] # see if there's a more recent non-preview version. 
        if ($howOutOfDate -gt 0 -and $moreRecent -notlike '*-*-*-*') {
            Write-Error "$($foundRef.Value)  uses a preview version ( $($apiVersion) ) and there are more recent versions available." -TargetObject $foundRef -ErrorId ApiReference.Version.Preview.Not.Recent
            Write-Output "Valid Api Versions $potentialResourceType :`n$recentApiVersions"
        }

# the sorted array doesn't work perfectly so 2020-01-01-preview comes before 2020-01-01
# in this case if the dates are the same, the non-preview version should be used
        if ($howOutOfDate -eq 0 -and $validApiVersions.Count -gt 1) {
# check the second apiVersion and see if it matches the preview one
            $nextApiVersion = $validApiVersions[1]
# strip the qualifier on the apiVersion and see if it matches the next one in the sorted array
            $truncatedApiVersion = $($apiVersion).Substring(0, $($ApiVersion).LastIndexOf("-"))
            if ($nextApiVersion -eq $truncatedApiVersion) {
                Write-Error "$($foundRef.Value) uses a preview version ( $($apiVersion) ) and there is a non-preview version for that apiVersion available." -TargetObject $foundRef -ErrorId ApiReference.Version.Preview.Version.Has.NonPreview
                Write-Output "Valid Api Versions for $potentialResourceType :`n$recentApiVersions"                
            } 
        }     
    }

# Finally, check how long it's been since the ApiVersion's date
    $timeSinceApi = $TestDate - $apiDate
    if (($timeSinceApi.TotalDays -gt $NumberOfDays) -and ($howOutOfDate -gt 0)) {
#if the used apiVersion is the second in the list, check to see if the first in the list is the same preview version (due to sorting)
# for example: "2017-12-01-preview" and "2017-12-01" - the preview is sorted first so we think we're out of date
        $nonPreviewVersionInUse = $false
        if ($howOutOfDate -eq 1) { 
            $trimmedApiVersion = $validApiVersions[0].ToString().Substring(0, $validApiVersions[0].ToString().LastIndexOf("-"))
            $nonPreviewVersionInUse = ($trimmedApiVersion -eq $apiVersion)
        }
        if (-not $nonPreviewVersionInUse) {
            if ($($apiVersion) -eq $latestStableApiVersion) {     
# break from loop to avoid throwing error when using latest stable API version           
                break
            }
# If it's older than two years, and there's nothing more recent
            Write-Error "Api versions must be the latest or under $($NumberOfDays / 365) years old ($NumberOfDays days) - API version used by:`n            $($foundRef.Value)`n        is $([Math]::Floor($timeSinceApi.TotalDays)) days old" -ErrorId ApiReference.Version.OutOfDate -TargetObject $foundRef
            Write-Output "Valid Api Versions for $potentialResourceType :`n$recentApiVersions"
        }
    }

    if (! $validApiVersions.Contains($apiVersion)) {
        Write-Warning "The apiVersion $($apiVersion) was not found for the resource type: $potentialResourceType"
    }

}
*/
        public new const string Code = "use-recent-api-versions";
        public const int MaxAllowedAgeInDays = 365 * 2;

        // Debug/test switch: Pretend today is a different date
        private DateTime today = DateTime.Today;

        // Debug/test switch: Warn if the resource type or API version are not found (normally we don't
        // give errors for these because Bicep always provides a warning about types not being available)
        private bool warnNotFound = false;

        public record Failure(
            TextSpan Span,
            string ResourceType,
            string Reason,
            ApiVersion[] AcceptableVersions,
            CodeFix[] Fixes
        );

        public UseRecentApiVersionRule() : base(
            code: Code,
            description: CoreResources.UseRecentApiVersionRule_Description,
            docUri: new Uri($"https://aka.ms/bicep/linter/{Code}"),
            diagnosticStyling: DiagnosticStyling.Default,
            diagnosticLevel: DiagnosticLevel.Off
        )
        { }

        public override void Configure(AnalyzersConfiguration config)
        {
            base.Configure(config);

            // Today's date can be changed to enable testing/debug scenarios
            string? testToday = this.GetConfigurationValue<string?>("test-today", null);
            if (testToday is not null)
            {
                this.today = ApiVersionHelper.ParseDateFromApiVersion(testToday);
            }

            // Testing/debug: Warn if the resource type and/or API version are not found
            bool debugWarnNotFound = this.GetConfigurationValue<bool>("test-warn-not-found", false);
            this.warnNotFound = debugWarnNotFound == true;

        }

        public override string FormatMessage(params object[] values)
        {
            var resourceType = (string)values[0];
            var reason = (string)values[1];
            var acceptableVersions = (ApiVersion[])values[2];

            var acceptableVersionsString = string.Join(", ", acceptableVersions.Select(v => v.Formatted));
            return string.Format(CoreResources.UseRecentApiVersionRule_ErrorMessageFormat, resourceType)
                + (" " + reason)
                + (acceptableVersionsString.Any() ? " " + string.Format(CoreResources.UseRecentApiVersionRule_AcceptableVersions, acceptableVersionsString) : "");
        }

        override public IEnumerable<IDiagnostic> AnalyzeInternal(SemanticModel model)
        {
            foreach (var resource in model.DeclaredResources)
            {
                if (resource.Symbol.DeclaringSyntax is ResourceDeclarationSyntax declarationSyntax
                    && AnalyzeResource(model, declarationSyntax) is Failure failure)
                {
                    yield return CreateFixableDiagnosticForSpan(
                        failure.Span,
                        failure.Fixes,
                        failure.ResourceType,
                        failure.Reason,
                        failure.AcceptableVersions);
                }
            }
        }

        public Failure? AnalyzeResource(SemanticModel model, ResourceDeclarationSyntax resourceDeclarationSyntax)
        {
            if (model.GetSymbolInfo(resourceDeclarationSyntax) is not ResourceSymbol resourceSymbol)
            {
                return null;
            }

            if (model.DeclaredResources.FirstOrDefault(r => r.Symbol == resourceSymbol) is not DeclaredResourceMetadata declaredResourceMetadata
                || !declaredResourceMetadata.IsAzResource)
            {
                // Skip if it's not an Az resource or is invalid
                return null;
            }

            if (resourceSymbol.TryGetResourceTypeReference() is ResourceTypeReference resourceTypeReference &&
                resourceTypeReference.ApiVersion is string apiVersionString &&
                GetReplacementSpan(resourceSymbol, apiVersionString) is TextSpan replacementSpan)
            {
                string fullyQualifiedResourceType = resourceTypeReference.FormatType();
                var (date, suffix) = ApiVersionHelper.TryParse(apiVersionString);
                if (date != null)
                {
                    var failure = AnalyzeApiVersion(model.Compilation.ApiVersionProvider, replacementSpan, model.TargetScope, fullyQualifiedResourceType, new ApiVersion(date, suffix));
                    if (failure is not null)
                    {
                        return failure;
                    }
                }
            }

            return null;
        }

        public Failure? AnalyzeApiVersion(IApiVersionProvider apiVersionProvider, TextSpan replacementSpan, ResourceScope scope, string fullyQualifiedResourceType, ApiVersion actualApiVersion)
        {
            var (allApiVersions, acceptableApiVersions) = GetAcceptableApiVersions(apiVersionProvider, today, MaxAllowedAgeInDays, scope, fullyQualifiedResourceType);
            if (!allApiVersions.Any())
            {
                // Resource type not recognized
                if (warnNotFound)
                {
                    return new Failure(replacementSpan, fullyQualifiedResourceType, $"Could not find resource type {fullyQualifiedResourceType}", Array.Empty<ApiVersion>(), Array.Empty<CodeFix>());
                }
                return null;
            }

            Debug.Assert(acceptableApiVersions.Any(), $"There should always be at least one acceptable version for a valid resource type: {fullyQualifiedResourceType} (scope {scope})");
            if (acceptableApiVersions.Contains(actualApiVersion))
            {
                // Passed - version is acceptable
                return null;
            }

            if (!allApiVersions.Contains(actualApiVersion))
            {
                // apiVersion for resource type not recognized.
                if (warnNotFound)
                {
                    return CreateFailure(replacementSpan, fullyQualifiedResourceType, $"Could not find apiVersion {actualApiVersion.Formatted} for {fullyQualifiedResourceType}", acceptableApiVersions);
                }

                return null;
            }

            // At this point, the rule has failed. Just need to determine reason for failure, for the message.
            string? failureReason = null;

            // Is it because the version is recent but in preview, and there's a newer stable version available?
            if (actualApiVersion.IsPreview && IsRecent(actualApiVersion, today, MaxAllowedAgeInDays))
            {
                var mostRecentStableVersion = GetNewestDateOrNull(FilterStable(allApiVersions));
                if (mostRecentStableVersion is not null)
                {
                    var comparison = DateTime.Compare(actualApiVersion.Date, mostRecentStableVersion.Value);
                    var stableIsMoreRecent = comparison < 0;
                    var stableIsSameDate = comparison == 0;
                    if (stableIsMoreRecent)
                    {
                        failureReason = $"'{actualApiVersion.Formatted}' is a preview version and there is a more recent non-preview version available.";
                    }
                    else if (stableIsSameDate)
                    {
                        failureReason = $"'{actualApiVersion.Formatted}' is a preview version and there is a non-preview version available with the same date.";
                    }
                }
            }
            if (failureReason is null)
            {
                int ageInDays = today.Subtract(actualApiVersion.Date).Days;
                failureReason = $"'{actualApiVersion.Formatted}' is {ageInDays} days old, should be no more than {MaxAllowedAgeInDays} days old.";
            }

            Debug.Assert(failureReason is not null);
            return CreateFailure(
                replacementSpan,
                fullyQualifiedResourceType,
                failureReason,
                acceptableApiVersions);
        }

        public static (ApiVersion[] allApiVersions, ApiVersion[] acceptableVersions) GetAcceptableApiVersions(IApiVersionProvider apiVersionProvider, DateTime today, int maxAllowedAgeInDays, ResourceScope scope, string fullyQualifiedResourceType)
        {
            var allVersions = apiVersionProvider.GetApiVersions(scope, fullyQualifiedResourceType).ToArray();
            if (!allVersions.Any())
            {
                // The resource type is not recognized.
                return (allVersions, Array.Empty<ApiVersion>());
            }

            var stableVersionsSorted = FilterStable(allVersions).OrderBy(v => v.Date).ToArray();
            var previewVersionsSorted = FilterPreview(allVersions).OrderBy(v => v.Date).ToArray();

            var recentStableVersionsSorted = FilterRecent(stableVersionsSorted, today, maxAllowedAgeInDays).ToArray();
            var recentPreviewVersionsSorted = FilterRecent(previewVersionsSorted, today, maxAllowedAgeInDays).ToArray();

            // Start with all recent stable versions
            List<ApiVersion> acceptableVersions = recentStableVersionsSorted.ToList();

            // If no recent stable versions, add the most recent stable version, if any
            if (!acceptableVersions.Any())
            {
                acceptableVersions.AddRange(FilterMostRecentApiVersion(stableVersionsSorted));
            }

            // Add any recent (not old) preview versions that are newer than the newest stable version
            var mostRecentStableDate = GetNewestDateOrNull(stableVersionsSorted);
            if (mostRecentStableDate != null)
            {
                Debug.Assert(stableVersionsSorted.Any(), "There should have been at least one stable version since mostRecentStableDate != null");
                var previewsNewerThanMostRecentStable = recentPreviewVersionsSorted.Where(v => IsMoreRecentThan(v.Date, mostRecentStableDate.Value));
                acceptableVersions.AddRange(previewsNewerThanMostRecentStable);
            }
            else
            {
                // There are no stable versions available at all - add all preview versions that are recent enough
                acceptableVersions.AddRange(recentPreviewVersionsSorted);

                // If there are no recent preview versions, add the newest preview only
                if (!acceptableVersions.Any())
                {
                    acceptableVersions.AddRange(FilterMostRecentApiVersion(previewVersionsSorted));
                    Debug.Assert(acceptableVersions.Any(), "There should have been at least one preview version available to add");
                }
            }

            // Sort
            var acceptableVersionsSorted = acceptableVersions
                .OrderByDescending(v => v.Date) // first by date
                .ThenBy(v => v.IsStable ? 0 : 1) // then stable/preview (stable first)
                .ThenBy(v => v.Suffix, StringComparer.OrdinalIgnoreCase) // then alphabetically by suffix
                .ToArray();

            Debug.Assert(acceptableVersions.Any(), $"Didn't find any acceptable API versions for {fullyQualifiedResourceType}");
            return (allVersions, acceptableVersionsSorted);
        }

        // Find the portion of the resource.type@api-version string that corresponds to the api version
        private static TextSpan? GetReplacementSpan(ResourceSymbol resourceSymbol, string apiVersion)
        {
            if (resourceSymbol.DeclaringResource.TypeString is StringSyntax typeString &&
                typeString.StringTokens.First() is Token token)
            {
                int replacementSpanStart = token.Span.Position + token.Text.IndexOf(apiVersion);

                return new TextSpan(replacementSpanStart, apiVersion.Length);
            }

            return null;
        }

        private static Failure CreateFailure(TextSpan span, string fullyQualifiedResourceType, string reason, ApiVersion[] acceptableVersionsSorted)
        {
            // For now, always choose the most recent for the suggested auto-fix
            var preferredVersion = acceptableVersionsSorted[0];
            var codeReplacement = new CodeReplacement(span, preferredVersion.Formatted);

            var fix = new CodeFix(
                string.Format(CoreResources.UseRecentApiVersionRule_Fix_ReplaceApiVersion, preferredVersion.Formatted),
                isPreferred: true,
                CodeFixKind.QuickFix,
                codeReplacement);

            return new Failure(span, fullyQualifiedResourceType, reason, acceptableVersionsSorted, new CodeFix[] { fix });
        }

        private static DateTime? GetNewestDateOrNull(IEnumerable<ApiVersion> apiVersions)
        {
            return apiVersions.Any() ? apiVersions.Max(v => v.Date) : null;
        }

        // Retrieves the most recent API version (this could be more than one if there are multiple apiVersions
        //   with the same, most recent date, but different suffixes)
        private static IEnumerable<ApiVersion> FilterMostRecentApiVersion(IEnumerable<ApiVersion> apiVersions)
        {
            var mostRecentDate = GetNewestDateOrNull(apiVersions);
            if (mostRecentDate is not null)
            {
                return FilterByDateEquals(apiVersions, mostRecentDate.Value);
            }

            return Array.Empty<ApiVersion>();
        }

        private static IEnumerable<ApiVersion> FilterByDateEquals(IEnumerable<ApiVersion> apiVersions, DateTime date)
        {
            Debug.Assert(date == date.Date);
            return apiVersions.Where(v => v.Date == date);
        }

        // Recent meaning < maxAllowedAgeInDays old
        private static bool IsRecent(ApiVersion apiVersion, DateTime today, int maxAllowedAgeInDays)
        {
            return apiVersion.Date >= today.AddDays(-maxAllowedAgeInDays);
        }

        // Recent meaning < maxAllowedAgeInDays old
        private static IEnumerable<ApiVersion> FilterRecent(IEnumerable<ApiVersion> apiVersions, DateTime today, int maxAllowedAgeInDays)
        {
            return apiVersions.Where(v => IsRecent(v, today, maxAllowedAgeInDays));
        }

        private static IEnumerable<ApiVersion> FilterPreview(IEnumerable<ApiVersion> apiVersions)
        {
            return apiVersions.Where(v => v.IsPreview);
        }

        private static IEnumerable<ApiVersion> FilterStable(IEnumerable<ApiVersion> apiVersions)
        {
            return apiVersions.Where(v => v.IsStable);
        }

        private static bool IsMoreRecentThan(DateTime date, DateTime other)
        {
            return DateTime.Compare(date, other) > 0;
        }
    }
}
