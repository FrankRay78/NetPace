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
                // Fetch the latency for each server
                // and update the table as they come back
                for (int i = 0; i < servers.Count; i++)
                {
                    var server = servers[i];

                    try
                    {
                        var latencyResult = await speedTestClient.GetServerLatencyAsync(server, cancellationToken);

                        table.UpdateCell(i, 3, $"{latencyResult.LatencyMilliseconds}ms");
                    }
                    catch (Exception)
                    {
                        table.UpdateCell(i, 3, "-");
                    }

                    ctx.Refresh();
                }
            });
    }
}
