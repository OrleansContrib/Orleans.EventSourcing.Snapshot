using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing.Snapshot;
using Orleans.EventSourcing.Snapshot.Hosting;
using Orleans.Hosting;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;

namespace SimpleSample.Silo
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var host = BuildSilo();

            await host.RunAsync();
        }

        private static IHost BuildSilo()
        {
            var builder = new HostBuilder()
                .UseOrleans(o =>
                {
                    o.Services.AddMemoryCache(x =>
                    {
                        x.SizeLimit = 100;
                    });

                    o.Services.AddSingleton<BlobServiceClient>(_ => new BlobServiceClient("UseDevelopmentStorage=true"));

                    o.UseLocalhostClustering()
                        .ConfigureLogging(logging =>
                        {
                            logging.SetMinimumLevel(LogLevel.Debug).AddConsole();
                        })
                        .AddAzureBlobGrainStorageAsDefault(o =>
                        {
                            o.ConfigureBlobServiceClient("UseDevelopmentStorage=true");
                        })
                        .AddSnapshotStorageBasedLogConsistencyProviderAsDefault((op, name) =>
                        {
                            // Take snapshot every five events
                            op.SnapshotStrategy = strategyInfo =>
                                strategyInfo.CurrentConfirmedVersion - strategyInfo.SnapshotVersion >= 5;
                            op.UseIndependentEventStorage = true;
                            // Should configure event storage when set UseIndependentEventStorage true
                            op.ConfigureIndependentEventStorage = (services, name) =>
                            {
                                services.AddSingleton<IGrainEventStorage, BlobStorage>();
                            };
                        })
                        // .AddSnapshotStorageBasedLogConsistencyProviderAsDefault((op, name) => 
                        // {
                        //     // Take snapshot every five events
                        //     op.SnapshotStrategy = strategyInfo => strategyInfo.CurrentConfirmedVersion - strategyInfo.SnapshotVersion >= 5;
                        //     op.UseIndependentEventStorage = true;
                        //     // Should configure event storage when set UseIndependentEventStorage true
                        //     op.ConfigureIndependentEventStorage = (services, name) =>
                        //     {
                        //         var eventStoreConnectionString = "ConnectTo=tcp://admin:changeit@localhost:1113; HeartBeatTimeout=500";
                        //         var eventStoreConnection = EventStoreConnection.Create(eventStoreConnectionString);
                        //         eventStoreConnection.ConnectAsync().Wait();
                        //
                        //         services.AddSingleton(eventStoreConnection);
                        //         services.AddSingleton<IGrainEventStorage, EventStoreGrainEventStorage>();
                        //     };
                        // })
                        ;
                });
                

            return builder.Build();
        }
    }
}
