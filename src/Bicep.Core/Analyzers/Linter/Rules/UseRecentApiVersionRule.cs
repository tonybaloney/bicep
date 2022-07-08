// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bicep.Core.ApiVersion;
using Bicep.Core.ApiVersions;
using Bicep.Core.CodeAction;
using Bicep.Core.Configuration;
using Bicep.Core.Diagnostics;
using Bicep.Core.Parsing;
using Bicep.Core.Resources;
using Bicep.Core.Semantics;
using Bicep.Core.Syntax;

namespace Bicep.Core.Analyzers.Linter.Rules
{
    //asdfg prefix -> suffix

    //    //asdfg
    //      if ($FullResourceTypes -like '*/providers/*') {
    //        # If we have a provider resources
    //        $FullResourceTypes = @($FullResourceTypes -split '/')
    //        if ($av.Name -match "'/{0,}(?<ResourceType>\w+\.\w+)/{0,}'") {
    //            $FullResourceTypes = @($matches.ResourceType)
    //        }
    //        else
    //{
    //    Write - Warning "Could not identify provider resource for $($FullResourceTypes -join '/')"
    //            continue
    //        }
    //    }

    //asdfg update
    // Adds linter rule to flag an issue when api version used in resource is not recent
    // 1. Any GA version is allowed as long as it's less than years old, even if there is a more recent GA version
    // 2. If there is no GA apiVersion less than 2 years old, then accept only the latest one GA version available
    // 3. A non-stable version (api version with any -* suffix, such as -preview) is accepted only if it is latest and there is no later GA version
    // 4. For non preview versions(e.g. alpha, beta, privatepreview and rc), order of preference is latest GA -> Preview -> Non Preview   asdf????
    public sealed class UseRecentApiVersionRule : LinterRuleBase
    {
        public new const string Code = "use-recent-api-version";
        public const int MaxAllowedAgeInDays = 365 * 2;

        private DateTime today = DateTime.Today;

        public UseRecentApiVersionRule() : base(
            code: Code,
            description: CoreResources.UseRecentApiVersionRuleDescription,
            docUri: new Uri($"https://aka.ms/bicep/linter/{Code}"),
            diagnosticStyling: DiagnosticStyling.Default)
        {
        }

        public override void Configure(AnalyzersConfiguration config)
        {
            base.Configure(config);

            // Today's date can be changed to enable testing/debug scenarios
            string? debugToday = this.GetConfigurationValue<string?>("debug-today", null);
            if (debugToday is not null)
            {
                this.today = ApiVersionHelper.ParseDate(debugToday);
            }
        }

