// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace OSS.NoticeGenerator.Models
{
    public record NoticeRequest(ImmutableArray<string> Coordinates, NoticeRequestOptions Options);

    public record NoticeRequestOptions();

    public record NoticeResponse(string Content, NoticeResponseSummary Summary);

    public record NoticeResponseSummary(int Total, NoticeResponseWarnings Warnings);

    public record NoticeResponseWarnings(ImmutableArray<string> NoDefinition, ImmutableArray<string> NoLicense, ImmutableArray<string> NoCopyright);
}
