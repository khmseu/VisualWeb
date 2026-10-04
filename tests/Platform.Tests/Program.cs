using Xunit;

if (args is ["--wait-probe"])
{
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}

if (args.Length > 0 && args[0] == "--process-probe")
{
    if (args.Length != 3 || args[1] != "argument with spaces" || args[2] != "\"quotes\""
        || Environment.GetEnvironmentVariable("VISUALWEB_PROCESS_PROBE") != "value with spaces")
    {
        return 1;
    }

    await File.WriteAllTextAsync("probe-ok", "ok");
    return 0;
}

return await Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args);
