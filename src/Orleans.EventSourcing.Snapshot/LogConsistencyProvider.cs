using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.LogConsistency;
using Orleans.Storage;
using System;

namespace Orleans.EventSourcing.Snapshot
{
    public class LogConsistencyProvider : ILogViewAdaptorFactory
    {
        private SnapshotStorageLogConsistencyOptions _options;
        private Func<SnapshotStrategyInfo, bool> _snapshotStrategy; 
        private IGrainEventStorage _eventStorage;

        public bool UsesStorageProvider => true;

        public LogConsistencyProvider(
            SnapshotStorageLogConsistencyOptions options, 
            IGrainEventStorage eventStorage,
            Func<SnapshotStrategyInfo, bool> snapshotStrategy) 
        {
            _options = options;
            _eventStorage = eventStorage;
            _snapshotStrategy = snapshotStrategy;
        }

        public ILogViewAdaptor<TLogView, TLogEntry> MakeLogViewAdaptor<TLogView, TLogEntry>(
            ILogViewAdaptorHost<TLogView, TLogEntry> hostGrain, 
            TLogView initialState, 
            string grainTypeName, 
            IGrainStorage grainStorage, 
            ILogConsistencyProtocolServices services)
            where TLogView : class, new()
            where TLogEntry : class
        {
            return new LogViewAdaptor<TLogView, TLogEntry>(
                hostGrain, 
                initialState, 
                grainStorage,
                grainTypeName,
                _snapshotStrategy,
                services, 
                _options.UseIndependentEventStorage, 
                _eventStorage);
        }
    }

    public static class LogConsistencyProviderFactory
    {
        public static ILogViewAdaptorFactory Create(IServiceProvider services, string name, Func<SnapshotStrategyInfo, bool> snapshotStrategy)
        {
            var optionsMonitor = services.GetRequiredService<IOptionsMonitor<SnapshotStorageLogConsistencyOptions>>();

            var options = optionsMonitor.Get(name);
            IGrainEventStorage eventStorage = null;

            if (options.UseIndependentEventStorage) 
            {
                eventStorage = services.GetRequiredService<IGrainEventStorage>();
            }

            return ActivatorUtilities.CreateInstance<LogConsistencyProvider>(services, options, eventStorage, snapshotStrategy);
        }
    }
}
