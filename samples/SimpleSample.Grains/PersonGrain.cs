using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans.EventSourcing;
using SimpleSample.GrainInterfaces;
using SimpleSample.GrainInterfaces.State;

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
}
