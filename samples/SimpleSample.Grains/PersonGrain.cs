using Orleans.EventSourcing;
using SimpleSample.GrainInterfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SimpleSample.Grains
{
    public class PersonGrain : JournaledGrain<PersonState>, IPersonGrain
    {
        public Task Say(string content)
        {
            RaiseEvent(new PersonSaidEvent { Said = content });

            return Task.CompletedTask;
        }

        public Task<List<string>> GetHistorySaids()
        {
            return Task.FromResult(TentativeState.HistorySaids);
        }

        public Task UpdateNickName(string newNickName)
        {
            RaiseEvent(new PersonNickNameUpdatedEvent { NewNickName = newNickName });

            return Task.CompletedTask;
        }

        public Task<string> GetNickName()
        {
            return Task.FromResult(TentativeState.NickName);
        }
    }

    public class PersonState 
    {
        public string NickName { get; private set; } = "anonymous";

        public List<string> HistorySaids { get; private set; } = new List<string>();

        public void Apply(PersonSaidEvent @event) 
        {
            HistorySaids.Add(@event.Said);
        }
        
        public void Apply(PersonNickNameUpdatedEvent @event) 
        {
            NickName = @event.NewNickName;
        }
    }

    public class PersonSaidEvent 
    {
        public string Said { get; set; }
    }

    public class PersonNickNameUpdatedEvent
    {
        public string NewNickName { get; set; }
    }
}
