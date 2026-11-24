global using System.Text;

global using Microsoft.Extensions.DependencyInjection;
global using NetPace.Console.Commands;
global using NetPace.Core.Clients.Ookla;
global using NetPace.Core.Clients.Testing;
global using Spectre.Console;

// Resolves the Profile clash project-wide in favour of NetPace's enum; the two IAnsiConsole
// implementations that need Spectre's own Profile qualify it inline.
global using Profile = NetPace.Core.Profile;
