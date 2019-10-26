using Orleans;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SimpleSample.GrainInterfaces
{
    public interface IPersonGrain : IGrainWithGuidKey
    {
        Task Say(string content);

        Task<List<string>> GetHistorySaids();

        Task UpdateNickName(string newNickName);

        Task<string> GetNickName();
    }
}
