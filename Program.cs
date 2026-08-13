// Copyright (c) Thomas Gossler. All rights reserved.
// Licensed under the MIT license.

#nullable disable warnings

using System;
using System.Windows.Forms;
using Spectre.Console.Cli;

namespace WebPageHost;

/// <summary>
/// Command line interface tool for opening web pages in a WebView2 control.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        int? forwardedExitCode = ReuseWindowCoordinator.Initialize(args);
        if (forwardedExitCode.HasValue) {
            return forwardedExitCode.Value;
        }

        var app = new CommandApp();
        app.Configure(config => {
            _ = config.SetApplicationName(Common.ProgramName);

            _ = config.AddCommand<OpenCommand>("open")
                .WithDescription("Opens the URL in a new window with an embedded web browser.")
                .WithExample(new[] { "--help" })
                .WithExample(new[] { "open", "--help" })
                .WithExample(new[] { "open", "https://www.google.com/", "--zoomfactor", "0.6" })
                .WithExample(new[] { "open", "https://www.google.com/", "-x", "document.title" });

            _ = config.AddCommand<CleanupCommand>("cleanup")
                .WithDescription("Resets the current user's web browser persistent data folder and registry settings.")
                .WithExample(new[] { "cleanup" });

            _ = config.ValidateExamples();
        });

        EventHandler? idleHandler = null;
        idleHandler = (s, e) => {
            foreach (Form openForm in Application.OpenForms) {
                if (openForm is MainForm form) {
                    ReuseWindowCoordinator.Start(form);
                    Application.Idle -= idleHandler;
                    break;
                }
            }
        };
        Application.Idle += idleHandler;

        try {
            return app.Run(args);
        }
        finally {
            Application.Idle -= idleHandler;
            ReuseWindowCoordinator.Shutdown();
        }
    }
}
