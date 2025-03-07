﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.EndToEndTestHarness
{
    internal static class ParserFactory
    {
        internal static Parser CreateParser(Command command, bool disableHelp = false)
        {
            var builder = new CommandLineBuilder(command)
            .UseParseDirective()
            .UseSuggestDirective()
            .DisablePosixBundling();

            if (!disableHelp)
            {
                builder = builder.UseHelp();
            }
            return builder.Build();
        }

        private static CommandLineBuilder DisablePosixBundling(this CommandLineBuilder builder)
        {
            builder.EnablePosixBundling = false;
            return builder;
        }
    }
}
