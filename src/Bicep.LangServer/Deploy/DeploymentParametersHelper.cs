// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bicep.LanguageServer.Configuration;
using Bicep.LanguageServer.Handlers;
using Newtonsoft.Json.Linq;

namespace Bicep.LanguageServer.Deploy
{
    public class DeploymentParametersHelper
    {
        public static string GetUpdatedParametersFileContents(
            string documentPath,
            string parametersFileName,
            string parametersFilePath,
            ParametersFileCreateOrUpdate updateOrCreateParametersFile,
            IEnumerable<BicepUpdatedDeploymentParameter> updatedDeploymentParameters)
        {
            try
            {
                var text = !string.IsNullOrWhiteSpace(parametersFilePath) ? File.ReadAllText(parametersFilePath) : "{}";
                var jObject = GetParametersObjectValueIfParametersFileIsArmSchema(text);

                foreach (var updatedDeploymentParameter in updatedDeploymentParameters)
                {
                    var name = updatedDeploymentParameter.name;
                    var parameterType = updatedDeploymentParameter.parameterType;
                    var valueObject = UpdateJObjectBasedOnParameterType(
                        updatedDeploymentParameter.parameterType,
                        updatedDeploymentParameter.value,
                        JObject.Parse("{}"));

                    if (jObject.ContainsKey(name))
                    {
                        continue;
                    }
                    else
                    {
                        var jsonEditor = new JsonEditor(text);
                        (int line, int column, string text)? insertion = jsonEditor.InsertIfNotExist(new string[] { updatedDeploymentParameter.name }, valueObject);

                        if (insertion.HasValue)
                        {
                            var (line, column, insertText) = insertion.Value;

                            text = JsonEditor.ApplyInsertion(text, (line, column, insertText));
                        }
                    }
                }

                if (updatedDeploymentParameters.Any())
                {
                    if (updateOrCreateParametersFile == ParametersFileCreateOrUpdate.Update)
                    {
                        File.WriteAllText(parametersFilePath, text);
                    }
                    else if (updateOrCreateParametersFile == ParametersFileCreateOrUpdate.Create)
                    {
                        var directoryContainingBicepFile = Path.GetDirectoryName(documentPath);
                        if (directoryContainingBicepFile is not null)
                        {
                            File.WriteAllText(Path.Combine(directoryContainingBicepFile, parametersFileName), text);
                        }
                    }
                }

                return text;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format(LangServerResources.InvalidParameterFileDeploymentFailedMessage, documentPath, parametersFilePath, e.Message));
            }
        }

        public static JObject UpdateJObjectBasedOnParameterType(ParameterType? parameterType, string? value, JObject valueObject)
        {
            if (parameterType is not null)
            {
                if (parameterType == ParameterType.Int &&
                    value is not null &&
                    value.GetType() != typeof(int))
                {
                    var updatedValue = int.Parse(value);
                    valueObject.Add("value", updatedValue);

                    return valueObject;
                }
                else if (parameterType == ParameterType.Bool &&
                    value is not null &&
                    value.GetType() != typeof(bool))
                {
                    var updatedValue = bool.Parse(value);
                    valueObject.Add("value", updatedValue);

                    return valueObject;
                }
            }

            valueObject.Add("value", value);

            return valueObject;
        }

        public static JObject GetParametersObjectValueIfParametersFileIsArmSchema(string text)
        {
            var jObject = JObject.Parse(text);
            if (jObject.ContainsKey("$schema") && jObject.ContainsKey("contentVersion") && jObject.ContainsKey("parameters"))
            {
                var parametersObject = jObject["parameters"];
                if (parametersObject is not null)
                {
                    return JObject.Parse(parametersObject.ToString());
                }
            }

            return jObject;
        }
    }
}
