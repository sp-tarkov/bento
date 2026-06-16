using Bento;
using Spectre.Console.Cli;

// Builds the Spectre CLI host with BuildCommand as the default command and runs it with process arguments.
var app = new CommandApp<BuildCommand>();
app.Configure(config =>
{
    config.SetApplicationName("bento");
});
return await app.RunAsync(args);
