// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bicep.Cli;
using Bicep.Core.Samples;
using Bicep.Core.TypeSystem.Az;
using Bicep.Core.UnitTests;
using Bicep.Core.UnitTests.Baselines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bicep.Core.IntegrationTests.Benchmarks;

[TestClass]
public class BenchmarkTests
{
    [NotNull]
    public TestContext? TestContext { get; set; }

    private static IEnumerable<object[]> GetData()
        => DataSets.AllDataSets.ToDynamicTestData();

    [DataTestMethod]
    [DoNotParallelize]
    [DynamicData(nameof(GetData), DynamicDataSourceType.Method, DynamicDataDisplayNameDeclaringType = typeof(DataSet), DynamicDataDisplayName = nameof(DataSet.GetDisplayName))]
    public async Task Benchmarks_should_match_expected(DataSet dataSet)
    {
        var bicepFile = new EmbeddedFile(typeof(DataSet).Assembly, $"Files/{dataSet.Name}/main.bicep");
        var baselineFolder = BaselineFolder.BuildOutputFolder(TestContext, bicepFile);

        var program = new Program(new InvocationContext(
            new AzResourceTypeLoader(),
            new StringWriter(),
            new StringWriter(),
            features: BicepTestConstants.Features,
            clientFactory: BicepTestConstants.ClientFactory,
            templateSpecRepositoryFactory: BicepTestConstants.TemplateSpecRepositoryFactory));

        using var listener = new SimpleEventListener();
        await program.RunAsync(new [] { "build", baselineFolder.EntryFile.OutputFilePath });
        await Task.Delay(TimeSpan.FromSeconds(2));
        var bicepCounters = listener.GetBicepCounters();

        var perfFile = baselineFolder.GetFileOrEnsureCheckedIn("perf.out");
        await File.WriteAllTextAsync(perfFile.OutputFilePath, FormatBaselineFile(bicepCounters));
        perfFile.ShouldHaveExpectedValue();
    }

    private string FormatBaselineFile(ImmutableDictionary<string, double> bicepCounters)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in bicepCounters.OrderBy(x => x.Key))
        {
            sb.AppendLine($"{key}: {value}");
        }

        return sb.ToString();
    }

    public class SimpleEventListener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name != "Bicep.Core.EventSource" && source.Name != "Microsoft-Windows-DotNETRuntime")
            {
                return;
            }

            EnableEvents(source, EventLevel.Verbose, EventKeywords.All, new Dictionary<string, string?>()
            {
                ["EventCounterIntervalSec"] = "1"
            });
        }

        private ConcurrentDictionary<string, double> bicepCounters = new();

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name == "Bicep.Core.EventSource" &&
                eventData.EventName == "EventCounters" &&
                eventData.Payload is not null)
            {
                foreach (var payload in eventData.Payload)
                {
                    if (payload is IDictionary<string, object> eventPayload)
                    {
                        if (eventPayload.TryGetValue("Name", out var nameVal) && nameVal is string name &&
                            eventPayload.TryGetValue("Increment", out var incrementVal) && incrementVal is double increment)
                        {
                            bicepCounters.AddOrUpdate(
                                name,
                                _ => increment,
                                (_, oldVal) => oldVal + increment);
                        }
                    }
                }
            }
/*
            if (eventData.EventSource.Name == "Microsoft-Windows-DotNETRuntime" &&
                eventData.EventName is not null)
            {
                dotnetCounters.AddOrUpdate(
                    eventData.EventName,
                    k => 1,
                    (k, v) => v + 1);
            }
*/
        }

        public ImmutableDictionary<string, double> GetBicepCounters()
            => bicepCounters.ToImmutableDictionary();
    }
}
