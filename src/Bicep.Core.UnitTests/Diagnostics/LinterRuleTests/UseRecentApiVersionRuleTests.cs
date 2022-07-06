// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Bicep.Core.Analyzers.Linter;
using Bicep.Core.Analyzers.Linter.Rules;
using Bicep.Core.ApiVersion;
using Bicep.Core.CodeAction;
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
namespace Bicep.Core.UnitTests.Diagnostics.LinterRuleTests
{
    [TestClass]
    public class UseRecentApiVersionRuleTests : LinterRuleTestsBase
    {
        // Welcome to the 25th century!
        // All tests using CompileAndTest set this value as "today" as far as the rule is concerned
        private string TESTING_TODAY_DATE = "2422-07-04";

        public UseRecentApiVersionRuleTests()
        {
            var analyzers = JsonElementFactory.CreateElement(@"
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
                }".Replace("<TESTING_TODAY_DATE>", TESTING_TODAY_DATE));

            //asdfg needed?
            ConfigurationWithTestingTodayDate = new RootConfiguration(
                BicepTestConstants.BuiltInConfiguration.Cloud,
                BicepTestConstants.BuiltInConfiguration.ModuleAliases,
                new AnalyzersConfiguration(analyzers),
                null);
        }

        private void CompileAndTest(string bicep, params string[] expectedMessagesForCode)
        {
            CompileAndTest(bicep, OnCompileErrors.Include, IncludePosition.LineNumber, expectedMessagesForCode);
        }

        private void CompileAndTest(string bicep, OnCompileErrors onCompileErrors = OnCompileErrors.Include, IncludePosition includePosition = IncludePosition.None, params string[] expectedMessagesForCode)
        {
            //string config = @"
            //  {
            //    ""analyzers"": {
            //      ""core"": {
            //        ""enabled"": true,
            //        ""rules"": {
            //          ""use-recent-api-versions"": {
            //              ""level"": ""warning"",
            //              ""debugToday"": ""<TESTING_TODAY_DATE>""
            //          }
            //        }
            //      }
            //    }
            //  }".Replace("<TESTING_TODAY_DATE>", TESTING_TODAY_DATE);

            AssertLinterRuleDiagnostics(UseRecentApiVersionRule.Code, bicep, expectedMessagesForCode, onCompileErrors, includePosition, configuration: ConfigurationWithTestingTodayDate);
        }

        private IApiVersionProvider FakeApiVersionProviderResourceScope = new ApiVersionProvider(FakeResourceTypes.GetFakeTypes(FakeResourceTypes.ResourceScope));

        private RootConfiguration ConfigurationWithTestingTodayDate;

        private SemanticModel SemanticModelFakeResourceScope => new Compilation(
            BicepTestConstants.Features,
            TestTypeHelper.CreateEmptyProvider(),
            SourceFileGroupingFactory.CreateFromText(string.Empty, BicepTestConstants.FileResolver),
            ConfigurationWithTestingTodayDate,
            FakeApiVersionProviderResourceScope,
            new LinterAnalyzer(ConfigurationWithTestingTodayDate)).GetEntrypointSemanticModel();

        private string ConvertDateTimeToString(DateTime dateTime)
        {
            return dateTime.Year + "-" + dateTime.Month + "-" + dateTime.Day;
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
            CompileAndTest(text, expectedUseRecentApiVersions);
        }

        [TestMethod]
        public void AddCodeFixIfGAVersionIsNotLatest_WithCurrentVersionLessThanTwoYearsOld_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-1);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddMonths(-5);
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfGAVersionIsNotLatest(new TextSpan(17, 47),
                                                     recentGAVersion,
                                                     currentVersion);

