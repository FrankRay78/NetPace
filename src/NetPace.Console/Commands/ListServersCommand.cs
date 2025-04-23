using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class ListServersCommand : AsyncCommand<ListServersCommandSettings>
{
    private IAnsiConsole console;
    private ISpeedTestService speedTestClient;

    public ListServersCommand(IAnsiConsole console, ISpeedTestService speedTestClient)
    {
        this.console = console;
        this.speedTestClient = speedTestClient;

    }

    public override async Task<int> ExecuteAsync(CommandContext context, ListServersCommandSettings settings)
    {
        var servers = await speedTestClient.GetServersAsync();

        var serversList = servers.OrderBy(servers => servers.Location).ToList();

        if (settings.ShowLatency == null || !settings.ShowLatency.HasValue || !settings.ShowLatency.Value)
        {
            DisplayServers(serversList);
        }
        else
        {
            await DisplayServersWithLatency(serversList, speedTestClient);
        }

        return 0;
    }

    private void DisplayServers(List<IServer> servers)
    {
        var table = new Table()
            .Border(TableBorder.Square)
            .BorderColor(Color.Red)
            .AddColumn(new TableColumn("Location"))
            .AddColumn(new TableColumn("Sponsor"));

        foreach (var server in servers)
        {
            table.AddRow(server.Location ?? string.Empty, server.Sponsor ?? string.Empty);
        }

        console.WriteLine("");
        console.Write(table);
    }

    private async Task DisplayServersWithLatency(List<IServer> servers, ISpeedTestService speedTestClient)
    {
        var table = new Table()
            .Border(TableBorder.Square)
            .BorderColor(Color.Red)
            .AddColumn(new TableColumn("Location"))
            .AddColumn(new TableColumn("Sponsor"))
            .AddColumn(new TableColumn("Latency"));

        // Add the initial server list (without latency)
        foreach (var server in servers)
        {
            table.AddRow(server.Location ?? string.Empty, server.Sponsor ?? string.Empty);
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

                    var latencyResult = await speedTestClient.GetServerLatencyAsync(server);

                    if (latencyResult != null)
                    {
                        table.UpdateCell(i, 2, $"{latencyResult.Latency}ms");
                    }
                    else
                    {
                        table.UpdateCell(i, 2, "-");
                    }

                    ctx.Refresh();
                }
            });
    }
}