// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bicep.Core.Features;
using Bicep.Core.Resources;
using Bicep.Core.Semantics;
using Bicep.Core.Semantics.Namespaces;
using Bicep.Core.TypeSystem.Az;
using ResourceScope = Bicep.Core.TypeSystem.ResourceScope;

namespace Bicep.Core.ApiVersion
{
    public class ApiVersionProvider : IApiVersionProvider
    {
        private static StringComparer Comparer = LanguageConstants.ResourceTypeComparer;

        private Dictionary<ResourceScope, Cache> _caches = new();

        public ApiVersionProvider()
        {
        }

        // for unit testing
        public void InjectTypeReferences(ResourceScope scope,IEnumerable< ResourceTypeReference> resourceTypeReferences)
        {
            var cache = GetCache(scope);
            Debug.Assert(!cache.typesCached, $"{nameof(InjectTypeReferences)} Types have already been cached for scope {scope}");
            cache.injectedTypes = resourceTypeReferences.ToArray();
        }

        private Cache GetCache(ResourceScope scope)
        {
            if (_caches.TryGetValue(scope, out Cache? cache))
            {
                return cache;
            }
            else
            {
                var newCache = new Cache();
                _caches[scope] = newCache;
                return newCache;
            }
        }

        private Cache VerifyCache(ResourceScope scope)
        {
            var cache = GetCache(scope);
            if (cache.typesCached)
            {
                return cache;
            }
            cache.typesCached = true;

            IEnumerable<ResourceTypeReference> resourceTypeReferences;
            if (cache.injectedTypes is null)
            {
                DefaultNamespaceProvider defaultNamespaceProvider = new DefaultNamespaceProvider(new AzResourceTypeLoader(), new FeatureProvider());
                NamespaceResolver namespaceResolver = NamespaceResolver.Create(defaultNamespaceProvider, scope, Enumerable.Empty<ImportedNamespaceSymbol>());
                resourceTypeReferences = namespaceResolver.GetAvailableResourceTypes();
            }
            else
            {
                resourceTypeReferences = cache.injectedTypes;
            }

            cache.CacheApiVersions(resourceTypeReferences);
            return cache;
        }

        //asdfg
        public IEnumerable<string> GetSortedValidApiVersions(ResourceScope scope, string fullyQualifiedResourceType)
        {
            var cache = VerifyCache(scope);

            if (!cache.stableVersions.Any() && !cache.previewVersions.Any())
            {
                throw new InvalidCastException("ApiVersionProvider was unable to find any resource types");
            }

            var allVersions = new List<string>();

            if (cache.stableVersions.TryGetValue(fullyQualifiedResourceType, out List<string>? stable))
            {
                allVersions.AddRange(stable);
            }
            if (cache.previewVersions.TryGetValue(fullyQualifiedResourceType, out List<string>? previews))
            {
                allVersions.AddRange(previews);
            }

            //asdfg    allVersions.Sort();
            return allVersions.ToArray();
        }

        private class Cache
        {
            public bool typesCached;
            public ResourceTypeReference[]? injectedTypes;

            //asdfg
            // E.g. 2022-07-07
            public Dictionary<string, List<string>> stableVersions = new(Comparer);
            // E.g. 2022-07-07-alpha, 2022-07-07-preview, 2022-07-07-privatepreview etc.
            public Dictionary<string, List<string>> previewVersions = new(Comparer);

            public void CacheApiVersions(IEnumerable<ResourceTypeReference> resourceTypeReferences)
            {
                this.typesCached = true;

                foreach (var resourceTypeReference in resourceTypeReferences)
                {
                    (string? apiVersion, string? suffix) = resourceTypeReference.ApiVersion != null ? ApiVersionHelper.TryParse(resourceTypeReference.ApiVersion) : (null, null);
                    if (apiVersion is not null)
                    {
                        string fullyQualifiedType = resourceTypeReference.FormatType();
                        if (suffix == ApiVersionSuffixes.GA)
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
                        throw new InvalidOperationException($"Invalid resource type and apiVersion found: {resourceTypeReference.FormatType()}");
                    }
                }

                // Sort the lists of api versions
                stableVersions = stableVersions.ToDictionary(x => x.Key, x => x.Value.OrderBy(y => y).ToList(), Comparer);
                previewVersions = previewVersions.ToDictionary(x => x.Key, x => x.Value.OrderBy(y => y).ToList(), Comparer);
            }

            private void UpdateCache(Dictionary<string, List<string>> listOfTypes, string? apiVersion, string fullyQualifiedType)
            {
                if (apiVersion is not null)
                {
                    if (listOfTypes.TryGetValue(fullyQualifiedType, out List<string>? value))
                    {
                        value.Add(apiVersion);
                        listOfTypes[fullyQualifiedType] = value;
                    }
                    else
                    {
                        listOfTypes.Add(fullyQualifiedType, new List<string> { apiVersion });
                    }
                }
            }
        }

    }
}
