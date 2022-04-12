using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Types;
using Awaken.Contracts.PoolTwoContract;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Threading;

namespace Awaken.Contracts.PoolTwo
{
    public partial class PoolTwoContractTests
    {
        public Address Owner;
        public Address Tom;
        
        public ECKeyPair OwnerPair;
        public ECKeyPair TomPair;

        public Address PoolOneMock;
        public ECKeyPair PoolOneMockPair;
        
        
        // constants
        private const string DISTRIBUTETOKEN = "AWAKEN";
        private const string LPTOKEN_01 = "AAAE";
        private const string LPTOKEN_02 = "AAAB";
        private const long HalvingPeriod = 500;
        
        
        private async Task<PoolTwoContractContainer.PoolTwoContractStub> Initialize()
        {
            OwnerPair = SampleAccount.Accounts.First().KeyPair;
            Owner = Address.FromPublicKey(OwnerPair.PublicKey);
            TomPair = SampleAccount.Accounts[1].KeyPair;
            Tom = Address.FromPublicKey(TomPair.PublicKey);
            PoolOneMockPair = SampleAccount.Accounts[2].KeyPair;
            PoolOneMock = Address.FromPublicKey(PoolOneMockPair.PublicKey);
            
            var stub = GetPoolTwoContractStub(OwnerPair);
            await stub.Initialize.SendAsync(new InitializeInput
            {
                Owner = Owner,
                DistributeToken = DISTRIBUTETOKEN,
                HalvingPeriod = HalvingPeriod,
                StartBlock = 50,
                TotalReward = 9375000,
                DistributeTokenPerBlock = 10000,
                AwakenTokenContract = LpTokenContractAddress,
                RedepositStartBlock = 300
            });
            
            await InitLpTokenContract();
            
            await CreateToken();
            return stub;
        }
        
        private async Task InitLpTokenContract()
        {
            var lpTokenContractStub = GetLpTokenContractStub(OwnerPair);
            await lpTokenContractStub.Initialize.SendAsync(new Token.InitializeInput
            {
                Owner = Owner
            });
        }
        
        private async Task CreateToken()
        {
            var tokenStub = GetTokenContractStub(OwnerPair);
            await tokenStub.Create.SendAsync(new CreateInput
            {
                Decimals = 5,
                Symbol = DISTRIBUTETOKEN,
                Issuer = Owner,
                IsBurnable = true,
                TokenName = DISTRIBUTETOKEN,
                TotalSupply = 1000000000000
            });

            await tokenStub.Issue.SendAsync(new IssueInput
            {
                Amount = 1000000000000,
                Symbol = DISTRIBUTETOKEN,
                To = Owner
            });


            await tokenStub.Transfer.SendAsync(new TransferInput
            {
                    Amount = 9375000,
                    Symbol = DISTRIBUTETOKEN,
                    To = DAppContractAddress
            });

            // Lp token
            var lpTokenStub = GetLpTokenContractStub(OwnerPair);
            await lpTokenStub.Create.SendAsync(new Token.CreateInput
            {
                Decimals = 6,
                Issuer = Owner,
                Symbol = LPTOKEN_01,
                IsBurnable = true,
                TokenName = LPTOKEN_01,
                TotalSupply = 10000000000000,
            });

            await lpTokenStub.Issue.SendAsync(new Token.IssueInput
            {
                Amount = 10000000000000,
                Symbol = LPTOKEN_01,
                To = Owner
            });
            
            // ReSharper disable once InvalidXmlDocComment
            /**
             * transfer to tom.
             */
            await lpTokenStub.Transfer.SendAsync(new Token.TransferInput
            {
                Amount = 10000000000,
                Symbol = LPTOKEN_01,
                To = Tom
            });

            
            await lpTokenStub.Transfer.SendAsync(new Token.TransferInput
            {
                Amount = 10000000000,
                Symbol = LPTOKEN_01,
                To = PoolOneMock
            });
            
            await lpTokenStub.Create.SendAsync(new Token.CreateInput
            {
                Decimals = 5,
                Issuer = Owner,
                Symbol = LPTOKEN_02,
                IsBurnable = true,
                TokenName = LPTOKEN_02,
                TotalSupply = 100000000000000
            });

            await lpTokenStub.Issue.SendAsync(new Token.IssueInput
            {
                Amount = 100000000000000,
                Symbol = LPTOKEN_02,
                To = Owner
            });
        }
        
        private async Task<long> GetCurrentBlockHeight()
        {
            return (await GetChain()).BestChainHeight;
        }
        
        private async Task<Chain> GetChain()
        {
            var blockchainService = await GetBlockService();
            return AsyncHelper.RunSync(blockchainService.GetChainAsync);
        }
        
        private async Task<IBlockchainService> GetBlockService()
        {
            var blockchainService = Application.ServiceProvider.GetRequiredService<IBlockchainService>();
            return blockchainService;
        }
        
        
        private async Task<long> SkipBlocks(long skipBlocks)
        {
            var tokenStub = GetLpTokenContractStub(OwnerPair);
            var first = (await GetChain()).BestChainHeight;
            for (int i = 0; i < skipBlocks; i++)
            {
                await tokenStub.Transfer.SendAsync(new Token.TransferInput
                {
                    Symbol = LPTOKEN_02,
                    Amount = 1,
                    To = Tom
                });
            }
            var second = (await GetChain()).BestChainHeight;
            second.Sub(first).ShouldBe(skipBlocks);
            return second;
        }
    }
}