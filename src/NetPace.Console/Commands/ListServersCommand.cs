using NetPace.Console.Theming;
using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class ListServersCommand(IAnsiConsole console, ISpeedTestService speedTestClient, Theme theme)
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

    /// <summary>
    /// Tells the user how to interrupt a listing that takes a while — both the live listing and
    /// the latency scan before a static one. Drawn in the theme, with the key picked out wherever
    /// the theme has a colour for it.
    /// </summary>
    private void WriteExitHint() =>
        console.WriteStyledLine(("Press ", Style.Plain), ("CTRL+C", theme.Hint), (" to exit.", Style.Plain));

    /// <summary>
    /// A server detail as the table cell that shows it.
    /// </summary>
    /// <remarks>
    /// Speedtest publishes these values, so NetPace does not control what they contain, and they
    /// are drawn as plain text rather than handed to the table as strings: a string cell is read as
    /// Spectre.Console markup, where a square bracket in a location, sponsor or URL fails the whole
    /// listing, and a value that happens to read as a styling tag is drawn styled with its brackets
    /// eaten. The speed test's own writers show the same fields as plain text.
    /// </remarks>
    private static Text Detail(string? value) => new(value ?? string.Empty);

    private async Task DisplayFastestServer(List<IServer> servers, ISpeedTestService speedTestClient, CancellationToken cancellationToken)
    {
        console.WriteLine("");
        WriteExitHint();
        console.WriteLine("");

        var fastestLatencyResult = await speedTestClient.GetFastestServerByLatencyAsync(servers.ToArray(), cancellationToken);

        var table = new Table()
            .Border(TableBorder.Square)
            .BorderStyle(theme.TableBorder)
            .AddColumn(new TableColumn("Location"))
            .AddColumn(new TableColumn("Sponsor"))
            .AddColumn(new TableColumn("Url"))
            .AddColumn(new TableColumn("Latency"));

        table.AddRow(Detail(fastestLatencyResult.Server.Location), Detail(fastestLatencyResult.Server.Sponsor), Detail(fastestLatencyResult.Server.Url), new Text($"{fastestLatencyResult.LatencyMilliseconds}ms"));

        console.WriteLine("");
        console.Write(table);
    }

    private void DisplayServers(List<IServer> servers)
    {
        var table = new Table()
            .Border(TableBorder.Square)
            .BorderStyle(theme.TableBorder)
            .AddColumn(new TableColumn("Location"))
            .AddColumn(new TableColumn("Sponsor"))
            .AddColumn(new TableColumn("Url"));

        foreach (var server in servers)
        {
            table.AddRow(Detail(server.Location), Detail(server.Sponsor), Detail(server.Url));
        }

        console.WriteLine("");
        console.Write(table);
    }

    private async Task DisplayServersWithLatency(List<IServer> servers, ISpeedTestService speedTestClient, CancellationToken cancellationToken)
    {
        var table = new Table()
            .Border(TableBorder.Square)
            .BorderStyle(theme.TableBorder)
            .AddColumn(new TableColumn("Location"))
            .AddColumn(new TableColumn("Sponsor"))
            .AddColumn(new TableColumn("Url"))
            .AddColumn(new TableColumn("Latency"));

        // Add the initial server list (without latency)
        foreach (var server in servers)
        {
            table.AddRow(Detail(server.Location), Detail(server.Sponsor), Detail(server.Url));
        }

        console.WriteLine("");
        WriteExitHint();
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
