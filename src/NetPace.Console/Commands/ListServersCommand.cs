using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class ListServersCommand(IAnsiConsole console, ISpeedTestService speedTestClient)
{
    /// <summary>
    /// Executes the list servers command, displaying available speed test servers.
    /// </summary>
    public async Task<int> ExecuteAsync(ListServersCommandSettings settings, CancellationToken cancellationToken)
    {
        var servers = await speedTestClient.GetServersAsync(cancellationToken);

        var serversList = servers.OrderBy(servers => servers.Location).ToList();

        // Check if any servers are available
        if (serversList.Count == 0)
        {
            throw new Exception("No servers available");
        }

        if (settings.Fastest)
        {
            await DisplayFastestServer(serversList, speedTestClient, cancellationToken);
        }
        else if (!settings.ShowLatency)
        {
            DisplayServers(serversList);
        }
        else
        {
            await DisplayServersWithLatency(serversList, speedTestClient, cancellationToken);
        }

        return 0;
    }

    private async Task DisplayFastestServer(List<IServer> servers, ISpeedTestService speedTestClient, CancellationToken cancellationToken)
    {
        console.WriteLine("");
        console.MarkupLine("Press [yellow]CTRL+C[/] to exit.");
        console.WriteLine("");

        var fastestLatencyResult = await speedTestClient.GetFastestServerByLatencyAsync(servers.ToArray(), cancellationToken);

        var table = new Table()
            .Border(TableBorder.Square)
            .BorderColor(Color.Red)
            .AddColumn(new TableColumn("Location"))
            .AddColumn(new TableColumn("Sponsor"))
            .AddColumn(new TableColumn("Url"))
            .AddColumn(new TableColumn("Latency"));

        table.AddRow(fastestLatencyResult.Server.Location ?? string.Empty, fastestLatencyResult.Server.Sponsor ?? string.Empty, fastestLatencyResult.Server.Url ?? string.Empty, $"{fastestLatencyResult.LatencyMilliseconds}ms");

        console.WriteLine("");
        console.Write(table);
    }

    private void DisplayServers(List<IServer> servers)
    {
        var table = new Table()
            .Border(TableBorder.Square)
            .BorderColor(Color.Red)
            .AddColumn(new TableColumn("Location"))
            .AddColumn(new TableColumn("Sponsor"))
            .AddColumn(new TableColumn("Url"));

        foreach (var server in servers)
        {
            table.AddRow(server.Location ?? string.Empty, server.Sponsor ?? string.Empty, server.Url ?? string.Empty);
        }

        console.WriteLine("");
        console.Write(table);
    }

    private async Task DisplayServersWithLatency(List<IServer> servers, ISpeedTestService speedTestClient, CancellationToken cancellationToken)
    {
        var table = new Table()
            .Border(TableBorder.Square)
            .BorderColor(Color.Red)
            .AddColumn(new TableColumn("Location"))
            .AddColumn(new TableColumn("Sponsor"))
            .AddColumn(new TableColumn("Url"))
            .AddColumn(new TableColumn("Latency"));

        // Add the initial server list (without latency)
        foreach (var server in servers)
        {
            table.AddRow(server.Location ?? string.Empty, server.Sponsor ?? string.Empty, server.Url ?? string.Empty);
        }

        console.WriteLine("");
        console.MarkupLine("Press [yellow]CTRL+C[/] to exit.");
        console.WriteLine("");

        await console.Live(table)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                // Screen each server through the same path auto-selection uses, so a latency
                // listed here means a real run would consider that server. The servers are
                // screened together, as selection screens them, so the listing takes about one
                // screening ceiling rather than one per unreachable server. Each row is filled in
                // as its result comes back; the table is not safe to change from two rows at once.
                var tableLock = new object();

                async Task ScreenRowAsync(int row)
                {
                    string latency;

                    try
                    {
                        var latencyResult = await speedTestClient.GetFastestServerByLatencyAsync([servers[row]], cancellationToken);

                        latency = $"{latencyResult.LatencyMilliseconds}ms";
                    }
                    catch (Exception e) when (e is not ArgumentException)
                    {
                        // Screening reports an unreachable candidate by throwing, so this is the
                        // ordinary path for a server that did not answer. A provider misconfigured
                        // through its settings also throws, as ArgumentException - that must reach
                        // the user rather than print a dash against every server in the list.
                        latency = "-";
                    }

                    lock (tableLock)
                    {
                        table.UpdateCell(row, 3, latency);
                        ctx.Refresh();
                    }
                }

                await Task.WhenAll(Enumerable.Range(0, servers.Count).Select(ScreenRowAsync));
            });
    }
}
