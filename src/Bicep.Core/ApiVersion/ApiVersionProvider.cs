// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bicep.Core.Features;
using Bicep.Core.Resources;
using Bicep.Core.Semantics;
using Bicep.Core.Semantics.Namespaces;
using Bicep.Core.TypeSystem.Az;

namespace Bicep.Core.ApiVersion
{
    public class ApiVersionProvider : IApiVersionProvider //asdfg rename add AzResource
    {
        private static StringComparer Comparer = LanguageConstants.ResourceTypeComparer;

        private Dictionary<string, List<string>> alphaVersions = new(Comparer);
        private Dictionary<string, List<string>> betaVersions = new(Comparer);
        private Dictionary<string, List<string>> gaVersions = new(Comparer);
        private Dictionary<string, List<string>> previewVersions = new(Comparer);
        private Dictionary<string, List<string>> privatePreviewVersions = new(Comparer);
        private Dictionary<string, List<string>> rcVersions = new(Comparer);

        private static readonly Regex VersionPattern = new Regex(@"^((?<version>(\d{4}-\d{2}-\d{2}))(?<prefix>-(preview|alpha|beta|rc|privatepreview))?$)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

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
                (string? apiVersion, string? prefix) = resourceTypeReference.ApiVersion != null ? GetApiVersionAndPrefix(resourceTypeReference.ApiVersion) : (null, null);
                if (prefix != null)
                {
                    string fullyQualifiedType = resourceTypeReference.FormatType(); //asdfg? really need to do this?
                    switch (prefix)
                    {
                        case ApiVersionPrefixConstants.GA:
                            UpdateCache(gaVersions, apiVersion, fullyQualifiedType);
                            break;
                        case ApiVersionPrefixConstants.Alpha:
                            UpdateCache(alphaVersions, apiVersion, fullyQualifiedType);
                            break;
                        case ApiVersionPrefixConstants.Beta:
                            UpdateCache(betaVersions, apiVersion, fullyQualifiedType);
                            break;
                        case ApiVersionPrefixConstants.Preview:
                            UpdateCache(previewVersions, apiVersion, fullyQualifiedType);
                            break;
                        case ApiVersionPrefixConstants.PrivatePreview:
                            UpdateCache(privatePreviewVersions, apiVersion, fullyQualifiedType);
                            break;
                        case ApiVersionPrefixConstants.RC:
                            UpdateCache(rcVersions, apiVersion, fullyQualifiedType);
                            break;
                    }
                }
            }

            // Sort the lists of api versions
            gaVersions = gaVersions.ToDictionary(x => x.Key, x => x.Value.OrderByDescending(y => y).ToList(), Comparer);
            alphaVersions = alphaVersions.ToDictionary(x => x.Key, x => x.Value.OrderByDescending(y => y).ToList(), Comparer);
            betaVersions = betaVersions.ToDictionary(x => x.Key, x => x.Value.OrderByDescending(y => y).ToList(), Comparer);
            previewVersions = previewVersions.ToDictionary(x => x.Key, x => x.Value.OrderByDescending(y => y).ToList(), Comparer);
            privatePreviewVersions = privatePreviewVersions.ToDictionary(x => x.Key, x => x.Value.OrderByDescending(y => y).ToList(), Comparer);
            rcVersions = rcVersions.ToDictionary(x => x.Key, x => x.Value.OrderByDescending(y => y).ToList());
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

        public string? GetRecentApiVersion(string fullyQualifiedName, string? prefix)
        {
            switch (prefix)
            {
                case ApiVersionPrefixConstants.GA:
                    return GetRecentApiVersion(fullyQualifiedName, gaVersions);
                case ApiVersionPrefixConstants.Alpha:
                    return GetRecentApiVersion(fullyQualifiedName, alphaVersions);
                case ApiVersionPrefixConstants.Beta:
                    return GetRecentApiVersion(fullyQualifiedName, betaVersions);
                case ApiVersionPrefixConstants.Preview:
                    return GetRecentApiVersion(fullyQualifiedName, previewVersions);
                case ApiVersionPrefixConstants.PrivatePreview:
                    return GetRecentApiVersion(fullyQualifiedName, privatePreviewVersions);
                case ApiVersionPrefixConstants.RC:
                    return GetRecentApiVersion(fullyQualifiedName, rcVersions);
            }

            return null;
        }

        private string? GetRecentApiVersion(string fullyQualifiedName, Dictionary<string, List<string>> cache)
        {
            if (cache.TryGetValue(fullyQualifiedName, out List<string>? apiVersionDates) && apiVersionDates.Any())
            {
                return apiVersionDates.First();
            }

            return null;
        }

        public (string?, string?) GetApiVersionAndPrefix(string apiVersion)
        {
            MatchCollection matches = VersionPattern.Matches(apiVersion);
            string? prefix = null;
            string? version = null;

            foreach (Match match in matches)
            {
                version = match.Groups["version"].Value;
                prefix = match.Groups["prefix"].Value;

                if (version is not null)
                {
                    return (version, prefix);
                }
            }

            return (version, prefix);
        }
    }
}
