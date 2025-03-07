// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.CodeAction;
using Bicep.Core.Diagnostics;
using Bicep.Core.Navigation;
using Bicep.Core.Semantics;
using Bicep.Core.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bicep.Core.Analyzers.Linter.Rules
{
    public sealed class ArtifactsParametersRule : LocationRuleBase
    {
        public new const string Code = "artifacts-parameters";

        // Bicep parameters are case-insensitive, but the ARM template versions that they represent are not, and the existing
        // publish scripts use ARM, so the rule needs to use case-insensitive checks
        private readonly StringComparison ArmParameterComparison = StringComparison.InvariantCultureIgnoreCase;

        private const string ArtifactsLocationParamNameCommon = "artifactsLocation"; // ArtifactsLocationName and ArtifactsLocationSasTokenName both contain this string
        private const string ArtifactsLocationName = $"_artifactsLocation";
        private const string ArtifactsLocationSasTokenName = $"_artifactsLocationSasToken";

        public ArtifactsParametersRule() : base(
            code: Code,
            description: "Follow best practices when including the _artifactsLocation and _artifactsLocationSasToken parameters.",
            docUri: new Uri($"https://aka.ms/bicep/linter/{Code}"))
        {
            Debug.Assert(ArtifactsLocationName.Contains(ArtifactsLocationParamNameCommon, ArmParameterComparison));
            Debug.Assert(ArtifactsLocationSasTokenName.Contains(ArtifactsLocationParamNameCommon, ArmParameterComparison));
            Debug.Assert(ArtifactsLocationSasTokenName.ToLowerInvariant().Contains(ArtifactsLocationParamNameCommon.ToUpperInvariant(), ArmParameterComparison));
            Debug.Assert(ArtifactsLocationName.StartsWith("_"));
            Debug.Assert(ArtifactsLocationSasTokenName.StartsWith("_"));
            Debug.Assert(!ArtifactsLocationParamNameCommon.StartsWith("_"));
        }

        public override string FormatMessage(params object[] values)
            => string.Format((string)values[0]);

        override public IEnumerable<IDiagnostic> AnalyzeInternal(SemanticModel model)
        {
            var diagnostics = new List<IDiagnostic>();

            diagnostics.AddRange(VerifyTopLevelParameters(model));
            diagnostics.AddRange(VerifyModuleParameters(model));

            return diagnostics.ToArray();
        }

        private IEnumerable<IDiagnostic> VerifyTopLevelParameters(SemanticModel model)
        {
            var paramsOfInterest = model.Root.ParameterDeclarations.Where(p => p.Name.Contains(ArtifactsLocationParamNameCommon, ArmParameterComparison)).ToArray();
            if (paramsOfInterest.Length == 0)
            {
                // Neither parameter exists, nothing to test
                yield break;
            }

            // Find artifacts parameters
            var (artifactsLocationParam, artifactsLocationParamDiagnostic) = FindArtifactsParameter(paramsOfInterest, ArtifactsLocationName);
            if (artifactsLocationParamDiagnostic is not null)
            {
                yield return artifactsLocationParamDiagnostic;
            }
            var (artifactsSasParam, artifactsSasParamDiagnostic) = FindArtifactsParameter(paramsOfInterest, ArtifactsLocationSasTokenName);
            if (artifactsSasParamDiagnostic is not null)
            {
                yield return artifactsSasParamDiagnostic;
            }

            // RULE: If you provide one parameter (either _artifactsLocation or _artifactsLocationSasToken), you must provide the other.
            if (artifactsLocationParam is not null && artifactsSasParam is null)
            {
                yield return CreateDiagnosticForSpan(
                    artifactsLocationParam.NameSyntax.Span,
                    string.Format(CoreResources.ArtifactsLocationRule_Error_ParamMissing, ArtifactsLocationName, ArtifactsLocationSasTokenName));
            }
            else if (artifactsSasParam is not null && artifactsLocationParam is null)
            {
                yield return CreateDiagnosticForSpan(
                     artifactsSasParam.NameSyntax.Span,
                     string.Format(CoreResources.ArtifactsLocationRule_Error_ParamMissing, ArtifactsLocationSasTokenName, ArtifactsLocationName));
            }
            if (artifactsLocationParam is null || artifactsSasParam is null)
            {
                yield break;
            }

            // Both parameters exist at this point
            Debug.Assert(artifactsLocationParam is not null);
            Debug.Assert(artifactsSasParam is not null);

            // RULE: _artifactsLocation must be a string.
            if (VerifyParameterType(artifactsLocationParam, LanguageConstants.TypeNameString) is IDiagnostic diagnosticLocType)
            {
                yield return diagnosticLocType;
            }

            // RULE: _artifactsLocationSasToken must be a secure string.
            if (VerifyParameterType(artifactsSasParam, LanguageConstants.TypeNameString) is IDiagnostic diagnosticSasType)
            {
                yield return diagnosticSasType;
            }
            if (VerifyParameterIsSecure(artifactsSasParam) is IDiagnostic diagnosticSecure)
            {
                yield return diagnosticSecure;
            }

            // Verify default values
            foreach (var diag in VerifyDefaultValues(artifactsLocationParam, artifactsSasParam))
            {
                yield return diag;
            }
        }

        private IEnumerable<IDiagnostic> VerifyModuleParameters(SemanticModel model)
        {
            // RULE: When referencing a module, if that module has an _artifactsLocation or _artifactsLocationSasToken parameter, a value must be
            //     passed in for those parameters, even if they have default values in the module.

            foreach (var module in model.Root.ModuleDeclarations)
            {
                if (module.TryGetSemanticModel(out ISemanticModel? moduleModel, out _))
                {
                    foreach (var formalParam in moduleModel.Parameters)
                    {
                        if (formalParam.Name.Equals(ArtifactsLocationName, ArmParameterComparison) ||
                            formalParam.Name.Equals(ArtifactsLocationSasTokenName, ArmParameterComparison))
                        {
                            // Expect a parameter passed in with the same name
                            if (module.DeclaringSyntax is ModuleDeclarationSyntax moduleSyntax)
                            {
                                var moduleParamsPropertyObject = moduleSyntax.TryGetBody()?
                                    .TryGetPropertyByName(LanguageConstants.ModuleParamsPropertyName) as ObjectPropertySyntax;

                                // Look for a parameter value being passed in for the formal parameter that we found
                                // Be sure the param value we're looking for matches exactly the name/casing for the formal parameter (ordinal)
                                var passedInParameter = (moduleParamsPropertyObject?.Value as ObjectSyntax)?.Properties.Where(p =>
                                   LanguageConstants.IdentifierComparer.Equals( // Use Bicep casing here
                                       (p.Key as IdentifierSyntax)?.IdentifierName,
                                       formalParam.Name))
                                    .FirstOrDefault();
                                if (passedInParameter == null)
                                {
                                    yield return CreateDiagnosticForSpan(
                                        module.NameSyntax.Span,
                                        string.Format(
                                            "Parameter '{0}' of module '{1}' should be assigned an explicit value.",
                                            formalParam.Name,
                                            module.Name));
                                }
                            }
                        }
                    }
                }
            }
        }

        private IEnumerable<IDiagnostic> VerifyDefaultValues(
            ParameterSymbol artifactsLocationParam,
            ParameterSymbol artifactsSasParam)
        {
            // The old ARM TTK rules regarding default values were this:
            //   _artifactsLocation must have a default value in the main template.
            //   _artifactsLocation can't have a default value in a nested template.
            //   _artifactsLocation must have either "[deployment().properties.templateLink.uri]" or the raw repo URL for its default value.
            //   _artifactsLocationSasToken can only have an empty string for its default value.
            //   _artifactsLocationSasToken can't have a default value in a nested template.
            //
            // With VerifyModuleParameters(), we no longer need to worry about main vs nested template and default values
            // are optional.  The new rules are:
            //   If _artifactsLocation has a default value, it must be either "[deployment().properties.templateLink.uri]" or raw URL (no rule about
            //     whether it should or shouldn't have a default value, instead checking module references to make sure the value is passed through)
            //   If _artifactsLocationSasToken has a default value, it must be an empty string (no rule about
            //     whether it should or shouldn't have a default value, instead checking module references)

            var artifactsLocationDefaultValue = GetParameterDefaultValue(artifactsLocationParam);
            var artifactsSasDefaultValue = GetParameterDefaultValue(artifactsSasParam);

            // ... RULE: If _artifactsLocation has a default value, it must be either "[deployment().properties.templateLink.uri]" or a raw URL
            if (artifactsLocationDefaultValue != null) // pass if no default value
            {
                var pass = false;
                var literal = TryGetStringLiteral(artifactsLocationDefaultValue);
                if (literal is not null && literal.StartsWith("http"))
                {
                    pass = true;
                }
                else
                {
                    // We're not worried about an exact match.
                    string syntaxString = artifactsLocationDefaultValue.ToText();
                    if (Regex.Matches(syntaxString, "deployment\\(.*\\.templatelink", RegexOptions.IgnoreCase).Any())
                    {
                        pass = true;
                    }
                }
                if (!pass)
                {
                    yield return CreateDiagnosticForSpan(
                        artifactsLocationDefaultValue.Span,
                        $"If the '{ArtifactsLocationName}' parameter has a default value, it must be a raw URL or an expression like '{"deployment().properties.templateLink.uri"}'.");
                }
            }

            //  ... RULE: If _artifactsLocationSasToken has a default value, it must be an empty string
            if (artifactsSasDefaultValue != null) // pass if no default value
            {
                var literal = TryGetStringLiteral(artifactsSasDefaultValue);
                if (literal != string.Empty)
                {
                    yield return CreateDiagnosticForSpan(
                        artifactsSasDefaultValue.Span,
                        $"If the '{ArtifactsLocationSasTokenName}' parameter has a default value, it must be an empty string.");
                }
            }
        }

        private SyntaxBase? GetParameterDefaultValue(ParameterSymbol parameterSymbol)
        {
            if (parameterSymbol.DeclaringSyntax is ParameterDeclarationSyntax syntax)
            {
                return SyntaxHelper.TryGetDefaultValue(syntax);
            }

            return null;
        }


        private string? TryGetStringLiteral(SyntaxBase syntax)
        {
            if (syntax is StringSyntax @string)
            {
                return @string.TryGetLiteralValue();
            }

            return null;
        }

        private string? GetParameterType(ParameterSymbol parameterSymbol)
        {
            if (parameterSymbol.DeclaringSyntax is ParameterDeclarationSyntax parameterDeclaration
               && parameterDeclaration.ParameterType is SimpleTypeSyntax typeSyntax)
            {
                if (typeSyntax.HasParseErrors())
                {
                    return null;
                }

                return typeSyntax.TypeName;
            }

            return null;
        }

        private IDiagnostic? VerifyParameterType(ParameterSymbol parameter, string expectedTypeName)
        {
            if (GetParameterType(parameter) is string paramType
               && paramType != expectedTypeName)
            {
                return CreateFixableDiagnosticForSpan(
                    parameter.DeclaringParameter.Type.Span,
                    new CodeFix(
                        String.Format(CoreResources.ArtifactsLocationRule_FixTitle_ChangeType, parameter.Name, expectedTypeName),
                        isPreferred: true,
                        CodeFixKind.QuickFix,
                        new CodeReplacement(parameter.DeclaringParameter.Type.Span, expectedTypeName)
                    ),
                    string.Format(CoreResources.ArtifactsLocationRule_Error_ParamMustBeType, parameter.Name, expectedTypeName)
                );
            }

            return null;
        }

        private IDiagnostic? VerifyParameterIsSecure(ParameterSymbol parameter)
        {
            if (!parameter.IsSecure())
            {
                return CreateDiagnosticForSpan(
                    parameter.DeclaringParameter.Type.Span,
                    string.Format(CoreResources.ArtifactsLocationRule_Error_ParamMustBeSecure, parameter.Name)
                );
            }

            return null;
        }

        private (ParameterSymbol?, IDiagnostic? diagnostic) FindArtifactsParameter(ParameterSymbol[] parameters, string parameterName)
        {
            Debug.Assert(parameterName[0] == '_');
            var nameWithoutUnderscore = parameterName.Substring(1);

            var paramNoUnderscore = parameters.Where(p => p.Name.Equals(nameWithoutUnderscore, ArmParameterComparison)).FirstOrDefault();
            if (paramNoUnderscore is not null)
            {
                return (
                     paramNoUnderscore,
                     CreateDiagnosticForSpan(
                       paramNoUnderscore.NameSyntax.Span,
                       string.Format(CoreResources.ArtifactsLocationRule_Error_MissingUnderscore, nameWithoutUnderscore)));
            }

            var param = parameters.Where(p => p.Name.Equals(parameterName, ArmParameterComparison)).FirstOrDefault();
            if (param is not null)
            {
                return (param, null);
            }

            return (null, null);
        }
    }
}
