using Orleans;
using Orleans.Configuration;
using SimpleSample.GrainInterfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

namespace SimpleSample.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = await BuildHost();
            await host.RunAsync();
        }

        private static async Task<IHost> BuildHost()
        {
            var host = new HostBuilder()
                .UseOrleansClient(o =>
                {
                    o.UseLocalhostClustering();
                    o.Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "default";
                        options.ServiceId = "http";
                    });
                })
                .ConfigureServices((host, services) =>
                {
                    services.AddHostedService<BgService>();
                })
                .UseConsoleLifetime();
            return host.Build();
        }
    }

    internal class BgService : BackgroundService
    {
        private readonly IGrainFactory _grainFactory;

        public BgService(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var personId = Guid.Empty;
            var person = _grainFactory.GetGrain<IPersonGrain>(personId);
            
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
    }
}
