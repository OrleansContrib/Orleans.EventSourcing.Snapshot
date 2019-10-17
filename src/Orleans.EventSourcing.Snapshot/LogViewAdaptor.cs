using Force.DeepCloner;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing.Common;
using Orleans.LogConsistency;
using Orleans.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Orleans.EventSourcing.Snapshot
{
    public class LogViewAdaptor<TLogView, TLogEntry> : PrimaryBasedLogViewAdaptor<TLogView, TLogEntry, SubmissionEntry<TLogEntry>>
        where TLogView : class, new()
        where TLogEntry : class
    {
        private const int _maxEntriesInNotifications = 200;

        private IGrainStorage _grainStorage;
        private string _grainTypeName;
        private Func<SnapshotStrategyInfo, bool> _snapshotStrategy;
        private bool _useIndependentEventStorage;
        private IGrainEventStorage _eventStorage;

        private SnapshotStateWithMetaDataAndETag<TLogView, TLogEntry> _snapshotState;
        private SortedList<long, UpdateNotificationMessage> _notifications = new SortedList<long, UpdateNotificationMessage>();
        private TLogView _confirmedViewInternal;
        private int _confirmedVersionInternal;

        public LogViewAdaptor(
            ILogViewAdaptorHost<TLogView, TLogEntry> host, 
            TLogView initialState,
            IGrainStorage grainStorage,
            string grainTypeName,
            Func<SnapshotStrategyInfo, bool> snapshotStrategy,
            ILogConsistencyProtocolServices services,
            bool useIndependentEventStorage,
            IGrainEventStorage eventStorage)
         : base(host, initialState, services)
        {
            _grainStorage = grainStorage;
            _grainTypeName = grainTypeName;
            _snapshotStrategy = snapshotStrategy;
            _useIndependentEventStorage = useIndependentEventStorage;

            if (useIndependentEventStorage) 
            {
                _eventStorage = eventStorage 
                    ?? throw new ArgumentNullException(nameof(eventStorage), 
                        "Must set eventStorage when useIndependentEventStorage is true");
            }
        }

        public override async Task<IReadOnlyList<TLogEntry>> RetrieveLogSegment(int fromVersion, int toVersion)
        {
            IReadOnlyList<TLogEntry> segment;
            int cachedLogCount = _snapshotState.StateAndMetaData.Log.Count;

            if (fromVersion >= toVersion)
            {
                segment = new List<TLogEntry>();
            }
            else if (fromVersion >= cachedLogCount)
            {
                segment = await _eventStorage.RetrieveEvents<TLogEntry>(
                    _grainTypeName, Services.GrainReference, fromVersion, toVersion);
            }
            else if (toVersion < cachedLogCount)
            {
                segment = _snapshotState.StateAndMetaData.Log.GetRange(fromVersion, (toVersion - fromVersion));
            }
            else
            {
                var segmentPart1 = _snapshotState.StateAndMetaData.Log.GetRange(fromVersion, (cachedLogCount - fromVersion));
                var segmentPart2 = await _eventStorage.RetrieveEvents<TLogEntry>(
                    _grainTypeName, Services.GrainReference, cachedLogCount, toVersion);

                segmentPart1.AddRange(segmentPart2);
                segment = segmentPart1;
            }

            return segment;
        }

        protected override TLogView LastConfirmedView()
        {
            return _confirmedViewInternal;
        }

        protected override int GetConfirmedVersion()
        {
            return _confirmedVersionInternal;
        }

        protected override void InitializeConfirmedView(TLogView initialstate)
        {
            _snapshotState = new SnapshotStateWithMetaDataAndETag<TLogView, TLogEntry>();
            _confirmedViewInternal = initialstate;
            _confirmedVersionInternal = 0;
        }

        protected override SubmissionEntry<TLogEntry> MakeSubmissionEntry(TLogEntry entry)
        {
            return new SubmissionEntry<TLogEntry>() { Entry = entry };
        }

        protected override async Task ReadAsync()
        {
            enter_operation("ReadAsync");

            while (true)
            {
                try
                {
                    await _grainStorage.ReadStateAsync(_grainTypeName, Services.GrainReference, _snapshotState);

                    Services.Log(LogLevel.Debug, "read success {0}", _snapshotState);

                    if (_confirmedVersionInternal < _snapshotState.StateAndMetaData.SnapshotVersion)
                    {
                        _confirmedVersionInternal = _snapshotState.StateAndMetaData.SnapshotVersion;
                        _confirmedViewInternal = _snapshotState.StateAndMetaData.Snapshot;
                    }

                    var logs = await RetrieveLogSegment(
                        _confirmedVersionInternal,
                        _snapshotState.StateAndMetaData.GlobalVersion);

                    Services.Log(LogLevel.Debug, "read success {0}", logs);

                    UpdateConfirmedView(logs);

                    LastPrimaryIssue.Resolve(Host, Services);

                    break; // successful
                }
                catch (Exception e)
                {
                    LastPrimaryIssue.Record(new ReadFromSnapshotStorageFailed() { Exception = e }, Host, Services);
                }

                Services.Log(LogLevel.Debug, "read failed {0}", LastPrimaryIssue);

                await LastPrimaryIssue.DelayBeforeRetry();
            }

            exit_operation("ReadAsync");
        }

        protected override async Task<int> WriteAsync()
        {
            enter_operation("WriteAsync");

            var updates = GetCurrentBatchOfUpdates();
            bool batchSuccessfullyWritten = false;
            bool logsSuccessfullySaved = false;

            var writebit = _snapshotState.StateAndMetaData.FlipBit(Services.MyClusterId);
            if (!_useIndependentEventStorage) 
            {
                foreach (var x in updates)
                {
                    _snapshotState.StateAndMetaData.Log.Add(x.Entry);
                }

                logsSuccessfullySaved = true;
            }
            else 
            {
                try
                {
                    await _eventStorage.Save(_grainTypeName, Services.GrainReference, updates.Select(x => x.Entry));
                    logsSuccessfullySaved = true;
                }
                catch (Exception e) 
                {
                    LastPrimaryIssue.Record(new UpdateEventStorageFailed() { Exception = e }, Host, Services);
                }
            }

            if (logsSuccessfullySaved)
            {
                try
                {
                    if (_snapshotStrategy(GetSnapshotStrategyInfo())) 
                    {
                        _snapshotState.StateAndMetaData.SnapshotVersion = _confirmedVersionInternal;
                        _snapshotState.StateAndMetaData.SnapshotUpdatedTime = DateTime.Now;
                        _snapshotState.StateAndMetaData.Snapshot = _confirmedViewInternal.DeepClone();
                    }

                    await _grainStorage.WriteStateAsync(_grainTypeName, Services.GrainReference, _snapshotState);

                    batchSuccessfullyWritten = true;

                    Services.Log(LogLevel.Debug, "write ({0} updates) success {1}", updates.Length, _snapshotState);

                    UpdateConfirmedView(updates.Select(u => u.Entry));

                    LastPrimaryIssue.Resolve(Host, Services);
                }
                catch (Exception e)
                {
                    LastPrimaryIssue.Record(new UpdateSnapshotStorageFailed() { Exception = e }, Host, Services);
                }
            }

            if (!batchSuccessfullyWritten)
            {
                Services.Log(LogLevel.Debug, "write apparently failed {0}", LastPrimaryIssue);

                while (true)
                {
                    await LastPrimaryIssue.DelayBeforeRetry();

                    try
                    {
                        await _grainStorage.ReadStateAsync(_grainTypeName, Services.GrainReference, _snapshotState);

                        Services.Log(LogLevel.Debug, "read success {0}", _snapshotState);

                        if (_confirmedVersionInternal < _snapshotState.StateAndMetaData.SnapshotVersion)
                        {
                            _confirmedVersionInternal = _snapshotState.StateAndMetaData.SnapshotVersion;
                            _confirmedViewInternal = _snapshotState.StateAndMetaData.Snapshot;
                        }

                        var logs = await RetrieveLogSegment(
                            _confirmedVersionInternal,
                            _snapshotState.StateAndMetaData.GlobalVersion);

                        Services.Log(LogLevel.Debug, "read success {0}", logs);

                        UpdateConfirmedView(logs);

                        LastPrimaryIssue.Resolve(Host, Services);

                        break;
                    }
                    catch (Exception e)
                    {
                        LastPrimaryIssue.Record(new ReadFromSnapshotStorageFailed() { Exception = e }, Host, Services);
                    }

                    Services.Log(LogLevel.Debug, "read failed {0}", LastPrimaryIssue);
                }

                if (writebit == _snapshotState.StateAndMetaData.GetBit(Services.MyClusterId))
                {
                    Services.Log(LogLevel.Debug, "last write ({0} updates) was actually a success {1}", updates.Length, _snapshotState);

                    batchSuccessfullyWritten = true;
                }
            }

            if (batchSuccessfullyWritten)
            {
                BroadcastNotification(new UpdateNotificationMessage()
                {
                    Version = _snapshotState.StateAndMetaData.GlobalVersion,
                    Updates = updates.Select(se => se.Entry).ToList(),
                    Origin = Services.MyClusterId,
                    ETag = _snapshotState.ETag
                });
            }

            exit_operation("WriteAsync");

            if (!batchSuccessfullyWritten)
                return 0;

            return updates.Length;
        }

        protected override INotificationMessage Merge(INotificationMessage earlierMessage, INotificationMessage laterMessage)
        {
            var earlier = earlierMessage as UpdateNotificationMessage;
            var later = laterMessage as UpdateNotificationMessage;

            if (earlier != null
                && later != null
                && earlier.Origin == later.Origin
                && earlier.Version + later.Updates.Count == later.Version
                && earlier.Updates.Count + later.Updates.Count < _maxEntriesInNotifications)

                return new UpdateNotificationMessage()
                {
                    Version = later.Version,
                    Origin = later.Origin,
                    Updates = earlier.Updates.Concat(later.Updates).ToList(),
                    ETag = later.ETag
                };

            else
                return base.Merge(earlierMessage, laterMessage); // keep only the version number
        }

        protected override void OnNotificationReceived(INotificationMessage payload)
        {
            var um = payload as UpdateNotificationMessage;
            if (um != null)
                _notifications.Add(um.Version - um.Updates.Count, um);
            else
                base.OnNotificationReceived(payload);
        }

        protected override void ProcessNotifications()
        {
            while (_notifications.Count > 0 && _notifications.ElementAt(0).Key < _snapshotState.StateAndMetaData.GlobalVersion)
            {
                Services.Log(LogLevel.Debug, "discarding notification {0}", _notifications.ElementAt(0).Value);
                _notifications.RemoveAt(0);
            }

            while (_notifications.Count > 0 && _notifications.ElementAt(0).Key == _snapshotState.StateAndMetaData.GlobalVersion)
            {
                var updateNotification = _notifications.ElementAt(0).Value;
                _notifications.RemoveAt(0);

                if (!_useIndependentEventStorage)
                {
                    foreach (var u in updateNotification.Updates)
                    {
                        _snapshotState.StateAndMetaData.Log.Add(u);
                    }
                }

                _snapshotState.StateAndMetaData.FlipBit(updateNotification.Origin);

                _snapshotState.ETag = updateNotification.ETag;

                UpdateConfirmedView(updateNotification.Updates);

                Services.Log(LogLevel.Debug, "notification success ({0} updates) {1}", updateNotification.Updates.Count, _snapshotState);
            }

            Services.Log(LogLevel.Trace, "unprocessed notifications in queue: {0}", _notifications.Count);

            base.ProcessNotifications();

        }

        [Serializable]
        public class ReadFromSnapshotStorageFailed : PrimaryOperationFailed
        {
            public override string ToString()
            {
                return $"read from snapshot storage failed: caught {Exception.GetType().Name}: {Exception.Message}";
            }
        }

        [Serializable]
        public class UpdateSnapshotStorageFailed : PrimaryOperationFailed
        {
            public override string ToString()
            {
                return $"write to snapshot storage failed: caught {Exception.GetType().Name}: {Exception.Message}";
            }
        }

        [Serializable]
        public class ReadFromEventStorageFailed : PrimaryOperationFailed
        {
            public override string ToString()
            {
                return $"read from event storage failed: caught {Exception.GetType().Name}: {Exception.Message}";
            }
        }

        [Serializable]
        public class UpdateEventStorageFailed : PrimaryOperationFailed
        {
            public override string ToString()
            {
                return $"write to event storage failed: caught {Exception.GetType().Name}: {Exception.Message}";
            }
        }

        [Serializable]
        protected class UpdateNotificationMessage : INotificationMessage
        {
            public int Version { get; set; }

            public string Origin { get; set; }

            public List<TLogEntry> Updates { get; set; }

            public string ETag { get; set; }

            public override string ToString()
            {
                return string.Format("v{0} ({1} updates by {2}) etag={3}", Version, Updates.Count, Origin, ETag);
            }
        }

        private void UpdateConfirmedView(IEnumerable<TLogEntry> pendingLogs)
        {
            foreach (var log in pendingLogs)
            {
                try
                {
                    Host.UpdateView(_confirmedViewInternal, log);
                }
                catch (Exception e)
                {
                    Services.CaughtUserCodeException("UpdateView", nameof(UpdateConfirmedView), e);
                }
            }

            _confirmedVersionInternal += pendingLogs.Count();
        }

        private SnapshotStrategyInfo GetSnapshotStrategyInfo() 
        {
            return new SnapshotStrategyInfo
            {
                CurrentConfirmedVersion = _confirmedVersionInternal,
                SnapshotVersion = _snapshotState.StateAndMetaData.SnapshotVersion,
                SnapshotUpdatedTime = _snapshotState.StateAndMetaData.SnapshotUpdatedTime
            };
        }

#if DEBUG
        private bool operation_in_progress;
#endif

        [Conditional("DEBUG")]
        private void enter_operation(string name)
        {
#if DEBUG
            Services.Log(LogLevel.Trace, "/-- enter {0}", name);
            Debug.Assert(!operation_in_progress);
            operation_in_progress = true;
#endif
        }

        [Conditional("DEBUG")]
        private void exit_operation(string name)
        {
#if DEBUG
            Services.Log(LogLevel.Trace, "\\-- exit {0}", name);
            Debug.Assert(operation_in_progress);
            operation_in_progress = false;
#endif
        }
    }
}