        override public IEnumerable<IDiagnostic> AnalyzeInternal(SemanticModel model)
        {
            var visitor = new Visitor(model, today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            visitor.Visit(model.SourceFile.ProgramSyntax);

            return visitor.Fixes.Select(fix => CreateFixableDiagnosticForSpan(fix.Span, fix.Fix));
        }

        public sealed class Visitor : SyntaxVisitor
        {
            internal readonly List<(TextSpan Span, CodeFix Fix)> Fixes = new();

            private readonly IApiVersionProvider apiVersionProvider;
            private readonly SemanticModel model;
            private readonly DateTime today;
            private readonly int maxAllowedAgeInDays;


            public Visitor(SemanticModel model, DateTime today, int maxAllowedAgeInDays)
            {
                this.apiVersionProvider = model.ApiVersionProvider ?? new ApiVersionProvider();
                this.model = model;
                this.today = today;
                this.maxAllowedAgeInDays = maxAllowedAgeInDays;
            }

            public override void VisitResourceDeclarationSyntax(ResourceDeclarationSyntax resourceDeclarationSyntax)
            {
                ResourceSymbol resourceSymbol = new ResourceSymbol(model.SymbolContext, resourceDeclarationSyntax.Name.IdentifierName, resourceDeclarationSyntax);

                if (resourceSymbol.TryGetResourceTypeReference() is ResourceTypeReference resourceTypeReference &&
                    resourceTypeReference.ApiVersion is string apiVersion &&
                    GetReplacementSpan(resourceSymbol, apiVersion) is TextSpan replacementSpan)
                {
                    string fullyQualifiedResourceType = resourceTypeReference.FormatType();
                    var fix = CreatePossibleDiagnostic(replacementSpan, fullyQualifiedResourceType, apiVersion);

                    if (fix is not null)
                    {
                        Fixes.Add(fix.Value);
                    }
                }

                base.VisitResourceDeclarationSyntax(resourceDeclarationSyntax);
            }

            public (TextSpan span, CodeFix fix)? CreatePossibleDiagnostic(TextSpan replacementSpan/*asdfg*/, string fullyQualifiedResourceType, string actualApiVersion)
            {
                (string? currentApiDate, string? actualApiSuffix) = ApiVersionHelper.TryParse(actualApiVersion);
                if (currentApiDate is null)
                {//asdfg testpoint
                    // The API version is not valid. Bicep will show an error, so we don't want to add anything else
                    return null;
                }

                var acceptableVersions = GetAcceptableApiVersions(apiVersionProvider, today, maxAllowedAgeInDays, fullyQualifiedResourceType);
                if (!acceptableVersions.Any())
                {
                    // Bicep will show a warning, so we don't want to add anything else
                }

                if (acceptableVersions.Contains(actualApiVersion)) //asdfg case insensitive
                {
                    // Nothing to do
                }


                //asdfg
                //string? recentGAVersion = apiVersionProvider.GetRecentApiVersion(fullyQualifiedResourceType, ApiVersionSuffixes.GA);

                ////asdfg
                //if (string.IsNullOrEmpty(actualApiSuffix))
                //{
                //    return CreateCodeFixIfGAVersionIsNotLatest(span,
                //                                    recentGAVersion,
                //                                    currentApiDate);
                //}
                //else
                //{
                //    string? recentNonPreviewVersion = apiVersionProvider.GetRecentApiVersion(fullyQualifiedResourceType, actualApiSuffix); //asdfg what are the rules here?
                //    string? recentPreviewVersion = apiVersionProvider.GetRecentApiVersion(fullyQualifiedResourceType, ApiVersionSuffixes.Preview);

                //    return CreateCodeFixIfNonGAVersionIsNotLatest(span,
                //                                        recentGAVersion,
                //                                        recentPreviewVersion,
                //                                        recentNonPreviewVersion,
                //                                        actualApiSuffix,
                //                                        currentApiDate);
                //}

                return null;
            }

            public static string[] GetAcceptableApiVersions(IApiVersionProvider apiVersionProvider, DateTime today, int maxAllowedAgeInDays, string fullyQualifiedResourceType)
            {
                var allVersionsSorted = apiVersionProvider.GetSortedValidApiVersions(fullyQualifiedResourceType);
                if (!allVersionsSorted.Any())
                { //asdfg testpoint
                    // The resource type is not recognized.
                    return Array.Empty<string>();
                }

                var lastAcceptableRecentDate = ApiVersionHelper.Format(today.AddDays(-maxAllowedAgeInDays), null);

                var stableVersionsSorted = allVersionsSorted.Where(v => !ApiVersionHelper.IsPreviewVersion(v)).ToArray();
                var previewVersionsSorted = allVersionsSorted.Where(v => ApiVersionHelper.IsPreviewVersion(v)).ToArray();

                var recentStableVersionsSorted = FilterRecentVersions(stableVersionsSorted, lastAcceptableRecentDate).ToArray();
                var recentPreviewVersionsSorted = FilterRecentVersions(previewVersionsSorted, lastAcceptableRecentDate).ToArray();

                // Start with all recent stable versions
                List<string> acceptableVersions = recentStableVersionsSorted.ToList();

                // Add any recent preview versions
                acceptableVersions.AddRange(recentPreviewVersionsSorted);

                // If there are no recent stable versions...
                if (!recentStableVersionsSorted.Any())
                {
                    // Allow the most recent, stable version even though it's old
                    if (stableVersionsSorted.Any())
                    {
                        acceptableVersions.Add(stableVersionsSorted.Last());
                    }

                    // If there are also no recent preview resources, allow only those with the most recent date
                    // that is newer than the stable one (only allow a single date - in the weird case where there are
                    // multiple with that same date, take them all)
                    if (!recentPreviewVersionsSorted.Any())
                    {
                        var mostRecentPreviewDate = previewVersionsSorted.Max(v => ApiVersionHelper.TryParse(v).date);
                        if (mostRecentPreviewDate is not null)
                        {
                            var mostRecentPreviewVersions = previewVersionsSorted.Where(v => ApiVersionHelper.CompareApiVersionDates(v, mostRecentPreviewDate) == 0);
                            acceptableVersions.AddRange(mostRecentPreviewVersions);
                        }
                    }
                }

                return acceptableVersions.ToArray();
            }

            private static IEnumerable<string> FilterRecentVersions(string[] apiVersions, string lastAcceptableRecentDate)
            {
                return apiVersions.Where(v => ApiVersionHelper.CompareApiVersionDates(v, lastAcceptableRecentDate) >= 0).ToArray(); //asdfg test
            }

            private TextSpan? GetReplacementSpan(ResourceSymbol resourceSymbol, string apiVersion)
            {
                if (resourceSymbol.DeclaringResource.TypeString is StringSyntax typeString &&
                    typeString.StringTokens.First() is Token token)
                {
                    int replacementSpanStart = token.Span.Position + token.Text.IndexOf(apiVersion);

                    return new TextSpan(replacementSpanStart, apiVersion.Length);
                }

                return null;
            }

            // 1. Any GA version is allowed as long as it's not > 2 years old, even if there is a more recent GA version
            // 2. If there is no GA apiVersion less than 2 years old, then take the latest one available from the cache of GA versions
            public (TextSpan Span, CodeFix Fix)? CreateCodeFixIfGAVersionIsNotLatest(TextSpan span,
                                                         string? recentGAVersion,
                                                         string? currentApiVersion)
            {
                if (currentApiVersion is null || recentGAVersion is null)
                {
                    return null;
                }

                DateTime currentApiVersionDate = DateTime.Parse(currentApiVersion);

                if (today.Year - currentApiVersionDate.Year <= 2) //asdfg
                {
                    return null;
                }

                DateTime recentGAVersionDate = DateTime.Parse(recentGAVersion);

                if (DateTime.Compare(recentGAVersionDate, currentApiVersionDate) > 0)
                {
                    return CreateCodeFix(span, recentGAVersion);
                }

                return null;
            }

            // A preview version is valid only if it is latest and there is no later GA version
            // For non preview versions like alpha, beta, privatepreview and rc, order of preference is latest GA -> Preview -> Non Preview 
            public (TextSpan Span, CodeFix Fix)? CreateCodeFixIfNonGAVersionIsNotLatest(TextSpan span,
                                                            string? recentGAVersion,
                                                            string? recentPreviewVersion,
                                                            string? recentNonPreviewVersion,
                                                            string prefix,
                                                            string? currentVersion)
            {
                if (currentVersion is null)
                {
                    return null;
                }

                DateTime currentVersionDate = DateTime.Parse(currentVersion);

                Dictionary<string, DateTime> prefixToRecentApiVersionMap = new Dictionary<string, DateTime>(); //asdfg misnamed

                if (prefix.Equals(ApiVersionSuffixes.Preview))
                {
                    if (recentGAVersion is not null)
                    {
                        prefixToRecentApiVersionMap.Add(recentGAVersion, DateTime.Parse(recentGAVersion));
                    }

                    if (recentPreviewVersion is not null)
                    {
                        prefixToRecentApiVersionMap.Add(recentPreviewVersion + prefix, DateTime.Parse(recentPreviewVersion));
                    }
                }
                else
                {
                    if (recentGAVersion is not null)
                    {
                        prefixToRecentApiVersionMap.Add(recentGAVersion, DateTime.Parse(recentGAVersion));
                    }

                    if (recentNonPreviewVersion is not null)
                    {
                        prefixToRecentApiVersionMap.Add(recentNonPreviewVersion + prefix, DateTime.Parse(recentNonPreviewVersion));
                    }

                    if (recentPreviewVersion is not null)
                    {
                        prefixToRecentApiVersionMap.Add(recentPreviewVersion + ApiVersionSuffixes.Preview, DateTime.Parse(recentPreviewVersion));
                    }
                }

                if (prefixToRecentApiVersionMap.Any())
                {
                    var sortedPrefixToRecentApiVersionDateMap = prefixToRecentApiVersionMap.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

                    KeyValuePair<string, DateTime> kvp = sortedPrefixToRecentApiVersionDateMap.First();

                    if (DateTime.Compare(kvp.Value, currentVersionDate) >= 0)
                    {//asdfg?
                        Trace.WriteLine("Preview version");
                        Trace.WriteLine("Date1: " + kvp.Value);
                        Trace.WriteLine("Date2: " + currentVersionDate);

                        return CreateCodeFix(span, kvp.Key);
                    }
                }

                return null;
            }

            private (TextSpan Span, CodeFix Fix) CreateCodeFix(TextSpan span, string apiVersion)
            {
                var codeReplacement = new CodeReplacement(span, apiVersion);
                string description = string.Format(CoreResources.UseRecentApiVersionRuleMessageFormat, apiVersion);
                var fix = new CodeFix(description, true, CodeFixKind.QuickFix, codeReplacement);

                return (span, fix);
            }
        }
    }
}
