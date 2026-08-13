// Copyright (c) Thomas Gossler. All rights reserved.
// Licensed under the MIT license.

using System;

namespace WebPageHost;

/// <summary>
/// Connects command-line reuse-window requests with the application's main form.
/// </summary>
internal static class ReuseWindowCoordinator
{
    private static ReuseWindowBroker? broker;

    /// <summary>
    /// Initializes reuse-window handling. Returns an exit code when this process only forwarded a URL.
    /// </summary>
    public static int? Initialize(string[] args)
    {
        if (args.Length < 2 || !args[0].Equals("open", StringComparison.OrdinalIgnoreCase) ||
            !Contains(args, "--reusewindow") || Contains(args, "-c") || Contains(args, "--continue")) {
            return null;
        }

        string? url = FindUrl(args);
        if (string.IsNullOrWhiteSpace(url)) {
            return null;
        }

        string environmentName = NormalizeEnvironmentName(FindEnvironmentName(args));
        broker = ReuseWindowBroker.Create(environmentName);
        if (broker.IsPrimary) {
            return null;
        }

        int exitCode = broker.ForwardUrl(url) ? 0 : 1;
        broker.Dispose();
        broker = null;
        return exitCode;
    }

    public static void Start(MainForm form)
    {
        broker?.Start(form);
    }

    public static void Shutdown()
    {
        broker?.Dispose();
        broker = null;
    }

    private static bool Contains(string[] args, string value)
    {
        foreach (string arg in args) {
            if (arg.Equals(value, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    private static string? FindUrl(string[] args)
    {
        for (int i = 1; i < args.Length; i++) {
            string arg = args[i];
            if (arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) {
                return arg;
            }
        }
        return null;
    }

    private static string? FindEnvironmentName(string[] args)
    {
        for (int i = 1; i < args.Length; i++) {
            string arg = args[i];
            if ((arg.Equals("-e", StringComparison.OrdinalIgnoreCase) || arg.Equals("--envname", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length) {
                return args[i + 1];
            }
            if (arg.StartsWith("--envname=", StringComparison.OrdinalIgnoreCase)) {
                return arg.Substring("--envname=".Length);
            }
            if (arg.StartsWith("-e=", StringComparison.OrdinalIgnoreCase)) {
                return arg.Substring(3);
            }
        }
        return null;
    }

    private static string NormalizeEnvironmentName(string? environmentName)
    {
        return string.IsNullOrWhiteSpace(environmentName)
            ? string.Empty
            : environmentName.Replace(" ", "").Replace("\\", "").Replace("/", "").Replace(".", "").Replace("*", "").Replace("?", "");
    }
}
