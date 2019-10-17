using System;

namespace Orleans.EventSourcing.Snapshot
{
    public struct SnapshotStrategyInfo
    {
        public int CurrentConfirmedVersion { get; set; }

        public int SnapshotVersion { get; set; }

        public DateTime? SnapshotUpdatedTime { get; set; }
    }
}
