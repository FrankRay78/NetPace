using NetPace.Console.Theming;
using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class ListServersCommand(IAnsiConsole console, ErrorOutput errorOutput, ISpeedTestService speedTestClient, Theme theme)
{
    /// <summary>
    /// Executes the list servers command, displaying available speed test servers.
    /// </summary>
    /// <remarks>
    /// A network outcome is data, not an error, and leaves the exit code at <c>0</c>. Only an
    /// operational fault or a misconfigured provider propagates for a non-zero exit.
    /// </remarks>
    public async Task<int> ExecuteAsync(ListServersCommandSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await ListAsync(settings, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Only this command's own token counts: an HttpClient timeout surfaces as a
            // cancellation too, and reporting that as the user's choice would hide it.
        }
        catch (Exception e) when (ExitsNonZero(e))
        {
            throw;
        }
        catch (Exception e)
        {
            // The listing itself is the result; what went wrong while fetching it is not, so it
            // goes to the error stream.
            errorOutput.WriteLine(theme, e.Message);
        }

        return 0;
    }

    /// <summary>
    /// Lists the servers in whichever form the settings ask for: the fastest one, the plain list,
    /// or the list with each server's latency.
    /// </summary>
    private async Task ListAsync(ListServersCommandSettings settings, CancellationToken cancellationToken)
    {
        var servers = await speedTestClient.GetServersAsync(cancellationToken);

        var serversList = servers.OrderBy(server => server.Location).ToList();

        if (serversList.Count == 0)
        {
            throw new NoServersAvailableException();
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
    }

    /// <summary>
    /// Whether a failure is NetPace's own rather than the network's, and so must exit non-zero: an
    /// operational fault, or a provider the settings have made unusable.
    /// </summary>
    private static bool ExitsNonZero(Exception exception) =>
        OperationalFault.Matches(exception) || exception is ArgumentException;

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
    /// Drawn as plain text rather than handed to the table as strings, which Spectre.Console reads
    /// as markup: an unbalanced square bracket in a published value would fail the whole listing.
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
                // Screened through the path auto-selection uses, so a listed latency is the figure
                // a run ranks on, but each gets the whole ceiling with no quicker sibling to end it.
                var tableLock = new object();

                async Task ScreenRowAsync(int row)
                {
                    string latency;

                    try
                    {
                        var latencyResult = await speedTestClient.GetFastestServerByLatencyAsync([servers[row]], cancellationToken);

                        latency = $"{latencyResult.LatencyMilliseconds}ms";
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Left blank: a dash would say the server did not answer, a verdict the
                        // listing never reached.
                        return;
                    }
                    catch (Exception e) when (e is not ArgumentException)
                    {
                        // The ordinary path for a server that did not answer. ArgumentException is
                        // excluded: a misconfigured provider must reach the user, not dash every row.
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
