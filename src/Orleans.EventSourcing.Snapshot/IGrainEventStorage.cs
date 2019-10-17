using Orleans.Runtime;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orleans.EventSourcing.Snapshot
{
    public interface IGrainEventStorage
    {
        Task Save<TEvent>(string grainTypeName, GrainReference grainReference, IEnumerable<TEvent> events);

        Task<List<TEvent>> RetrieveEvents<TEvent>(string grainTypeName, GrainReference grainReference, int fromVersion, int toVersion);
    }
}
