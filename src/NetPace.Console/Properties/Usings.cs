global using System.Text;

global using Microsoft.Extensions.DependencyInjection;
global using NetPace.Console.Commands;
global using NetPace.Core.Clients.Ookla;
global using NetPace.Core.Clients.Testing;
global using Spectre.Console;

// Resolve the Profile clash project-wide in favour of our enum. The two files that need
// Spectre's terminal-capability Profile (FileConsole, CompositeAnsiConsole — both
// IAnsiConsole implementations) use the fully qualified name inline.
global using Profile = NetPace.Core.Profile;
