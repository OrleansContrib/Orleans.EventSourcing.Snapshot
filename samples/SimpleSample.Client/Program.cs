using Orleans;
using Orleans.Configuration;
using SimpleSample.GrainInterfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace SimpleSample.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var clusterClient = BuildOrleansClient();

            var personId = Guid.NewGuid();
            var person = clusterClient.GetGrain<IPersonGrain>(personId);

            Console.WriteLine("Please input your nickname: ");
            var nickName = Console.ReadLine();

            await person.UpdateNickName(nickName);

            while (true)
            {
                Console.WriteLine("Type in what you want to say: ");
                var input = Console.ReadLine();

                await person.Say(input);

                var historySaids = await person.GetHistorySaids();

                Console.WriteLine("Your history saids: ");
                Console.WriteLine(string.Join(Environment.NewLine, historySaids));
                Console.WriteLine("--------------------");
                Console.WriteLine();
            }
        }

        private static IClusterClient BuildOrleansClient()
        {
            var endPoints = new List<IPEndPoint>();

            var ipAddress = IPAddress.Loopback;
            var port = 30000;

            endPoints.Add(new IPEndPoint(ipAddress, port));

            var client = new ClientBuilder()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "SimpleSample";
                    options.ServiceId = "SimpleSample";
                })
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(IPersonGrain).Assembly).WithReferences())
                .UseStaticClustering(endPoints.ToArray())
                .Build();

            client.Connect(async ex =>
            {
                Console.WriteLine("Connect orleans error", ex);

                await Task.Delay(1000);

                return true;
            });

            return client;
        }
    }
}
