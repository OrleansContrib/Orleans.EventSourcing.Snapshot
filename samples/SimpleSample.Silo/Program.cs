using EventStore.ClientAPI;
using JsonNet.PrivateSettersContractResolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing.Snapshot;
using Orleans.EventSourcing.Snapshot.Hosting;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;
using System;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Orleans.EventSourcing;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using Orleans.Runtime;
using Orleans.Runtime.LogConsistency;
using Orleans.Serialization;
using SimpleSample.GrainInterfaces.State;

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
                        .UseMongoDBClient("mongodb://localhost:27017")
                        .AddMongoDBGrainStorageAsDefault((MongoDBGrainStorageOptions op) =>
                        {
                            op.CollectionPrefix = "GrainStorage";
                            op.DatabaseName = "SimpleSampleOreleans";
                        })
                        .AddSnapshotStorageBasedLogConsistencyProviderAsDefault((op, name) => 
                        {
                            // Take snapshot every five events
                            op.SnapshotStrategy = strategyInfo => strategyInfo.CurrentConfirmedVersion - strategyInfo.SnapshotVersion >= 5;
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
                        .Services.TryAddSingleton<Factory<IGrainContext, ILogConsistencyProtocolServices>>(serviceProvider =>
                        {
                            var factory = ActivatorUtilities.CreateFactory(typeof(ProtocolServices), new[] { typeof(IGrainContext) });
                            return arg1 => (ILogConsistencyProtocolServices)factory(serviceProvider, new object[] { arg1 });
                        });
                });
                

            return builder.Build();
        }
    }
}
