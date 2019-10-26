using EventStore.ClientAPI;
using JsonNet.PrivateSettersContractResolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.EventSourcing.Snapshot;
using Orleans.EventSourcing.Snapshot.Hosting;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;
using SimpleSample.Grains;
using System;
using System.Threading.Tasks;

namespace SimpleSample.Silo
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var host = BuildSilo();

            await host.StartAsync();

            Console.ReadLine();
        }

        private static ISiloHost BuildSilo()
        {
            var builder = new SiloHostBuilder()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "SimpleSample";
                    options.ServiceId = "SimpleSample";
                })
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(PersonGrain).Assembly).WithReferences())
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Debug).AddConsole();
                })
                .AddMongoDBGrainStorageAsDefault((MongoDBGrainStorageOptions op) =>
                {
                    op.CollectionPrefix = "GrainStorage";
                    op.ConnectionString = "mongodb://127.0.0.1:27017";
                    op.DatabaseName = "SimpleSampleOrelans";

                    op.ConfigureJsonSerializerSettings = jsonSettings =>
                    {
                        jsonSettings.ContractResolver = new PrivateSetterContractResolver();
                    };
                })
                .AddSnapshotStorageBasedLogConsistencyProviderAsDefault((op, name) => 
                {
                    // Take snapshot every five events
                    op.SnapshotStrategy = strategyInfo => strategyInfo.CurrentConfirmedVersion - strategyInfo.SnapshotVersion >= 5;
                    op.UseIndependentEventStorage = true;
                    // Should configure event storage when set UseIndependentEventStorage true
                    op.ConfigureIndependentEventStorage = (services, name) =>
                    {
                        var eventStoreConnectionString = "ConnectTo=tcp://admin:changeit@127.0.0.1:1113; HeartBeatTimeout=500";
                        var eventStoreConnection = EventStoreConnection.Create(eventStoreConnectionString);
                        eventStoreConnection.ConnectAsync().Wait();

                        services.AddSingleton(eventStoreConnection);
                        services.AddSingleton<IGrainEventStorage, EventStoreGrainEventStorage>();
                    };
                });

            return builder.Build();
        }
    }
}
