# Codex Handoff: Validate Reuse-Window Support and Produce Windows Release

## Goal

Fully validate the new per-environment window-reuse behavior in this fork and produce a source-identical Windows x64 release build suitable for use as the browser configured in IBKR TWS on Windows 11.

## Repository state

- Repository: `evs625/WebPageHost`
- Source of truth: `main`
- Reuse-window implementation commit: `dfb131841d25ef03cad0419d47ccf0153d4f41ef` (`Reuse existing environment window (#1)`).
- New CLI option: `--reusewindow`.
- Intended TWS invocation:

```text
WebPageHost.exe open <URL> -k -e IBKR --reusewindow
```

## Intended behavior

1. First launch for an environment creates the WebView2 window.
2. A later launch using the same normalized `-e/--envname` and `--reusewindow` sends the requested URL to the existing process, navigates that WebView, restores it if minimized, brings it to the foreground, and exits without creating a second window.
3. Different environment names remain independent.
4. Existing `-k/--keepuserdata` cookie/local-storage/profile persistence remains unchanged.
5. Existing behavior without `--reusewindow` remains unchanged.
6. Existing popup/new-window handling remains inside WebPageHost rather than escaping to the system browser.

## Current implementation to review

The change introduces:

- `ReuseWindowBroker.cs`: named mutex + named pipe, keyed by normalized environment name.
- `ReuseWindowCoordinator.cs`: early command-line routing and forwarding.
- `Program.cs`: coordinator lifecycle/startup wiring.
- `OpenCommand.Settings.cs`: `--reusewindow` option.

Do not assume the implementation is correct. Review it critically for:

- startup races;
- named-mutex semantics and lifetime;
- named-pipe lifecycle and error handling;
- UI-thread dispatch;
- process shutdown;
- command-line parsing;
- normalization collisions;
- stale/crashed primary process behavior;
- compatibility with existing options;
- preservation of upstream behavior.

## Required validation

Build and test the actual C# project on Windows 11 using the project target/runtime:

- `net10.0-windows7.0`
- `win-x64`
- Release
- self-contained
- single-file

At minimum exercise all of the following:

1. First launch with `-k -e IBKR --reusewindow`.
2. Second launch for the same environment with a different URL: the same window is reused and navigated.
3. Existing minimized window is restored and activated.
4. Close the window, then relaunch: a fresh window starts normally.
5. Persistent cookies/localStorage survive close/relaunch with `-k`.
6. Two different `-e` values create separate windows and separate profiles.
7. Launch without `--reusewindow` still creates separate windows.
8. Default/no environment with `--reusewindow` behaves deterministically.
9. Verify `-e NAME`, `--envname NAME`, `--envname=NAME`, and supported option ordering.
10. Verify URLs containing query strings, fragments, escaped characters, and long authentication handoff URLs.
11. Near-simultaneous launches for the same environment (race test).
12. `-c/--continue` interaction does not regress existing behavior.
13. Popup / `window.open()` / `target=_blank` behavior remains inside the WebView host.
14. Clean shutdown leaves no orphan host process and no stuck pipe/mutex preventing the next launch.
15. Repeated reuse cycle: at least 20 sequential URL forwards to one environment.
16. Abruptly terminate the primary process and verify the next invocation can recover and become primary.

## Test/fix policy

If any defect is found, make the smallest maintainable fix consistent with the existing code style and preserve all upstream behavior not directly related to reuse-window support.

Add automated tests where practical, especially for:

- command-line parsing;
- environment-name normalization/keying;
- IPC behavior that can be tested without UI automation;
- race/failure cases that can be made deterministic.

Do not rewrite unrelated parts of the application.

## Reproducible build

Prefer adding a minimal GitHub Actions Windows workflow that:

1. checks out the repository;
2. installs the required .NET SDK;
3. restores dependencies;
4. builds/tests;
5. runs `dotnet publish` for Release `win-x64`;
6. uploads the resulting single-file executable as an artifact.

Do not change runtime/product behavior merely to satisfy CI.

## Release deliverables

Produce all of the following:

1. Passing Release build from the C# source in this repository.
2. Final `WebPageHost.exe` x64 self-contained single-file artifact.
3. SHA-256 of the executable.
4. Exact source commit SHA from which the executable was built.
5. Short validation report listing tests run, results, fixes made, and any remaining limitations.
6. Reproducible build instructions or GitHub Actions workflow.

## Important note about the earlier binary

A temporary unsigned native Windows x64 compatibility binary named `WebPageHost-Reuse-win-x64.exe` was produced outside this repository because that environment did not have the required .NET toolchain. Its SHA-256 was:

```text
8ee505d10ca279467577a8782cc679620448b982cd9d26670bfc338c80dd45de
```

That binary is **not source-identical to this C# fork and is not the canonical release candidate**. Do not validate or release it as the final product. Treat the C# repository as the source of truth and generate a fresh executable from it.

## Acceptance criteria

The task is complete only when:

- the reuse-window implementation has been code-reviewed;
- the actual C# source has been built on Windows;
- the required behavioral tests have been run;
- any discovered defects have been fixed;
- a reproducible source-identical Release `.exe` has been produced;
- the final report contains the source commit SHA and executable SHA-256.
