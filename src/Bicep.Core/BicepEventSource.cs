// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Diagnostics.Tracing;

namespace Bicep.Core;

public sealed class BicepEventSource : EventSource
{
    public static readonly BicepEventSource Log = new();

    private readonly IncrementingEventCounter syntaxCreated;
    private readonly IncrementingEventCounter syntaxVisited;
    private readonly IncrementingEventCounter symbolCreated;

    private BicepEventSource()
        : base("Bicep.Core.EventSource")
    {
        syntaxCreated = new IncrementingEventCounter("syntax-created", this)
        {
            DisplayName = "Syntax Nodes Created",
            DisplayRateTimeScale = TimeSpan.FromSeconds(1),
        };
        syntaxVisited = new IncrementingEventCounter("syntax-visited", this)
        {
            DisplayName = "Syntax Nodes Visited",
            DisplayRateTimeScale = TimeSpan.FromSeconds(1),
        };
        symbolCreated = new IncrementingEventCounter("symbol-created", this)
        {
            DisplayName = "Symbols Created",
            DisplayRateTimeScale = TimeSpan.FromSeconds(1),
        };
    }

    public static void SyntaxCreated()
        => Log.syntaxCreated.Increment(1);

    public static void SyntaxVisited()
        => Log.syntaxVisited.Increment(1);

    public static void SymbolCreated()
        => Log.symbolCreated.Increment(1);

    protected override void Dispose(bool disposing)
    {
        syntaxCreated.Dispose();
        syntaxVisited.Dispose();
        symbolCreated.Dispose();

        base.Dispose(disposing);
    }
}
