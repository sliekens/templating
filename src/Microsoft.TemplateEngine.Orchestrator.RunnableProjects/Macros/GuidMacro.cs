// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GuidMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("10919008-4E13-4FA8-825C-3B4DA855578E");

        public string Type => "guid";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            var config = rawConfig as GuidMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as GuidMacroConfig");
            }

            string guidFormats;
            if (!string.IsNullOrEmpty(config.Format))
            {
                guidFormats = config.Format!;
            }
            else
            {
                guidFormats = GuidMacroConfig.DefaultFormats;
            }

            Guid g = Guid.NewGuid();

            for (int i = 0; i < guidFormats.Length; ++i)
            {
                bool isUpperCase = char.IsUpper(guidFormats[i]);
                string value = g.ToString(guidFormats[i].ToString());
                value = isUpperCase ? value.ToUpperInvariant() : value.ToLowerInvariant();
                Parameter pLegacy = new Parameter
                {
                    IsVariable = true,
                    Name = config.VariableName + "-" + guidFormats[i],
                    DataType = config.DataType
                };

                // Not breaking any dependencies on exact param names and on the
                //  case insensitive matching of parameters (https://github.com/dotnet/templating/blob/7e14ef44/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/RunnableProjectGenerator.cs#L726)
                //  we need to introduce new parameters - with distinc naming for upper- and lower- casing replacements
                Parameter pNew = new Parameter
                {
                    IsVariable = true,
                    Name =
                        config.VariableName +
                        (isUpperCase ? GuidMacroConfig.UpperCaseDenominator : GuidMacroConfig.LowerCaseDenominator) +
                        guidFormats[i],
                    DataType = config.DataType
                };

                vars[pLegacy.Name] = value;
                vars[pNew.Name] = value;
                setter(pLegacy, value);
                setter(pNew, value);
            }

            Parameter pd;

            if (parameters.TryGetParameterDefinition(config.VariableName, out ITemplateParameter existingParam))
            {
                // If there is an existing parameter with this name, it must be reused so it can be referenced by name
                // for other processing, for example: if the parameter had value forms defined for creating variants.
                // When the param already exists, use its definition, but set IsVariable = true for consistency.
                pd = (Parameter)existingParam;
                pd.IsVariable = true;
            }
            else
            {
                pd = new Parameter
                {
                    IsVariable = true,
                    Name = config.VariableName
                };
            }
            var defaultFormat = string.IsNullOrEmpty(config.DefaultFormat) ? "D" : config.DefaultFormat!;
            string defaultValue = char.IsUpper(defaultFormat[0]) ?
                g.ToString(config.DefaultFormat).ToUpperInvariant() :
                g.ToString(config.DefaultFormat).ToLowerInvariant();
            vars[config.VariableName] = defaultValue;
            setter(pd, defaultValue);
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            var deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            deferredConfig.Parameters.TryGetValue("format", out JToken? formatToken);
            string? format = formatToken?.ToString();

            deferredConfig.Parameters.TryGetValue("defaultFormat", out JToken defaultFormatToken);
            string? defaultFormat = defaultFormatToken?.ToString();

            IMacroConfig realConfig = new GuidMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, format, defaultFormat);
            return realConfig;
        }
    }
}
