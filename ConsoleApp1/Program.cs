using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Opc.Ua;

using System.Globalization;

namespace ControlsServer;
internal sealed class Program
{
    private static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                _ = services.AddHostedService<ConsoleHostedService>();
            })
            .RunConsoleAsync();
    }
}

internal sealed class ConsoleHostedService : IHostedService
{
    private readonly ILogger _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IConfiguration _configuration;

    public ConsoleHostedService(
        ILogger<ConsoleHostedService> logger,
        IConfiguration configuration,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Starting with arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");
        // Get the value as a string
        _ = _configuration["MyFirstArg"];
        _ = _appLifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    _ = await doWork(_logger);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception!");
                }
                finally
                {
                    // Stop the application once the work is done
                    _appLifetime.StopApplication();
                }
            });
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    private async Task<int> doWork(ILogger logger)
    {

        // TODO: replace with logger
        TextWriter output = Console.Out;
        // Default  options
        bool autoAccept = false;
        bool renewCertificate = false;
        string? password = null;

        string message = $"OPC UA library: {Utils.GetAssemblyBuildNumber()} @ {Utils.GetAssemblyTimestamp().ToString("G", CultureInfo.InvariantCulture)} -- {Utils.GetAssemblySoftwareVersion()}";
        logger.LogInformation(message);

        // The application name and config file names
        string applicationName = nameof(ControlsServer);
        string configSectionName = nameof(ControlsServer);
        try
        {
            // TODO: do we need to configure-await to false ?
            // TODO: actualServer implementation class
            // create the UA server
            UAServer<ControlsServer> server = new(output)
            {
                AutoAccept = autoAccept,
                Password = password
            };

            // load the server configuration, validate certificates
            logger.LogInformation($"Loading configuration from {configSectionName}.{applicationName}");
            await server.LoadAsync(applicationName, configSectionName).ConfigureAwait(false);


            // TODO: setup the logging
            // ConsoleUtils.ConfigureLogging(server.Configuration, applicationName, logConsole, LogLevel.Information);

            // check or renew the certificate
            logger.LogInformation("Check the certificate.");
            await server.CheckCertificateAsync(renewCertificate);
            //.ConfigureAwait(false);

            // TODO: IDK Create and add the node managers
            //server.Create(Servers.Utils.NodeManagerFactories);

            // start the server
            logger.LogInformation("Start the server.");
            await server.StartAsync();
            //.ConfigureAwait(false);

            logger.LogInformation("Server started. Press Ctrl-C to exit...");

            // stop server. May have to wait for clients to disconnect.
            logger.LogInformation("Server stopped. Waiting for exit...");
            await server.StopAsync();
            //.ConfigureAwait(false);

            return 0;
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return 1;
        }
    }
}

