// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.Analyzers.Linter;
using Bicep.Core.Diagnostics;
using Bicep.Core.Text;
using Bicep.Core.UnitTests.Assertions;
using Bicep.Core.UnitTests.Utils;
using FluentAssertions;
using FluentAssertions.Execution;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bicep.Core.UnitTests.Diagnostics.LinterRuleTests
{
    public class LinterRuleTestsBase
    {
        public enum OnCompileErrors
        {
            Ignore, // Ignore any compile errors
            Include, // Include compile errors in the list of messages to expect
        }

        public enum IncludePosition
        {
            None,
            LineNumber,
        }

        private string FormatDiagnostic(IDiagnostic diagnostic, ImmutableArray<int> lineStarts, IncludePosition includePosition)
        {
            if (includePosition == IncludePosition.LineNumber)
            {
                var position = TextCoordinateConverter.GetPosition(lineStarts, diagnostic.Span.Position);
                return $"[{position.line}] {diagnostic.Message}";
            }
            else
            {
                return diagnostic.Message;
            }
        }

        protected void AssertLinterRuleDiagnostics(string ruleCode, string bicepText, string[] expectedMessagesForCode, OnCompileErrors onCompileErrors = OnCompileErrors.Include, IncludePosition includePosition = IncludePosition.None)
        {
            var lineStarts = TextCoordinateConverter.GetLineStarts(bicepText);

            AssertLinterRuleDiagnostics(ruleCode, bicepText, onCompileErrors, diags =>
            {
                var messages = diags.Select(d => FormatDiagnostic(d, lineStarts, includePosition));
                messages.Should().BeEquivalentTo(expectedMessagesForCode);
            });
        }

        protected void AssertLinterRuleDiagnostics(string ruleCode, string bicepText, int expectedDiagnosticCountForCode, OnCompileErrors onCompileErrors = OnCompileErrors.Include, IncludePosition includePosition = IncludePosition.None)
        {
            AssertLinterRuleDiagnostics(ruleCode, bicepText, onCompileErrors, diags =>
            {
                diags.Should().HaveCount(expectedDiagnosticCountForCode);
            });
        }

        protected void AssertLinterRuleDiagnostics(string ruleCode, string bicepText, Action<IEnumerable<IDiagnostic>> assertAction)
        {
            AssertLinterRuleDiagnostics(ruleCode, bicepText, OnCompileErrors.Include, assertAction);
        }

        protected void AssertLinterRuleDiagnostics(string ruleCode, string bicepText, OnCompileErrors onCompileErrors, Action<IEnumerable<IDiagnostic>> assertAction)
        {
            RunWithDiagnosticAnnotations(
                  bicepText,
                  diag => diag.Code == ruleCode || (onCompileErrors == OnCompileErrors.Include && diag.Level == DiagnosticLevel.Error),
                  onCompileErrors,
                  assertAction);
        }

        private static void RunWithDiagnosticAnnotations(string bicepText, Func<IDiagnostic, bool> filterFunc, OnCompileErrors onCompileErrors, Action<IEnumerable<IDiagnostic>> assertAction)
        {
            var result = CompilationHelper.Compile(bicepText);
            using (new AssertionScope().WithFullSource(result.BicepFile))
            {
                result.Should().NotHaveDiagnosticsWithCodes(new[] { LinterAnalyzer.LinterRuleInternalError }, "There should never be linter LinterRuleInternalError errors");

                IDiagnostic[] diagnosticsMatchingCode = result.Diagnostics.Where(filterFunc).ToArray();
                DiagnosticAssertions.DoWithDiagnosticAnnotations(
                    result.Compilation.SourceFileGrouping.EntryPoint,
                    result.Diagnostics.Where(filterFunc),
                    assertAction);
            }
        }


    }
}
