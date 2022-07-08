// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Bicep.Core.ApiVersions
{
    public interface IApiVersionProvider
    {
        // List is sorted at top level by ascending date, and each suffix array is sorted ascending alphabetically
        //asdfg  public (DateTime date, string[] suffixes)[] GetSortedValidApiVersions(string fullyQualifiedResourceName);

        public string[] GetSortedValidApiVersions(string fullyQualifiedResourceName);
    }
}