            spanFixes.Should().BeEmpty();
        }

        [TestMethod]
        public void AddCodeFixIfGAVersionIsNotLatest_WithCurrentVersionMoreThanTwoYearsOldAndRecentApiVersionIsAvailable_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddMonths(-5);
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfGAVersionIsNotLatest(new TextSpan(17, 47),
                                                     recentGAVersion,
                                                     currentVersion);

            spanFixes.Should().SatisfyRespectively(
                x =>
                {
                    x.Value.Description.Should().Be("Use recent API version " + recentGAVersion);
                });
        }

        [TestMethod]
        public void AddCodeFixIfGAVersionIsNotLatest_WithCurrentAndRecentApiVersionsMoreThanTwoYearsOld_ShouldAddDiagnosticsToUseRecentApiVersion()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-4);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-3);
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfGAVersionIsNotLatest(new TextSpan(17, 47),
                                                     recentGAVersion,
                                                     currentVersion);

            spanFixes.Should().SatisfyRespectively(
                x =>
                {
                    x.Value.Description.Should().Be("Use recent API version " + recentGAVersion);
                });
        }

        [TestMethod]
        public void AddCodeFixIfGAVersionIsNotLatest_WhenCurrentAndRecentApiVersionsAreSameAndMoreThanTwoYearsOld_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = currentVersionDate;
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfGAVersionIsNotLatest(new TextSpan(17, 47),
                                                     recentGAVersion,
                                                     currentVersion);

            spanFixes.Should().BeEmpty();
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenCurrentPreviewVersionIsLatest_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-1);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-3);
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            DateTime recentPreviewVersionDate = currentVersionDate;
            string recentPreviewVersion = ConvertDateTimeToString(recentPreviewVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionPrefixConstants.Preview,
                                                        currentVersion);

            spanFixes.Should().BeEmpty();
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenRecentPreviewVersionIsAvailable_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-5);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-3);
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentPreviewVersion = ConvertDateTimeToString(recentPreviewVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionPrefixConstants.Preview,
                                                        currentVersion);

            spanFixes.Should().SatisfyRespectively(
                x =>
                {
                    x.Value.Description.Should().Be("Use recent API version " + recentPreviewVersion + ApiVersionPrefixConstants.Preview);
                });
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenRecentGAVersionIsAvailable_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-5);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-2);
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-3);
            string recentPreviewVersion = ConvertDateTimeToString(recentPreviewVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionPrefixConstants.Preview,
                                                        currentVersion);

            spanFixes.Should().SatisfyRespectively(
                x =>
                {
                    x.Value.Description.Should().Be("Use recent API version " + recentGAVersion);
                });
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenRecentGAVersionIsSameAsPreviewVersion_ShouldAddDiagnosticsUsingGAVersion()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-2);
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            string recentPreviewVersion = recentGAVersion;

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionPrefixConstants.Preview,
                                                        currentVersion);

            spanFixes.Should().SatisfyRespectively(
                x =>
                {
                    x.Value.Description.Should().Be("Use recent API version " + recentGAVersion);
                });
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenGAVersionisNull_AndCurrentVersionIsNotRecent_ShouldAddDiagnosticsUsingRecentPreviewVersion()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentPreviewVersion = ConvertDateTimeToString(recentPreviewVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        null,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionPrefixConstants.Preview,
                                                        currentVersion);

            spanFixes.Should().SatisfyRespectively(
                x =>
                {
                    x.Value.Description.Should().Be("Use recent API version " + recentPreviewVersion + ApiVersionPrefixConstants.Preview);
                });
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithPreviewVersion_WhenGAVersionisNull_AndCurrentVersionIsRecent_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-2);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentPreviewVersionDate = currentVersionDate;
            string recentPreviewVersion = ConvertDateTimeToString(recentPreviewVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        null,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionPrefixConstants.Preview,
                                                        currentVersion);

            spanFixes.Should().BeEmpty();
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithNonPreviewVersion_WhenGAVersionisNull_AndPreviewVersionIsRecent_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-1);
            string recentPreviewVersion = ConvertDateTimeToString(recentPreviewVersionDate);

            DateTime recentNonPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentNonPreviewApiVersion = ConvertDateTimeToString(recentNonPreviewVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        null,
                                                        recentPreviewVersion,
                                                        null,
                                                        ApiVersionPrefixConstants.RC,
                                                        currentVersion);

            spanFixes.Should().SatisfyRespectively(
                x =>
                {
                    x.Value.Description.Should().Be("Use recent API version " + recentPreviewVersion + ApiVersionPrefixConstants.Preview);
                });
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithNonPreviewVersion_WhenGAVersionisRecent_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-1);
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentPreviewVersion = ConvertDateTimeToString(recentPreviewVersionDate);

            DateTime recentNonPreviewVersionDate = DateTime.Today.AddYears(-3);
            string recentNonPreviewVersion = ConvertDateTimeToString(recentNonPreviewVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        recentNonPreviewVersion,
                                                        ApiVersionPrefixConstants.Alpha,
                                                        currentVersion);

            spanFixes.Should().SatisfyRespectively(
                x =>
                {
                    x.Value.Description.Should().Be("Use recent API version " + recentGAVersion);
                });
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithNonPreviewVersion_WhenPreviewAndGAVersionsAreNull_AndNonPreviewVersionIsNotRecent_ShouldAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddYears(-3);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentNonPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentNonPreviewVersion = ConvertDateTimeToString(recentNonPreviewVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        null,
                                                        null,
                                                        recentNonPreviewVersion,
                                                        ApiVersionPrefixConstants.Alpha,
                                                        currentVersion);

            spanFixes.Should().SatisfyRespectively(
                x =>
                {
                    x.Value.Description.Should().Be("Use recent API version " + recentNonPreviewVersion + ApiVersionPrefixConstants.Alpha);
                });
        }

        [TestMethod]
        public void AddCodeFixIfNonGAVersionIsNotLatest_WithRecentNonPreviewVersion_ShouldNotAddDiagnostics()
        {
            DateTime currentVersionDate = DateTime.Today.AddMonths(-3);
            string currentVersion = ConvertDateTimeToString(currentVersionDate);

            DateTime recentGAVersionDate = DateTime.Today.AddYears(-1);
            string recentGAVersion = ConvertDateTimeToString(recentGAVersionDate);

            DateTime recentPreviewVersionDate = DateTime.Today.AddYears(-2);
            string recentPreviewVersion = ConvertDateTimeToString(recentPreviewVersionDate);

            DateTime recentNonPreviewVersionDate = DateTime.Today.AddYears(-3);
            string recentNonPreviewVersion = ConvertDateTimeToString(recentNonPreviewVersionDate);

            Dictionary<TextSpan, CodeFix> spanFixes = new();

            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            visitor.AddCodeFixIfNonGAVersionIsNotLatest(new TextSpan(17, 47),
                                                        recentGAVersion,
                                                        recentPreviewVersion,
                                                        recentNonPreviewVersion,
                                                        ApiVersionPrefixConstants.Alpha,
                                                        currentVersion);

            spanFixes.Should().BeEmpty();
        }

        [DataRow("invalid-text")]
        [DataRow("")]
        [DataRow("   ")]
        [TestMethod]
        public void GetApiVersionDate_WithInvalidVersion(string apiVersion)
        {
            Dictionary<TextSpan, CodeFix> spanFixes = new();
            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            DateTime? actual = visitor.GetApiVersionDate(apiVersion);

            actual.Should().BeNull();
        }

        [DataRow("2015-04-01-rc", "2015-04-01")]
        [DataRow("2016-04-01", "2016-04-01")]
        [DataRow("2016-04-01-privatepreview", "2016-04-01")]
        [TestMethod]
        public void GetApiVersionDate_WithValidVersion(string apiVersion, string expectedVersion)
        {
            Dictionary<TextSpan, CodeFix> spanFixes = new();
            Visitor visitor = new Visitor(spanFixes, SemanticModelFakeResourceScope, DateTime.Today);

            DateTime? actual = visitor.GetApiVersionDate(apiVersion);

            actual.Should().Be(DateTime.Parse(expectedVersion));
        }

        [TestMethod]
        public void ArmTtkTest_ApiVersionIsNotAnExpression()
        {
            string bicep = @"
                resource publicIPAddress1 'Microsoft.Network/publicIPAddresses@[concat(\'2020\', \'01-01\')]' = {
                  name: 'publicIPAddress1'
                  location: resourceGroup().location
                  tags: {
                    displayName: 'publicIPAddress1'
                  }
                  properties: {
                    publicIPAllocationMethod: 'Dynamic'
                  }
                }";
            CompileAndTest(bicep, "The resource type is not valid. Specify a valid resource type of format \"<types>@<apiVersion>\".");
        }

        [TestMethod]
        public void NestedResources1()
        {
            string bicep = @"
                param location string

                resource namespace1 'Microsoft.ServiceBus/namespaces@2018-01-01-preview' = {
                  name: 'namespace1'
                  location: location
                  properties: {
                  }
                }

                // Using 'parent'
                resource namespace1_queue1 'Microsoft.ServiceBus/namespaces/queues@2017-04-01' = {
                  parent: namespace1
                  name: 'queue1'
                }

                // Using 'parent'
                resource namespace1_queue1_rule1 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2015-08-01' = {
                  parent: namespace1_queue1
                  name: 'rule1'
                }

                // Using nested name
                resource namespace1_queue2 'Microsoft.ServiceBus/namespaces/queues@2017-04-01' = {
                  name: 'namespace1/queue1'
                }

                // Using 'parent'
                resource namespace1_queue2_rule2 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2018-01-01-preview' = {
                  parent: namespace1_queue2
                  name: 'rule2'
                }

                // Using nested name
                resource namespace1_queue2_rule3 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2017-04-01' = {
                  name: 'namespace1/queue2/rule3'
                }";

            CompileAndTest(bicep, OnCompileErrors.Include, IncludePosition.LineNumber,
                "[3] Use recent API versions",
                "[11] Use recent API versions",
                "[17] Use recent API versions",
                "[23] Use recent API versions",
                "[28] Use recent API versions",
                "[34] Use recent API versions"
                );
        }

        [TestMethod]
        public void NestedResources2()
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

            CompileAndTest(bicep, OnCompileErrors.Include, IncludePosition.LineNumber,
                "[4] Use recent API versions",
                "[8] Use recent API versions",
                "[12] Use recent API versions",
                "[19] Use recent API versions",
                "[24] Use recent API versions"
                );
        }

        [TestMethod]
        public void ArmTtk_NotAString()
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

            CompileAndTest(bicep, OnCompileErrors.Include, IncludePosition.LineNumber,
               "[1] The resource type is not valid. Specify a valid resource type of format \"<types>@<apiVersion>\".");
        }
    }
}
