using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.LogConsistency;
using Orleans.Providers;
using Orleans.Runtime;
using System;

namespace Orleans.EventSourcing.Snapshot.Hosting
{
    public static class SnapshotStorageSiloBuilderExtensions
    {
        public static ISiloHostBuilder AddSnapshotStorageBasedLogConsistencyProviderAsDefault(
            this ISiloHostBuilder builder,
            Action<SnapshotStorageOptions, string> configureSnapshotStorageOptions)
        {
            return builder.AddSnapshotStorageBasedLogConsistencyProvider(
                configureSnapshotStorageOptions, ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME);
        }

        public static ISiloHostBuilder AddSnapshotStorageBasedLogConsistencyProvider(
            this ISiloHostBuilder builder,
            Action<SnapshotStorageOptions, string> configureSnapshotStorageOptions,
            string name = "SnapshotStorage")
        {
            var snapshotStorageOptions = new SnapshotStorageOptions();
            configureSnapshotStorageOptions?.Invoke(snapshotStorageOptions, name);

            return builder.ConfigureServices(services => 
            {
                services.AddSnapshotStorageLogConsistencyOptions(snapshotStorageOptions.UseIndependentEventStorage, name);

                if (snapshotStorageOptions.UseIndependentEventStorage) 
                {
                    snapshotStorageOptions.ConfigureIndependentEventStorage?.Invoke(services, name);
                }

                services.AddSnapshotStorageBasedLogConsistencyProvider(snapshotStorageOptions.SnapshotStrategy, name);   
            });
        }

        internal static IServiceCollection AddSnapshotStorageBasedLogConsistencyProvider(
            this IServiceCollection services,
            Func<SnapshotStrategyInfo, bool> snapshotStrategy,
            string name)
        {
            services
                .AddSingletonNamedService(name, (sp, n) => LogConsistencyProviderFactory.Create(sp, n, snapshotStrategy))
                .TryAddSingleton(sp => sp.GetServiceByName<ILogViewAdaptorFactory>(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME));

            return services;
        }

        internal static IServiceCollection AddSnapshotStorageLogConsistencyOptions(
            this IServiceCollection services, 
            bool useIndependentEventStorage, 
            string name) 
        {
            services.AddOptions<SnapshotStorageLogConsistencyOptions>(name)
               .Configure(options => options.UseIndependentEventStorage = useIndependentEventStorage);

            services.ConfigureNamedOptionForLogging<SnapshotStorageLogConsistencyOptions>(name);

            return services;
        }
    }
}
