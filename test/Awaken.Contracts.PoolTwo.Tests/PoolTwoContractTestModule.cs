using System.Collections.Generic;
using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using Awaken.Contracts.PoolTwo.ContractInitializationProviders;
using Awaken.Contracts.Token;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace Awaken.Contracts.PoolTwo
{
    [DependsOn(typeof(MainChainDAppContractTestModule))]
    public class PoolTwoContractTestModule : MainChainDAppContractTestModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IContractInitializationProvider, PoolTwoContractInitializationProvider>();
            context.Services.AddSingleton<IContractInitializationProvider, AwakenTokenInitializationProvider>();
        }

        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
            var contractCodeProvider = context.ServiceProvider.GetService<IContractCodeProvider>();
            var contractDllLocation = typeof(Awaken.Contracts.PoolTwoContract.PoolTwoContract).Assembly.Location;
            var contractCodes = new Dictionary<string, byte[]>(contractCodeProvider.Codes)
            {
                {
                    new PoolTwoContractInitializationProvider().ContractCodeName,
                    File.ReadAllBytes(contractDllLocation)
                },
                {
                    new AwakenTokenInitializationProvider().ContractCodeName,
                    File.ReadAllBytes(typeof(TokenContract).Assembly.Location)
                }
            };
            contractCodeProvider.Codes = contractCodes;
        }
    }
}