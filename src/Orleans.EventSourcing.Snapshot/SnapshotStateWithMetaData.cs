using Orleans.EventSourcing.Common;
using System;
using System.Collections.Generic;

namespace Orleans.EventSourcing.Snapshot
{
    [Serializable]
    public class SnapshotStateWithMetaDataAndETag<TState, TEntry> : IGrainState
        where TState : class, new()
        where TEntry : class
    {
        public SnapshotStateWithMetaData<TState, TEntry> StateAndMetaData { get; set; }

        public string ETag { get; set; }
		
        public bool RecordExists
        {
            get => StateAndMetaData.GlobalVersion > 0;
            set => _ = value;
        }

        public Type Type => typeof(SnapshotStateWithMetaData<TState, TEntry>);

        object IGrainState.State
        {
            get => StateAndMetaData;
            set => StateAndMetaData = (SnapshotStateWithMetaData<TState, TEntry>)value;
        }

        public SnapshotStateWithMetaDataAndETag()
        {
            StateAndMetaData = new SnapshotStateWithMetaData<TState, TEntry>();
        }

        public override string ToString()
        {
            return string.Format(
                "v{0} Flags={1} ETag={2} Snapshot={3} Log={4}",
                StateAndMetaData.GlobalVersion,
                StateAndMetaData.WriteVector,
                ETag,
                StateAndMetaData.Snapshot,
                StateAndMetaData.Log);
        }
    }

    public interface ISnapshotMetaData
    {
    	public int SnapshotVersion { get; }
	public int GlobalVersion { get; }
	public DateTime? SnapshotUpdatedTime { get; }
    }

    [Serializable]
    public class SnapshotStateWithMetaData<TState, TEntry> : ISnapshotMetaData
        where TState : class, new()
        where TEntry : class
    {
        public List<TEntry> Log { get; set; }

        public TState Snapshot { get; set; }

        public DateTime? SnapshotUpdatedTime { get; set; }

        public int GlobalVersion { get; set; }

        public int SnapshotVersion { get; set; }

        public string WriteVector { get; set; }

        public SnapshotStateWithMetaData()
        {
            Log = new List<TEntry>();
            Snapshot = new TState();
            SnapshotVersion = 0;
            WriteVector = "";
        }

        public bool GetBit(string Replica)
        {
            return StringEncodedWriteVector.GetBit(WriteVector, Replica);
        }

        public bool FlipBit(string Replica)
        {
            var str = WriteVector;
            var rval = StringEncodedWriteVector.FlipBit(ref str, Replica);
            WriteVector = str;
            return rval;
        }
    }
}
