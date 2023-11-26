using Orleans.Runtime;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orleans.EventSourcing.Snapshot
{
    public interface IGrainEventStorage
    {
        Task SaveEvents<TEvent>(string grainTypeName, GrainId grainReference, IEnumerable<TEvent> events, int expectedVersion);

        Task<List<TEvent>> ReadEvents<TEvent>(string grainTypeName, GrainId grainReference, int start, int count);

        Task<int> EventsCount(string grainTypeName, GrainId grainReference);
    }
}
