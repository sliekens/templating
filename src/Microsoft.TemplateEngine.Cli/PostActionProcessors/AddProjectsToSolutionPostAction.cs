// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class AddProjectsToSolutionPostAction : PostActionProcessor2Base, IPostActionProcessor
    {
        internal static readonly Guid ActionProcessorId = new Guid("D396686C-DE0E-4DE6-906D-291CD29FC5DE");

        public Guid Id => ActionProcessorId;

        public bool Process(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            if (string.IsNullOrEmpty(outputBasePath))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.AddProjToSlnPostActionUnresolvedSlnFile));
                return false;
            }

            IReadOnlyList<string> nearestSlnFilesFound = FindSolutionFilesAtOrAbovePath(environment.Host.FileSystem, outputBasePath);
            if (nearestSlnFilesFound.Count != 1)
            {
                Reporter.Error.WriteLine(LocalizableStrings.AddProjToSlnPostActionUnresolvedSlnFile);
                return false;
            }

            IReadOnlyList<string>? projectFiles;

            if (action.Args.TryGetValue("projectFiles", out string? configProjectFiles) && creationEffects is ICreationEffects2 creationEffects2)
            {
                JToken config = JToken.Parse(configProjectFiles);
                List<string> allProjects = new List<string>();

                if (config is JArray arr)
                {
                    foreach (JToken globText in arr)
                    {
                        if (globText.Type != JTokenType.String)
                        {
                            continue;
                        }

                        foreach (string path in GetTargetForSource(creationEffects2, globText.ToString()))
                        {
                            if (Path.GetExtension(path).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
                            {
                                allProjects.Add(path);
                            }
                        }
                    }
                }
                else if (config.Type == JTokenType.String)
                {
                    foreach (string path in GetTargetForSource(creationEffects2, config.ToString()))
                    {
                        if (Path.GetExtension(path).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
                        {
                            allProjects.Add(path);
                        }
                    }
                }

                if (allProjects.Count == 0)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.AddProjToSlnPostActionNoProjFiles);
                    return false;
                }

                projectFiles = allProjects;
            }
            else
            {
                //If the author didn't opt in to the new behavior by specifying "projectFiles", use the old behavior
                if (!TryGetProjectFilesToAdd(environment, action, templateCreationResult, outputBasePath, out projectFiles) || projectFiles == null)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.AddProjToSlnPostActionNoProjFiles);
                    return false;
                }
            }

            string solutionFolder = GetSolutionFolder(action);
            Dotnet addProjToSlnCommand = Dotnet.AddProjectsToSolution(nearestSlnFilesFound[0], projectFiles, solutionFolder);
            addProjToSlnCommand.CaptureStdOut();
            addProjToSlnCommand.CaptureStdErr();
            Reporter.Output.WriteLine(string.Format(LocalizableStrings.AddProjToSlnPostActionRunning, addProjToSlnCommand.Command));
            Dotnet.Result commandResult = addProjToSlnCommand.Execute();

            if (commandResult.ExitCode != 0)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.AddProjToSlnPostActionFailed, string.Join(" ", projectFiles), nearestSlnFilesFound[0], solutionFolder));
                Reporter.Error.WriteCommandOutput(commandResult);
                Reporter.Error.WriteLine(string.Empty);
                return false;
            }
            else
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.AddProjToSlnPostActionSucceeded, string.Join(" ", projectFiles), nearestSlnFilesFound[0], solutionFolder));
                return true;
            }
        }

        internal static IReadOnlyList<string> FindSolutionFilesAtOrAbovePath(IPhysicalFileSystem fileSystem, string outputBasePath)
        {
            return FileFindHelpers.FindFilesAtOrAbovePath(fileSystem, outputBasePath, "*.sln");
        }

        // The project files to add are a subset of the primary outputs, specifically the primary outputs indicated by the primaryOutputIndexes post action argument (semicolon separated)
        // If any indexes are out of range or non-numeric, this method returns false and projectFiles is set to null.
        internal static bool TryGetProjectFilesToAdd(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath, out IReadOnlyList<string>? projectFiles)
        {
            List<string> filesToAdd = new List<string>();

            if ((actionConfig.Args != null) && actionConfig.Args.TryGetValue("primaryOutputIndexes", out string? projectIndexes))
            {
                foreach (string indexString in projectIndexes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(indexString.Trim(), out int index))
                    {
                        if (templateCreationResult.PrimaryOutputs.Count <= index || index < 0)
                        {
                            projectFiles = null;
                            return false;
                        }

                        filesToAdd.Add(Path.Combine(outputBasePath, templateCreationResult.PrimaryOutputs[index].Path));
                    }
                    else
                    {
                        projectFiles = null;
                        return false;
                    }
                }

                projectFiles = filesToAdd;
                return true;
            }
            else
            {
                foreach (string pathString in templateCreationResult.PrimaryOutputs.Select(x => x.Path))
                {
                    filesToAdd.Add(!string.IsNullOrEmpty(outputBasePath) ? Path.Combine(outputBasePath, pathString) : pathString);
                }

                projectFiles = filesToAdd;
                return true;
            }
        }

        private string GetSolutionFolder(IPostAction actionConfig)
        {
            if (actionConfig.Args != null && actionConfig.Args.TryGetValue("solutionFolder", out string? solutionFolder))
            {
                return solutionFolder;
            }
            return string.Empty;
        }
    }
}
