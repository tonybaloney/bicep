// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Bicep.Core.ApiVersion;
using Bicep.Core.Features;
using Bicep.Core.Resources;
using Bicep.Core.Semantics;
using Bicep.Core.Semantics.Namespaces;
using Bicep.Core.TypeSystem.Az;

namespace Bicep.Core.ApiVersions
{
    public class ApiVersionProvider : IApiVersionProvider
    {
        private static StringComparer Comparer = LanguageConstants.ResourceTypeComparer;

        // E.g. 2022-07-07
        private Dictionary<string, List<string>> stableVersions = new(Comparer);
        // E.g. 2022-07-07-alpha, 2022-07-07-preview, 2022-07-07-privatepreview etc.
        private Dictionary<string, List<string>> previewVersions = new(Comparer);


        public ApiVersionProvider()
        {
            DefaultNamespaceProvider defaultNamespaceProvider = new DefaultNamespaceProvider(new AzResourceTypeLoader(), new FeatureProvider());
            NamespaceResolver namespaceResolver = NamespaceResolver.Create(defaultNamespaceProvider, TypeSystem.ResourceScope.ResourceGroup, Enumerable.Empty<ImportedNamespaceSymbol>());
            IEnumerable<ResourceTypeReference> resourceTypeReferences = namespaceResolver.GetAvailableResourceTypes();

            CacheApiVersions(resourceTypeReferences);
        }

        // For testing
        public ApiVersionProvider(IEnumerable<ResourceTypeReference> resourceTypeReferences)
        {
            CacheApiVersions(resourceTypeReferences);
        }

        private void CacheApiVersions(IEnumerable<ResourceTypeReference> resourceTypeReferences)
        {
            foreach (var resourceTypeReference in resourceTypeReferences)
            {
                (string? apiVersion, string? suffix) = resourceTypeReference.ApiVersion != null ? ApiVersionHelper.TryParse(resourceTypeReference.ApiVersion) : (null, null);
                if (apiVersion is not null)
                {
                    string fullyQualifiedType = resourceTypeReference.FormatType();
                    if (suffix == ApiVersionSuffixes.GA) //asdfg case insensitive?
                    {

                        UpdateCache(stableVersions, apiVersion, fullyQualifiedType);
                    }
                    else
                    {
                        UpdateCache(previewVersions, apiVersion + suffix /* will have been lower-cased */, fullyQualifiedType);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Invalid API version found: {resourceTypeReference.FormatType()}");
                }
            }

            // Sort the lists of api versions
            stableVersions = stableVersions.ToDictionary(x => x.Key, x => x.Value.OrderBy (y => y).ToList(), Comparer);
            previewVersions = previewVersions.ToDictionary(x => x.Key, x => x.Value.OrderBy(y => y).ToList(), Comparer);
        }

        private void UpdateCache(Dictionary<string, List<string>> cache, string? apiVersion, string fullyQualifiedType)
        {
            if (apiVersion is not null)
            {
                if (cache.TryGetValue(fullyQualifiedType, out List<string>? value))
                {
                    value.Add(apiVersion);
                    cache[fullyQualifiedType] = value;
                }
                else
                {
                    cache.Add(fullyQualifiedType, new List<string> { apiVersion });
                }
            }
        }

        //asdfg
        public string[] GetSortedValidApiVersions(string fullyQualifiedResourceType)
        {
            var allVersions = new List<string>();

            if (stableVersions.TryGetValue(fullyQualifiedResourceType, out List<string>? stable))
            {
                allVersions.AddRange(stable);
            }
            if (previewVersions.TryGetValue(fullyQualifiedResourceType, out List<string>? previews))
            {
                allVersions.AddRange(previews);
            }

            return allVersions.ToArray();
        }

        //asdfg
        //public string? GetRecentApiVersion(string fullyQualifiedName, string? prefix)
        //{
        //    switch (prefix)
        //    {
        //        case ApiVersionSuffixes.GA:
        //            return GetRecentApiVersion(fullyQualifiedName, gaVersions);
        //        case ApiVersionSuffixes.Alpha:
        //            return GetRecentApiVersion(fullyQualifiedName, alphaVersions);
        //        case ApiVersionSuffixes.Beta:
        //            return GetRecentApiVersion(fullyQualifiedName, betaVersions);
        //        case ApiVersionSuffixes.Preview:
        //            return GetRecentApiVersion(fullyQualifiedName, previewVersions);
        //        case ApiVersionSuffixes.PrivatePreview:
        //            return GetRecentApiVersion(fullyQualifiedName, privatePreviewVersions);
        //        case ApiVersionSuffixes.RC:
        //            return GetRecentApiVersion(fullyQualifiedName, rcVersions);
        //    }

        //    return null;
        //}

        //private string? GetRecentApiVersion(string fullyQualifiedName, Dictionary<string, List<string>> cache)
        //    {
        //        if (cache.TryGetValue(fullyQualifiedName, out List<string>? apiVersionDates) && apiVersionDates.Any())
        //        {
        //            return apiVersionDates.First();
        //        }

        //        return null;
        //    }

    }
}
