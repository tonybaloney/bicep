// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Bicep.Core.TypeSystem;

namespace Bicep.Core.ApiVersion
{
    public interface IApiVersionProvider
    {
        public IEnumerable<string> GetResourceTypeNames(ResourceScope scope);
        public IEnumerable<string> GetApiVersions(ResourceScope scope, string fullyQualifiedResourceName);
    }
}
