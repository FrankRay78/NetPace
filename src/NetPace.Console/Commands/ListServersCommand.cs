using NetPace.Console.Theming;
using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class ListServersCommand(IAnsiConsole console, ErrorOutput errorOutput, ISpeedTestService speedTestClient, Theme theme)
{
    // Network and discovery outcomes are data, not errors, as they are for a speed test: they are
    // reported and leave the exit code at 0. Only an operational fault, or a provider misconfigured
    // through its settings, propagates past this method to the top-level handler.
    public async Task<int> ExecuteAsync(ListServersCommandSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await ListAsync(settings, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Only this command's own token counts: an HttpClient timeout surfaces as a
            // cancellation too, and reporting that one as the user's choice would hide it.
        }
        catch (Exception e) when (ExitsNonZero(e))
        {
            throw;
        }
        catch (Exception e)
        {
            // The listing is the result; what went wrong fetching it is not, so it goes to the
            // error stream.
            errorOutput.WriteLine(theme, e.Message);
        }

        return 0;
    }

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

    // NetPace's own failure rather than the network's: an operational fault, or a provider the
    // settings have made unusable.
    private static bool ExitsNonZero(Exception exception) =>
        OperationalFault.Matches(exception) || exception is ArgumentException;

    private void WriteExitHint() =>
        console.WriteStyledLine(("Press ", Style.Plain), ("CTRL+C", theme.Hint), (" to exit.", Style.Plain));

    // Drawn as plain text rather than handed to the table as a string, because a string cell is
    // read as Spectre.Console markup: Speedtest publishes these values, and an unbalanced square
    // bracket in one would fail the whole listing.
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
                // The same path auto-selection uses, so the latency listed is the figure a run
                // ranks on - but each server is screened on its own, with no quicker sibling to end
                // the pass early, so a server listed with a latency may still be one a real run
                // would stop waiting for. Screened together, so the listing takes about one ceiling
                // rather than one per unreachable server, and filled in a row at a time, because
                // the table is not safe to change from two rows at once.
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
                        // Left blank: a dash would say the server did not answer, which is a
                        // verdict the listing never reached.
                        return;
                    }
                    catch (Exception e) when (e is not ArgumentException)
                    {
                        // Screening reports an unreachable candidate by throwing, so this is the
                        // ordinary path for one that did not answer. A provider misconfigured
                        // through its settings throws ArgumentException, which must reach the user
                        // rather than print a dash against every server in the list.
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
