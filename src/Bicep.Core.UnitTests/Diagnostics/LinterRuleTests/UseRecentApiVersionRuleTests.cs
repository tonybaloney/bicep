// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Bicep.Core.Analyzers.Linter;
using Bicep.Core.Analyzers.Linter.Rules;
using Bicep.Core.ApiVersion;
using Bicep.Core.ApiVersions;
using Bicep.Core.Configuration;
using Bicep.Core.Json;
using Bicep.Core.Parsing;
using Bicep.Core.Semantics;
using Bicep.Core.UnitTests.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Bicep.Core.Analyzers.Linter.Rules.UseRecentApiVersionRule;

// asdfg test with case sensitivity
// asdfg test with different scopes
// asdfg does it need to understand imported types?
// asdfg test two years
//asdfg deployment scopes
namespace Bicep.Core.UnitTests.Diagnostics.LinterRuleTests
{
    [TestClass]
    public class UseRecentApiVersionRuleTests : LinterRuleTestsBase
    {
        // Welcome to the 25th century!
        // All tests using CompileAndTest set this value as "today" as far as the rule is concerned
        private const string FAKE_TODAY_DATE = "2422-07-04";

        public UseRecentApiVersionRuleTests()
        {

        }

        private void CompileAndTest(string bicep, OnCompileErrors onCompileErrors, params string[] expectedMessagesForCode)
        {
            // Test with today's date and real types.
            AssertLinterRuleDiagnostics(UseRecentApiVersionRule.Code,
               bicep,
               expectedMessagesForCode,
               onCompileErrors,
               IncludePosition.LineNumber);
        }

        private void CompileAndTestWithFakeDateAndTypes(string bicep, params string[] expectedMessagesForCode)
        {
            // Test with the linter thinking today's date is FAKE_TODAY_DATE and also fake resource types from FakeResourceTypes
            // Note: The compiler does not know about these fake types, only the linter.

            AssertLinterRuleDiagnostics(UseRecentApiVersionRule.Code,
                bicep,
                expectedMessagesForCode,
                OnCompileErrors.IncludeErrors,
                IncludePosition.LineNumber,
                configuration: ConfigurationWithFakeTodayDate,
                apiVersionProvider: FakeApiVersionProviderResourceScope);
        }

        private void CompileAndTestWithFakeDateAndTypes(string bicep, string[] resourceTypes, string fakeToday, params string[] expectedMessagesForCode)
        {
            // Test with the linter thinking today's date is FAKE_TODAY_DATE and also fake resource types from FakeResourceTypes
            // Note: The compiler does not know about these fake types, only the linter.

            var apiProvider = new ApiVersionProvider(FakeResourceTypes.GetFakeTypes(string.Join('\n', resourceTypes)));

            AssertLinterRuleDiagnostics(UseRecentApiVersionRule.Code,
                bicep,
                expectedMessagesForCode,
                OnCompileErrors.IncludeErrors,
                IncludePosition.LineNumber,
                configuration: CreateConfigurationWithFakeToday(fakeToday),
                apiVersionProvider: apiProvider);
        }

        // Uses fake resource types from FakeResourceTypes
        private readonly IApiVersionProvider FakeApiVersionProviderResourceScope = new ApiVersionProvider(FakeResourceTypes.GetFakeTypes(FakeResourceTypes.ResourceScope));

        // Uses fake today's date
        private static RootConfiguration ConfigurationWithFakeTodayDate = CreateConfigurationWithFakeToday(FAKE_TODAY_DATE);

        public static SemanticModel SemanticModel => new Compilation(
           BicepTestConstants.Features,
           TestTypeHelper.CreateEmptyProvider(),
           SourceFileGroupingFactory.CreateFromText(string.Empty, BicepTestConstants.FileResolver),
           BicepTestConstants.BuiltInConfiguration,
           BicepTestConstants.ApiVersionProvider,
           new LinterAnalyzer(ConfigurationWithFakeTodayDate)).GetEntrypointSemanticModel();

        private static RootConfiguration CreateConfigurationWithFakeToday(string today)
        {
            return new RootConfiguration(
                BicepTestConstants.BuiltInConfiguration.Cloud,
                BicepTestConstants.BuiltInConfiguration.ModuleAliases,
                    new AnalyzersConfiguration(
                         JsonElementFactory.CreateElement(@"
                            {
                              ""core"": {
                                ""enabled"": true,
                                ""rules"": {
                                  ""use-recent-api-version"": {
                                      ""level"": ""warning"",
                                      ""debug-today"": ""<TESTING_TODAY_DATE>""
                                  }
                                }
                              }
                            }".Replace("<TESTING_TODAY_DATE>", today))),
                null);
        }

        [TestClass]
        public class GetAcceptableApiVersions
        {
            //private void TestGetAcceptableApiVersions(string[] resourceTypes, string today, string[] expectedApiVersions, int maxAllowedAgeInDays = UseRecentApiVersionRule.MaxAllowedAgeInDays)
            //{
            //    TestGetAcceptableApiVersions(
            //        string.Join('\n', resourceTypes),
            //        today,
            //        expectedApiVersions,
            //        maxAllowedAgeInDays);
            //}

            private void TestGetAcceptableApiVersions(string fullyQualifiedResourceType, string resourceTypes, string today, string[] expectedApiVersions, int maxAllowedAgeInDays = UseRecentApiVersionRule.MaxAllowedAgeInDays)
            {
                var apiVersionProvider = new ApiVersionProvider(FakeResourceTypes.GetFakeTypes(resourceTypes));
                var allowedVersions = Visitor.GetAcceptableApiVersions(apiVersionProvider, ApiVersionHelper.ParseDate(today), maxAllowedAgeInDays, fullyQualifiedResourceType);
                allowedVersions.Should().BeEquivalentTo(expectedApiVersions);
            }


            [TestMethod]
            public void GAAPI_CaseInsensitiveResourceType()
            {
                TestGetAcceptableApiVersions(
                    "Fake.KUSTO/clusters",
                    @"
                        Fake.Kusto/clusters@2418-09-07-preview
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2418-09-07-preview",
                    });
            }

            [TestMethod]
            public void GAAPI_CaseInsensitiveApiSuffix()
            {
                TestGetAcceptableApiVersions(
                    "Fake.KUSTO/clusters",
                    @"
                        Fake.Kusto/clusters@2418-09-07-PREVIEW
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2418-09-07-preview",
                    });
            }

            [TestMethod]
            public void GAAPI_ResourceTypeNotRecognized_ReturnNone()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kisto/clusters",
                    @"
                        Fake.Kusto/clusters@2421-01-01
                    ",
                    "2421-07-07",
                    new string[]
                    {
                    });
            }

