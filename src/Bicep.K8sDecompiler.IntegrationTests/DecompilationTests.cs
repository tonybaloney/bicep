// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;
using Bicep.Core.UnitTests.Assertions;
using Bicep.Core.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Bicep.Core.UnitTests;
using Bicep.Core.Diagnostics;

namespace Bicep.K8sDecompiler.IntegrationTests
{
    [TestClass]
    public class DecompilationTests
    {
        [NotNull]
        public TestContext? TestContext { get; set; }

        private CompilationHelper.CompilationHelperContext EnabledImportsContext
            => new CompilationHelper.CompilationHelperContext(Features: BicepTestConstants.CreateFeaturesProvider(TestContext, importsEnabled: true));

        public record ExampleData(string BicepStreamName, string YmlStreamName, string OutputFolderName)
        {
            public static string GetDisplayName(MethodInfo info, object[] data) => ((ExampleData)data[0]).YmlStreamName!;
        }

        private static IEnumerable<object[]> GetWorkingExampleData()
        {
            const string pathPrefix = "Working/";

            // Only return files whose path segment length is 3 as entry files to avoid decompiling nested templates twice.
            var entryStreamNames = typeof(DecompilationTests).Assembly.GetManifestResourceNames()
                .Where(p => p.StartsWith(pathPrefix, StringComparison.Ordinal) && p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length == 3);

            foreach (var streamName in entryStreamNames)
            {
                var extension = Path.GetExtension(streamName);
                if (!StringComparer.OrdinalIgnoreCase.Equals(extension, ".yml"))
                {
                    continue;
                }

                var outputFolderName = streamName[pathPrefix.Length..^extension.Length].Replace('/', '_');
                var exampleData = new ExampleData(Path.ChangeExtension(streamName, ".bicep"), streamName, outputFolderName);

                yield return new object[] { exampleData };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(GetWorkingExampleData), DynamicDataSourceType.Method, DynamicDataDisplayNameDeclaringType = typeof(ExampleData), DynamicDataDisplayName = nameof(ExampleData.GetDisplayName))]
        [TestCategory(BaselineHelper.BaselineTestCategory)]
        public async Task Kubernetes_manifest_files_can_be_decompiled(ExampleData example)
        {
            var parentStream = Path.GetDirectoryName(example.BicepStreamName)!.Replace('\\', '/');
            var outputDirectory = FileHelper.SaveEmbeddedResourcesWithPathPrefix(TestContext, typeof(DecompilationTests).Assembly, parentStream);
            var ymlFileName = Path.Combine(outputDirectory, Path.GetFileName(example.YmlStreamName));
            var bicepFileName = Path.Combine(outputDirectory, Path.GetFileName(example.BicepStreamName));

            await Program.Decompile(ymlFileName, bicepFileName + ".actual");
            var generatedBicepContents = await File.ReadAllTextAsync(bicepFileName + ".actual");

            generatedBicepContents.Should().EqualWithLineByLineDiffOutput(
                TestContext,
                File.Exists(bicepFileName) ? (await File.ReadAllTextAsync(bicepFileName)) : string.Empty,
                expectedLocation: Path.Combine("src", "Bicep.K8sDecompiler.IntegrationTests", example.BicepStreamName),
                actualLocation: bicepFileName + ".actual");

            CompilationHelper.Compile(EnabledImportsContext, generatedBicepContents)
                .ExcludingLinterDiagnostics()
                .WithFilteredDiagnostics(x => x.Level == DiagnosticLevel.Error).Should().NotHaveAnyDiagnostics();
        }
    }
}