            [TestMethod]
            public void GAAPI_NoStable_OldPreview_PickOnlyMostRecentPreview()
            {
                //asdfg
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2413-05-15-beta
                        Fake.Kusto/clusters@2413-09-07-alpha
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-09-07-alpha",
                    });
            }

            [TestMethod]
            public void GAAPI_NoStable_OldPreview_PickOnlyMostRecentPreview_MultiplePreviewWithSameDate()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2413-05-15-beta
                        Fake.Kusto/clusters@2413-09-07-alpha
                        Fake.Kusto/clusters@2413-09-07-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-09-07-alpha",
                        "2413-09-07-beta",
                    });
            }

            [TestMethod]
            public void GAAPI_NoStable_NewPreview_PickNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2419-07-21-preview
                        Fake.Kusto/clusters@2419-08-15-beta
                        Fake.Kusto/clusters@2419-09-07-alpha
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2419-07-21-preview",
                        "2419-08-15-beta",
                        "2419-09-07-alpha",
                    });
            }

            [TestMethod]
            public void GGAAPI_NoStable_NewPreview_PickNewPreview_MultiplePreviewHaveSameDate()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2419-07-21-preview
                        Fake.Kusto/clusters@2419-08-15-beta
                        Fake.Kusto/clusters@2419-09-07-alpha
                        Fake.Kusto/clusters@2419-07-21-beta
                        Fake.Kusto/clusters@2419-08-15-privatepreview
                        Fake.Kusto/clusters@2419-09-07-beta
                        Fake.Kusto/clusters@2419-09-07-privatepreview
                    ",

                    "2421-07-07",
                    new string[]
                    {
                        "2419-07-21-preview",
                        "2419-08-15-beta",
                        "2419-09-07-alpha",
                        "2419-07-21-beta",
                        "2419-08-15-privatepreview",
                        "2419-09-07-beta",
                        "2419-09-07-privatepreview",
                    });
            }


            [TestMethod]
            public void GGAAPI_NoStable_OldAndNewPreview_PickNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2419-07-15-privatepreview
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2414-05-15-beta
                        Fake.Kusto/clusters@2415-09-07-alpha
                        Fake.Kusto/clusters@2419-08-21-beta
                        Fake.Kusto/clusters@2419-09-07-beta
                    ",

                    "2421-07-07",
                    new string[]
                    {
                        "2419-08-21-beta",
                        "2419-07-15-privatepreview",
                        "2419-09-07-beta",
                    });
            }

            [TestMethod]
            public void GAAPI_OldStable_NoPreview_PickOnlyMostRecentStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2419-01-21
                        Fake.Kusto/clusters@2419-05-15
                        Fake.Kusto/clusters@2419-09-07
                        Fake.Kusto/clusters@2419-11-09
                        Fake.Kusto/clusters@2420-02-15
                        Fake.Kusto/clusters@2420-06-14
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2500-07-07",
                    new string[]
                    {
                        "2420-09-18",
                    });
            }

            [TestMethod]
            public void GAAPI_OldStable_OldPreview_NewestPreviewIsOlderThanNewestStable_PickOnlyNewestStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15-preview
                        Fake.Kusto/clusters@2413-09-07
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-09-07",
                    });
            }

            [TestMethod]
            public void GAAPI_OldStable_OldPreview_NewestPreviewIsSameAgeAsNewestStable_PickOnlyNewestStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15-preview
                        Fake.Kusto/clusters@2413-06-15
                        Fake.Kusto/clusters@2413-06-15-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-06-15",
                    });
            }

            [TestMethod]
            public void GAAPI_OldStable_OldPreview_NewestPreviewIsNewThanNewestStable_PickNewestStableAndNewestPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15
                        Fake.Kusto/clusters@2413-09-07-preview
                        Fake.Kusto/clusters@2413-09-07-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-06-15",
                        "2413-09-07-preview",
                        "2413-09-07-beta",
                    });
            }

            [TestMethod]
            public void GAAPI_OldStable_NewPreview_PickNewestStableAndNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15
                        Fake.Kusto/clusters@2419-09-07-preview
                        Fake.Kusto/clusters@2419-09-07-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-06-15",
                        "2419-09-07-preview",
                        "2419-09-07-beta",
                    });
            }

            [TestMethod]
            public void GAAPI_OldStable_OldAndNewPreview_PickNewestStableAndNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15-beta
                        Fake.Kusto/clusters@2419-09-07-preview
                        Fake.Kusto/clusters@2419-09-07-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-05-15",
                        "2419-09-07-preview",
                        "2419-09-07-beta",
                    });
            }


            [TestMethod]
            public void GAAPI_NewStable_NoPreview_PickNewStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2419-07-21
                        Fake.Kusto/clusters@2419-08-15
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2419-07-21",
                        "2419-08-15",
                        "2420-09-18",
                    });
            }

            [TestMethod]
            public void GAAPI_NewStable_NewPreview_PickAll()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2419-07-21
                        Fake.Kusto/clusters@2419-07-15-alpha
                        Fake.Kusto/clusters@2419-07-16-beta
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2419-07-21",
                        "2419-07-15-alpha",
                        "2419-07-16-beta",
                        "2420-09-18",
                    });
            }

            [TestMethod]
            public void GAAPI_NewStable_OldAndNewPreview_PickNewStableAndNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2419-07-21
                        Fake.Kusto/clusters@2419-07-15-alpha
                        Fake.Kusto/clusters@2419-07-16-beta
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2419-07-21",
                        "2419-07-15-alpha",
                        "2419-07-16-beta",
                        "2420-09-18",
                    });
            }


            [TestMethod]
            public void GAAPI_OldAndNewStable_NoPreview_PickNewStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2419-01-21
                        Fake.Kusto/clusters@2419-05-15
                        Fake.Kusto/clusters@2419-09-07
                        Fake.Kusto/clusters@2421-01-01
                        Fake.Kusto/clusters@2425-01-01
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2419-09-07",
                        "2421-01-01",
                        "2425-01-01",
                    });
            }



            [TestMethod]
            public void GAAPI_OldAndNewStable_OldPreview_PickNewStable()
            { 
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-09-07-privatepreview
                        Fake.Kusto/clusters@2413-09-07-preview
                        Fake.Kusto/clusters@2414-01-21
                        Fake.Kusto/clusters@2414-05-15
                        Fake.Kusto/clusters@2414-09-07
                        Fake.Kusto/clusters@2415-11-09
                        Fake.Kusto/clusters@2415-02-15
                        Fake.Kusto/clusters@2420-06-14
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2420-06-14",
                        "2420-09-18"
                    });
            }

            [TestMethod]
            public void GAAPI_OldAndNewStable_NewPreview_PickNewStableAndNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2419-09-07-privatepreview
                        Fake.Kusto/clusters@2419-09-07-preview
                        Fake.Kusto/clusters@2414-01-21
                        Fake.Kusto/clusters@2414-05-15
                        Fake.Kusto/clusters@2414-09-07
                        Fake.Kusto/clusters@2415-11-09
                        Fake.Kusto/clusters@2415-02-15
                        Fake.Kusto/clusters@2420-06-14
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2420-06-14",
                        "2420-09-18",
                        "2419-09-07-privatepreview",
                        "2419-09-07-preview",
                    });
            }


            [TestMethod]
            public void GAAPI_OldAndNewStable_OldAndNewPreview_PickNewStableAndNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    @"
                        Fake.Kusto/clusters@2413-09-07-privatepreview
                        Fake.Kusto/clusters@2419-09-07-privatepreview
                        Fake.Kusto/clusters@2419-09-07-preview
                        Fake.Kusto/clusters@2414-01-21
                        Fake.Kusto/clusters@2414-05-15
                        Fake.Kusto/clusters@2414-09-07
                        Fake.Kusto/clusters@2415-11-09
                        Fake.Kusto/clusters@2415-02-15
                        Fake.Kusto/clusters@2420-06-14
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2420-06-14",
                        "2420-09-18",
                        "2419-09-07-privatepreview",
                        "2419-09-07-preview",
                    });
            }



            //asdf newer preview




















        }

        //asdfg will change with time
        [DataRow(@"
            resource dnsZone 'Microsoft.Network/dnsZones@2015-10-01-preview' = {
              name: 'name'
              location: resourceGroup().location
            }",
            "Use API version 2018-05-01")]
        [DataRow(@"
            resource dnsZone 'Microsoft.Network/dnsZones@2017-10-01' = {
              name: 'name'
              location: resourceGroup().location
            }",
            "2018-05-01")]
        [DataRow(@"
            resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' = {
              name: 'name'
              location: resourceGroup().location
            }")]
        [DataRow(@"
            resource containerRegistry 'Microsoft.ContainerRegistry/registries@2020-11-01-preview' = {
              name: 'name'
              location: resourceGroup().location
              sku: {
                name: 'Basic'
              }
            }",
            "2021-06-01-preview")]
        [DataRow(@"
            resource containerRegistry 'Microsoft.ContainerRegistry/registries@2019-05-01' = {
              name: 'name'
              location: resourceGroup().location
              sku: {
                name: 'Basic'
              }
            }")]
        [DataRow(@"
            resource containerRegistry 'Microsoft.ContainerRegistry/registries@2021-06-01-preview' = {
              name: 'name'
              location: resourceGroup().location
              sku: {
                name: 'Basic'
              }
            }")]
        [DataRow(@"
            resource appServicePlan 'Microsoft.Web/serverfarms@2021-01-01' = {
              name: 'name'
              location: resourceGroup().location
            }")]
        [DataTestMethod]
        public void TestRule(string text, params string[] expectedUseRecentApiVersions)
        {
            CompileAndTestWithFakeDateAndTypes(text, expectedUseRecentApiVersions);
        }

        [TestMethod]
        public void AddCodeFixIfGAVersionIsNotLatest_WithCurrentVersionLessThanTwoYearsOld_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-1);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddMonths(-5);
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfGAVersionIsNotLatest(new TextSpan(17, 47),
                                                     recentGAVersion,
                                                     currentVersion);

            fix.Should().BeNull();
        }

        [TestMethod]
        public void AddCodeFixIfGAVersionIsNotLatest_WithCurrentVersionMoreThanTwoYearsOldAndRecentApiVersionIsAvailable_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddMonths(-5);
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);

            var fix = visitor.CreateCodeFixIfGAVersionIsNotLatest(new TextSpan(17, 47),
                                                     recentGAVersion,
                                                     currentVersion);
            fix.Should().NotBeNull();
            fix!.Value.Fix.Description.Should().Be("Use recent API version " + recentGAVersion);
        }

        [TestMethod]
        public void AddCodeFixIfGAVersionIsNotLatest_WithCurrentAndRecentApiVersionsMoreThanTwoYearsOld_ShouldAddDiagnosticsToUseRecentApiVersion()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-4);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-3);
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfGAVersionIsNotLatest(new TextSpan(17, 47),
                                                     recentGAVersion,
                                                     currentVersion);

            fix.Should().NotBeNull();
            fix!.Value.Fix.Description.Should().Be("Use recent API version " + recentGAVersion);
        }

        [TestMethod]
        public void AddCodeFixIfGAVersionIsNotLatest_WhenCurrentAndRecentApiVersionsAreSameAndMoreThanTwoYearsOld_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = currentVersionDate;
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfGAVersionIsNotLatest(new TextSpan(17, 47),
                                                     recentGAVersion,
                                                     currentVersion);

            fix.Should().BeNull();
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenCurrentPreviewVersionIsLatest_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-1);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-3);
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            DateTime recentPreviewVersionDate = currentVersionDate;
            string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionSuffixes.Preview,
                                                        currentVersion);

            fix.Should().BeNull();
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenRecentPreviewVersionIsAvailable_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-5);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-3);
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionSuffixes.Preview,
                                                        currentVersion);

            fix.Should().NotBeNull();
            fix!.Value.Fix.Description.Should().Be("Use recent API version " + recentPreviewVersion + ApiVersionSuffixes.Preview);
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenRecentGAVersionIsAvailable_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-5);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-2);
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-3);
            string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionSuffixes.Preview,
                                                        currentVersion);

            fix.Should().NotBeNull();
            fix!.Value.Fix.Description.Should().Be("Use recent API version " + recentGAVersion);
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenRecentGAVersionIsSameAsPreviewVersion_ShouldAddDiagnosticsUsingGAVersion()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-2);
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            string recentPreviewVersion = recentGAVersion;

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionSuffixes.Preview,
                                                        currentVersion);

            fix.Should().NotBeNull();
            fix!.Value.Fix.Description.Should().Be("Use recent API version " + recentGAVersion);
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenGAVersionisNull_AndCurrentVersionIsNotRecent_ShouldAddDiagnosticsUsingRecentPreviewVersion()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        null,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionSuffixes.Preview,
                                                        currentVersion);

            fix.Should().NotBeNull();
            fix!.Value.Fix.Description.Should().Be("Use recent API version " + recentPreviewVersion + ApiVersionSuffixes.Preview);
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenGAVersionisNull_AndCurrentVersionIsRecent_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-2);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentPreviewVersionDate = currentVersionDate;
            string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        null,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionSuffixes.Preview,
                                                        currentVersion);

            fix.Should().BeNull();
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithNonPreviewVersion_WhenGAVersionisNull_AndPreviewVersionIsRecent_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-1);
            string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);

            DateTime recentNonPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentNonPreviewApiVersion = ApiVersionHelper.Format(recentNonPreviewVersionDate);


            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        null,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionSuffixes.RC,
                                                        currentVersion);

            fix.Should().NotBeNull();
            fix!.Value.Fix.Description.Should().Be("Use recent API version " + recentPreviewVersion + ApiVersionSuffixes.Preview);
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithNonPreviewVersion_WhenGAVersionisRecent_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-1);
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);

            DateTime recentNonPreviewVersionDate = DateTime.Today.AddYears(-3);
            string recentNonPreviewVersion = ApiVersionHelper.Format(recentNonPreviewVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        recentNonPreviewVersion,
                                                        ApiVersionSuffixes.Alpha,
                                                        currentVersion);

            fix.Should().NotBeNull();
            fix!.Value.Fix.Description.Should().Be("Use recent API version " + recentGAVersion);
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithNonPreviewVersion_WhenPreviewAndGAVersionsAreNull_AndNonPreviewVersionIsNotRecent_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentNonPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentNonPreviewVersion = ApiVersionHelper.Format(recentNonPreviewVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        null,
                                                        null,
                                                        recentNonPreviewVersion,
                                                        ApiVersionSuffixes.Alpha,
                                                        currentVersion);

            fix.Should().NotBeNull();
            fix!.Value.Fix.Description.Should().Be("Use recent API version " + recentNonPreviewVersion + ApiVersionSuffixes.Alpha);
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithRecentNonPreviewVersion_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddMonths(-3);
            string currentVersion = ApiVersionHelper.Format(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-1);
            string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);

            DateTime recentNonPreviewVersionDate = DateTime.Today.AddYears(-3);
            string recentNonPreviewVersion = ApiVersionHelper.Format(recentNonPreviewVersionDate);

            Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            var fix = visitor.CreateCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        recentNonPreviewVersion,
                                                        ApiVersionSuffixes.Alpha,
                                                        currentVersion);

            fix.Should().BeNull();
        }

        //asdfg
        //[DataRow("invalid-text")]
        //[DataRow("")]
        //[DataRow("   ")]
        //[TestMethod]
        //public void GetApiVersionDate_WithInvalidVersion(string apiVersion)
        //{
        //    Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);

        //    DateTime? actual = visitor.ApiVersionToDate(apiVersion);

        //    actual.Should().BeNull();
        //}

        //[DataRow("2015-04-01-rc", "2015-04-01")]
        //[DataRow("2016-04-01", "2016-04-01")]
        //[DataRow("2016-04-01-privatepreview", "2016-04-01")]
        //[TestMethod]
        //public void GetApiVersionDate_WithValidVersion(string apiVersion, string expectedVersion)
        //{
        //    Visitor visitor = new Visitor(SemanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);

        //    DateTime? actual = visitor.ApiVersionToDate(apiVersion);

        //    actual.Should().Be(DateTime.Parse(expectedVersion));
        //}

        [TestMethod]
        public void ArmTtk_ApiVersionIsNotAnExpression_Error()
        {
            string bicep = @"
                resource publicIPAddress1 'fake.Network/publicIPAddresses@[concat(\'2020\', \'01-01\')]' = {
                  name: 'publicIPAddress1'
                  location: resourceGroup().location
                  tags: {
                    displayName: 'publicIPAddress1'
                  }
                  properties: {
                    publicIPAllocationMethod: 'Dynamic'
                  }
                }";
            CompileAndTestWithFakeDateAndTypes(bicep, "[1] The resource type is not valid. Specify a valid resource type of format \"<types>@<apiVersion>\".");
        }

        [TestMethod]
        public void NestedResources1_Fail()
        {
            string bicep = @"
                param location string

                resource namespace1 'fake.ServiceBus/namespaces@2018-01-01-preview' = {
                  name: 'namespace1'
                  location: location
                  properties: {
                  }
                }

                // Using 'parent'
                resource namespace1_queue1 'fake.ServiceBus/namespaces/queues@2017-04-01' = {
                  parent: namespace1
                  name: 'queue1'
                }

                // Using 'parent'
                resource namespace1_queue1_rule1 'fake.ServiceBus/namespaces/queues/authorizationRules@2015-08-01' = {
                  parent: namespace1_queue1
                  name: 'rule1'
                }

                // Using nested name
                resource namespace1_queue2 'fake.ServiceBus/namespaces/queues@2017-04-01' = {
                  name: 'namespace1/queue1'
                }

                // Using 'parent'
                resource namespace1_queue2_rule2 'fake.ServiceBus/namespaces/queues/authorizationRules@2018-01-01-preview' = {
                  parent: namespace1_queue2
                  name: 'rule2'
                }

                // Using nested name
                resource namespace1_queue2_rule3 'fake.ServiceBus/namespaces/queues/authorizationRules@2017-04-01' = {
                  name: 'namespace1/queue2/rule3'
                }";

            CompileAndTestWithFakeDateAndTypes(bicep,
                "[3] Use recent API versions",
                "[11] Use recent API versions",
                "[17] Use recent API versions",
                "[23] Use recent API versions",
                "[28] Use recent API versions",
                "[34] Use recent API versions"
                );
        }

        [TestMethod]
        public void NestedResources2_Fail()
        {
            string bicep = @"
                param location string

                // Using resource nesting
                resource namespace2 'Microsoft.ServiceBus/namespaces@2018-01-01-preview' = {
                  name: 'namespace2'
                  location: location

                  resource queue1 'queues@2015-08-01' = {
                    name: 'queue1'
                    location: location

                    resource rule1 'authorizationRules@2018-01-01-preview' = {
                      name: 'rule1'
                    }
                  }
                }

                // Using nested name (parent is a nested resource)
                resource namespace2_queue1_rule2 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2017-04-01' = {
                  name: 'namespace2/queue1/rule2'
                }

                // Using parent (parent is a nested resource)
                resource namespace2_queue1_rule3 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2017-04-01' = {
                  parent: namespace2::queue1
                  name: 'rule3'
                }";

            CompileAndTestWithFakeDateAndTypes(bicep,
                "[4] Use recent API versions",
                "[8] Use recent API versions",
                "[12] Use recent API versions",
                "[19] Use recent API versions",
                "[24] Use recent API versions"
                );
        }

        [TestMethod]
        public void ArmTtk_NotAString_Error()
        {
            string bicep = @"
                resource publicIPAddress1 'Microsoft.Network/publicIPAddresses@True' = {
                name: 'publicIPAddress1'
                location: 'westus'
                tags: {
                    displayName: 'publicIPAddress1'
                }
                properties: {
                    publicIPAllocationMethod: 'Dynamic'
                }
            }
            ";

            CompileAndTestWithFakeDateAndTypes(bicep,
               "[1] The resource type is not valid. Specify a valid resource type of format \"<types>@<apiVersion>\".");
        }

        //asdfg test with non-preview versions at least as recent (plus when only older)
        // current algorithm:
        //   complain if there's a stable with same date as the preview being used (but only if apiversion not found)
        //   comaplin if there's a more recent stable than the preview being used (but only if old)
        // e.g.
        /* 
          "Microsoft.SecurityInsights/threatIntelligence": [
    {
      "aliases": null,
      "apiProfiles": null,
      "apiVersions": [
        "2022-07-01-preview",
        "2022-06-01-preview",
        "2022-05-01-preview",
        "2022-04-01-preview",
        "2022-01-01-preview",
        "2021-10-01-preview",
        "2021-10-01",
        "2021-09-01-preview",
        "2021-04-01",
        "2019-01-01-preview"
      ],
        */


        //asdfg
        /*
           "Microsoft.DBforMySQL/operations": [
    {
      "aliases": null,
      "apiProfiles": null,
      "apiVersions": [
        "2021-12-01-preview", pass
        "2021-05-01-preview", pass
        "2021-05-01", pass
        "2017-12-01-preview", fail (newer stable lavailabe)
        "2017-12-01"  fail
      ],
        */


        /*asdfg

        "Microsoft.DataProtection/ResourceGuards": [
    {
      "aliases": null,
      "apiProfiles": null,
      "apiVersions": [
        "2022-05-01",
        "2022-04-01",
        "2022-03-01",
        "2022-02-01-preview",
        "2022-01-01",
        "2021-12-01-preview",
        "2021-10-01-preview",
        "2021-07-01",
        "2021-02-01-preview"
      ],


        2022-02-01-preview:

            [-] apiVersions Should Be Recent (14 ms)                                                                            
        Microsoft.DataProtection/ResourceGuards uses a preview version ( 2022-02-01-preview ) and there are more recent versions available. Line: 6, Column: 8
        Valid Api Versions:                                                                                             
        2022-05-01                                                                                                      
        2022-05-01                                                                                                      
        2022-04-01                                                                                                      
        2022-03-01                                                                                                      
        2022-02-01-preview                                                                                              
        2022-01-01                                                                                                      
        2021-12-01-preview                                                                                              
        2021-10-01-preview                                                                                              
        2021-07-01                                                                                                      
        2021-02-01-preview  
        */



        //asdfg
        /* "Microsoft.Sql/servers/databases/geoBackupPolicies": [
    {
      "aliases": null,
      "apiProfiles": null,
      "apiVersions": [
        "2015-05-01-preview",
        "2014-04-01-preview",
        "2014-04-01"
      ],*/



        [TestMethod]
        public void ArmTtk_PreviewWhenNonPreviewIsAvailable_WithSameDateAsStable_Fail()
        {
            string bicep = @"
                resource db 'fake.DBforMySQL/servers@2417-12-01-preview' = {
                  name: 'db]'
                #disable-next-line no-hardcoded-location
                  location: 'westeurope'
                  properties: {
                    administratorLogin: 'sa'
                    administratorLoginPassword: 'don\'t put passwords in plain text'
                    createMode: 'Default'
                    sslEnforcement: 'Disabled'
                  }
                }
            ";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                new string[]
                {
                   "Fake.DBforMySQL/servers@2417-12-01",
                   "Fake.DBforMySQL/servers@2417-12-01-preview",
                },
                fakeToday: "2422-07-04",
                "[1] Use recent apiVersions. There is a non-preview version of Fake.DBforMySQL/servers available. Acceptable API versions: 2417-12-01");
        }

        [TestMethod]
        public void ArmTtk_PreviewWhenNonPreviewIsAvailable_WithLaterDateThanStable_Fail()
        {
            string bicep = @"
                resource db 'fake.DBforMySQL/servers@2417-12-01-preview' = {
                  name: 'db]'
                #disable-next-line no-hardcoded-location
                  location: 'westeurope'
                  properties: {
                    administratorLogin: 'sa'
                    administratorLoginPassword: 'don\'t put passwords in plain text'
                    createMode: 'Default'
                    sslEnforcement: 'Disabled'
                  }
                }
            ";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                new string[]
                {
                   "Fake.DBforMySQL/servers@2417-12-02",
                   "Fake.DBforMySQL/servers@2417-12-01-preview",
                },
                fakeToday: "2422-07-04",
                "[1] Use recent apiVersions. There is a non-preview version of Fake.DBforMySQL/servers available. Acceptable API versions: 2417-12-01");
        }

        [TestMethod]
        public void ArmTtk_PreviewWhenNonPreviewIsAvailable_WithEarlierDateThanStable_Pass()
        {
            string bicep = @"
                resource db 'fake.DBforMySQL/servers@2417-12-01-preview' = {
                  name: 'db]'
                #disable-next-line no-hardcoded-location
                  location: 'westeurope'
                  properties: {
                    administratorLogin: 'sa'
                    administratorLoginPassword: 'don\'t put passwords in plain text'
                    createMode: 'Default'
                    sslEnforcement: 'Disabled'
                  }
                }
            ";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                new string[]
                {
                   "Fake.DBforMySQL/servers@2417-11-31",
                   "Fake.DBforMySQL/servers@2417-12-01-preview",
                },
                fakeToday: "2422-07-04");
        }

        //asdfg what if only beta/etc?
        [TestMethod]
        public void ArmTtk_OnlyPreviewAvailable_EvenIfOld_Pass()
        {
            string bicep = @"
               resource namespace 'Microsoft.DevTestLab/schedules@2417-08-01-preview' = {
                  name: 'namespace'
                  location: 'global'
                  properties: {
                  }
               }";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                new string[]
                {
                   "Fake.MachineLearningCompute/operationalizationClusters@2417-06-01-preview",
                   "Fake.MachineLearningCompute/operationalizationClusters@2417-08-01-preview",
                },
                fakeToday: "2422-07-04");
        }

        [TestMethod]
        public void NewerPreviewAvailable_Fail()
        {
            string bicep = @"
               resource namespace 'Fake.MachineLearningCompute/operationalizationClusters@2417-06-01-preview' = {
                  name: 'clusters'
                  location: 'global'
               }";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                new string[]
                {
                   "Fake.MachineLearningCompute/operationalizationClusters@2417-06-01-preview",
                   "Fake.MachineLearningCompute/operationalizationClusters@2417-08-01-preview",
                },
                fakeToday: "2422-07-04",
                "asdfg fail");
        }

        [TestMethod]
        public void ExtensionResources_RoleAssignment_Pass()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            CompileAndTestWithFakeDateAndTypes(@"
                targetScope = 'subscription'

                @description('The principal to assign the role to')
                param principalId string

                @allowed([
                  'Owner'
                  'Contributor'
                  'Reader'
                ])
                @description('Built-in role to assign')
                param builtInRoleType string

                var role = {
                  Owner: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
                  Contributor: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c'
                  Reader: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7'
                }

                resource roleAssignSub 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
                  name: guid(subscription().id, principalId, role[builtInRoleType])
                  properties: {
                    roleDefinitionId: role[builtInRoleType]
                    principalId: principalId
                  }
                }");
        }

        [TestMethod]
        public void ExtensionResources_Lock_Pass()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            CompileAndTestWithFakeDateAndTypes(@"
               resource createRgLock 'Microsoft.Authorization/locks@2016-09-01' = {
                  name: 'rgLock'
                  properties: {
                    level: 'CanNotDelete'
                    notes: 'Resource group should not be deleted.'
                  }
                }");
        }

        [TestMethod]
        public void ExtensionResources_SubscriptionRole_Pass()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            CompileAndTestWithFakeDateAndTypes(@"
                targetScope = 'subscription'

                @description('The principal to assign the role to')
                param principalId string

                @allowed([
                  'Owner'
                  'Contributor'
                  'Reader'
                ])
                @description('Built-in role to assign')
                param builtInRoleType string

                var role = {
                  Owner: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
                  Contributor: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c'
                  Reader: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7'
                }

                resource roleAssignSub 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
                  name: guid(subscription().id, principalId, role[builtInRoleType])
                  properties: {
                    roleDefinitionId: role[builtInRoleType]
                    principalId: principalId
                  }
                }");
        }

        [TestMethod]
        public void ExtensionResources_ScopeProperty_Pass()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            CompileAndTestWithFakeDateAndTypes(@"
                @description('The principal to assign the role to')
                param principalId string

                @allowed([
                  'Owner'
                  'Contributor'
                  'Reader'
                ])
                @description('Built-in role to assign')
                param builtInRoleType string

                param location string = resourceGroup().location

                var role = {
                  Owner: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
                  Contributor: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c'
                  Reader: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7'
                }
                var uniqueStorageName = 'storage${uniqueString(resourceGroup().id)}'

                resource demoStorageAcct 'Microsoft.Storage/storageAccounts@2019-04-01' = {
                  name: uniqueStorageName
                  location: location
                  sku: {
                    name: 'Standard_LRS'
                  }
                  kind: 'Storage'
                  properties: {}
                }

                resource roleAssignStorage 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
                  name: guid(demoStorageAcct.id, principalId, role[builtInRoleType])
                  properties: {
                    roleDefinitionId: role[builtInRoleType]
                    principalId: principalId
                  }
                  scope: demoStorageAcct
                }");
        }

        [TestMethod]
        public void ExtensionResources_ScopeProperty_ExistingResource_Pass()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            CompileAndTestWithFakeDateAndTypes(@"
                resource demoStorageAcct 'Microsoft.Storage/storageAccounts@2021-04-01' existing = {
                  name: 'examplestore'
                }

                resource createStorageLock 'Microsoft.Authorization/locks@2016-09-01' = {
                  name: 'storeLock'
                  scope: demoStorageAcct
                  properties: {
                    level: 'CanNotDelete'
                    notes: 'Storage account should not be deleted.'
                  }
                }");
        }

        [TestMethod]
        public void TenantDeployment_OldApiVersion_Fail()
        {
            CompileAndTestWithFakeDateAndTypes(@"
                targetScope = 'tenant'

                resource mgName_resource 'Microsoft.Management/managementGroups@2020-02-01' = {
                  name: 'mg1'
                }",
                "asdfg todo");
        }

        [TestMethod]
        public void SubscriptionDeployment_OldApiVersion_Fail()
        {
            CompileAndTestWithFakeDateAndTypes(@"
                targetScope='subscription'

                param resourceGroupName string
                param resourceGroupLocation string

                resource newRG 'Microsoft.Resources/resourceGroups@2021-01-01' = {
                  name: resourceGroupName
                  location: resourceGroupLocation
                }",
                "asdfg todo");
        }

        [TestMethod]
        public void ManagementGroupDeployment_OldApiVersion_Fail()
        {
            CompileAndTestWithFakeDateAndTypes(@"
                targetScope = 'managementGroup'

                param mgName string = 'mg-${uniqueString(newGuid())}'

                resource newMG 'Microsoft.Management/managementGroups@2020-05-01' = {
                  scope: tenant()
                  name: mgName
                  properties: {}
                }

                output newManagementGroup string = mgName",
                "asdfg todo");
        }

        [TestMethod]
        public void ArmTtk_LatestStableApiOlderThan2Years_Pass()
        {
            CompileAndTestWithFakeDateAndTypes(@"
                @description('The name for the Slack connection.')
                param slackConnectionName string = 'SlackConnection'

                @description('Location for all resources.')
                param location string

                // The only available API versions are:
                //    fake.Web/connections@2015-08-01-preview
                //    fake.Web/connections@2016-06-01
                // So this passes even though it's older than 2 years
                resource slackConnectionName_resource 'Fke.Web/connections@2016-06-01' = {
                  location: location
                  name: slackConnectionName
                  properties: {
                    api: {
                      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'slack')
                    }
                    displayName: 'slack'
                  }
                }");
        }

        [TestMethod]
        public void UnrecognizedVersionApiOlderThan2Years_Pass_ButGetCompilerWarning()
        {
            CompileAndTest(@"
                @description('The name for the Slack connection.')
                param slackConnectionName string = 'SlackConnection'

                @description('Location for all resources.')
                param location string

                resource slackConnectionName_resource 'Microsoft.Web/connections@2015-06-01' = { // Known type, unknown api version
                  location: location
                  name: slackConnectionName
                  properties: {
                    api: {
                      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'slack')
                    }
                    displayName: 'slack'
                  }
                }",
                OnCompileErrors.IncludeErrorsAndWarnings,
                "[7] Resource type \"Microsoft.Web/connections@2015-06-01");
        }

        [TestMethod]
        public void UnrecognizedResourceType_WithApiOlderThan2Years_Pass_ButGetCompilerWarning()
        {
            CompileAndTest(@"
                @description('The name for the Slack connection.')
                param slackConnectionName string = 'SlackConnection'

                @description('Location for all resources.')
                param location string

                resource slackConnectionName_resource 'Microsoft.Unknown/connections@2015-06-01' = { // unknown resource type
                  location: location
                  name: slackConnectionName
                  properties: {
                    api: {
                      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'slack')
                    }
                    displayName: 'slack'
                  }
                }",
                 OnCompileErrors.IncludeErrorsAndWarnings,
                "[7] Resource type \"Fake.Unknown/connections@2015-06-01\" does not have types available.");
        }

        [TestMethod]
        public void ArmTtk_ProviderResource()
        {

            CompileAndTestWithFakeDateAndTypes(@"
                @description('The name for the Slack connection.')
                param slackConnectionName string = 'SlackConnection'

                @description('Location for all resources.')
                param location string

                resource slackConnectionName_resource 'Microsoft.Unknown/connections@2015-06-01' = { // unknown resource type
                  location: location
                  name: slackConnectionName
                  properties: {
                    api: {
                      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'slack')
                    }
                    displayName: 'slack'
                  }
                }",
                new string[] {
                "asdf??"
                },
                "2022-07-07",
                "asdf??");
        }

        [TestMethod]
        public void LotsOfNonStableVersions()
        {
            //asdfg?
            /*
             {
            "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
            "contentVersion": "1.0.0.0",
            "parameters": {},
            "functions": [],
            "resources": [
            {
            // pass
             "name": "res1",
            "type": "Microsoft.VSOnline/registeredSubscriptions",
            "apiVersion": "2020-05-26-privatepreview"
            },
            {
            // apiVersions Should Be Recent
            // Api versions must be the latest or under 2 years old (730 days) - API version 2020-05-26-preview of Microsoft.VSOnline/registeredSubscriptions is 772 days old Line: 14, Column: 14
            // Valid Api Versions:                                                                                             
            //    2020-05-26-beta                                                                                                 
            //    2020-05-26-privatepreview   
             "name": "res2",
            "type": "Microsoft.VSOnline/registeredSubscriptions",
            "apiVersion": "2020-05-26-preview"
            },
            {
            // pass
             "name": "res3",
            "type": "Microsoft.VSOnline/registeredSubscriptions",
            "apiVersion": "2020-05-26-beta"
            },
            {
            // pass
             "name": "res4",
            "type": "Microsoft.VSOnline/registeredSubscriptions",
            "apiVersion": "2020-05-26-alpha"
            },
            {
            // pass
             "name": "res5",
            "type": "Microsoft.VSOnline/registeredSubscriptions",
            "apiVersion": "2019-07-01-privatepreview"
            },
            {
            // pass
             "name": "res6",
            "type": "Microsoft.VSOnline/registeredSubscriptions",
            "apiVersion": "2019-07-01-preview"
            },
            {
            // pass
             "name": "res7",
            "type": "Microsoft.VSOnline/registeredSubscriptions",
            "apiVersion": "2019-07-01-beta"
            },
            {
            // pass
             "name": "res8",
            "type": "Microsoft.VSOnline/registeredSubscriptions",
            "apiVersion": "2019-07-01-alpha"
            }
            ],
            "outputs": {}
            }
            */

        CompileAndTestWithFakeDateAndTypes(@"
    // Pass - old but no more recent stable version
    resource res1 'Microsoft.VSOnline/registeredSubscriptions@2020-05-26-privatepreview' = {
      name: 'res1'
    }

    // asdfg?
    // Pass - old but no more recent stable version
    resource res2 'Microsoft.VSOnline/registeredSubscriptions@2020-05-26-preview' = {
      name: 'res2'
    }

    // asdfg?
    // Pass - old but no more recent stable version
    resource res3 'Microsoft.VSOnline/registeredSubscriptions@2020-05-26-beta' = {
      name: 'res3'
    }

    // asdfg?
    // Pass - old but no more recent stable version 
    resource res4 'Microsoft.VSOnline/registeredSubscriptions@2020-05-26-alpha' = {
      name: 'res4'
    }

    // Fail
    resource res5 'Microsoft.VSOnline/registeredSubscriptions@2019-07-01-privatepreview' = {
      name: 'res5'
    }

    // Fail
    resource res6 'Microsoft.VSOnline/registeredSubscriptions@2019-07-01-preview' = {
      name: 'res6'
    }

    // Fail
    resource res7 'Microsoft.VSOnline/registeredSubscriptions@2019-07-01-beta' = {
      name: 'res7'
    }

    // Fail
    resource res8 'Microsoft.VSOnline/registeredSubscriptions@2019-07-01-alpha' = {
      name: 'res8'
    }",
                new string[] {
        "Microsoft.VSOnline/registeredSubscriptions@2020-05-26-privatepreview",
        "Microsoft.VSOnline/registeredSubscriptions@2020-05-26-preview",
        "Microsoft.VSOnline/registeredSubscriptions@2020-05-26-beta",
        "Microsoft.VSOnline/registeredSubscriptions@2020-05-26-alpha",
        "Microsoft.VSOnline/registeredSubscriptions@2019-07-01-privatepreview",
        "Microsoft.VSOnline/registeredSubscriptions@2019-07-01-preview",
        "Microsoft.VSOnline/registeredSubscriptions@2019-07-01-beta",
        "Microsoft.VSOnline/registeredSubscriptions@2019-07-01-alpha"
                },
                "2022-07-07",
                "asdf??");
        }
    }
}
