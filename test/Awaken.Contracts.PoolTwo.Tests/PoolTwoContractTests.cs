using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.ContractTestKit;
using AElf.CSharp.Core;
using AElf.Types;
using Awaken.Contracts.PoolTwoContract;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using ApproveInput = Awaken.Contracts.Token.ApproveInput;

namespace Awaken.Contracts.PoolTwo
{
    public partial class PoolTwoContractTests : PoolTwoContractTestBase
    {
        [Fact]
        public async Task Init()
        {
            await Initialize();
        }
        
        [Fact]
        public async Task Redeposit_Pool_Test()
        {
            OwnerPair = SampleAccount.Accounts.First().KeyPair;
            Owner = Address.FromPublicKey(OwnerPair.PublicKey);
            TomPair = SampleAccount.Accounts[1].KeyPair;
            Tom = Address.FromPublicKey(TomPair.PublicKey);
            PoolOneMockPair = SampleAccount.Accounts[2].KeyPair;
            PoolOneMock = Address.FromPublicKey(PoolOneMockPair.PublicKey);
            var stub = GetPoolTwoContractStub(OwnerPair);

            long redepositStartBlock = 513;
            await stub.Initialize.SendAsync(new InitializeInput
            {
                Admin = Owner,
                DistributeToken = Distributetoken,
                HalvingPeriod = 180,
                StartBlock = 453,
                TotalReward = 3375000,
                DistributeTokenPerBlock = 10000,
                AwakenTokenContract = LpTokenContractAddress,
                RedepositStartBlock = redepositStartBlock
            });
            
            await InitLpTokenContract();
            await CreateToken();

            await AddPoolFunc(stub, 1, LpToken01, false);
            await AddPoolFunc(stub, 2, LpToken01, true);
            var tomPoolStub = GetPoolTwoContractStub(TomPair);
            var tomLpTokenStub = GetLpTokenContractStub(TomPair);
            var poolOneLpContractStub = GetLpTokenContractStub(PoolOneMockPair);
            var poolOnePoolContractStub = GetPoolTwoContractStub(PoolOneMockPair);
            
            await stub.SetFarmPoolOne.SendAsync(PoolOneMock);

            await tomLpTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 1000000,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });
            
            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = 1000000,
                Pid = 1
            });
            var currentBlockHeight = await GetCurrentBlockHeight();
            var skipBlock = redepositStartBlock.Sub(currentBlockHeight);
            var endBlockCallAsync = await stub.EndBlock.CallAsync(new Empty());
            endBlockCallAsync.Value.ShouldBe(1173);
            
            await stub.FixEndBlock.SendAsync(new BoolValue
            {
                Value = true
            });
            var redepositAdjustFlag = await stub.RedepositAdjustFlag.CallAsync(new Empty());
            redepositAdjustFlag.Value.ShouldBe(false);
            await SkipBlocks(skipBlock);
            {
                var blockCallAsync = await stub.EndBlock.CallAsync(new Empty());
                blockCallAsync.Value.ShouldBe(1173);
            }
            
            await stub.FixEndBlock.SendAsync(new BoolValue
            {
                Value = true
            });
            {
                var callAsync = await stub.RedepositAdjustFlag.CallAsync(new Empty());
                callAsync.Value.ShouldBe(true);
                var blockCallAsync = await stub.EndBlock.CallAsync(new Empty());
                blockCallAsync.Value.ShouldBe(1840L);
            }
            
            await poolOneLpContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 2000000,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });
            
            await poolOnePoolContractStub.ReDeposit.SendAsync(new ReDepositInput
            {
                Amount = 2000000,
                User = Tom
            });
           
        }
        
        [Fact]
        public async Task Redeposit_Pool_Lagging_Start()
        {
            var ownerStub = await Initialize();
            await AddPoolFunc(ownerStub, 10, LpToken01, false);
            await AddPoolFunc(ownerStub, 150, LpToken01, true);
            long reDepositStartBlock = 300;

            await ownerStub.SetFarmPoolOne.SendAsync(PoolOneMock);
            
            var tomPoolStub = GetPoolTwoContractStub(TomPair);
            var tomLpTokenStub = GetLpTokenContractStub(TomPair);
            var poolOnePoolContractStub = GetPoolTwoContractStub(PoolOneMockPair);
            var poolOneLpContractStub = GetLpTokenContractStub(PoolOneMockPair);
            var tomTokenContractStub = GetTokenContractStub(TomPair);


            await tomLpTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 1000000,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });

           
            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = 1000000,
                Pid = 1
            });
            var currentBlockHeight = await GetCurrentBlockHeight();
            var skipBlock = reDepositStartBlock.Sub(currentBlockHeight);

            var endBlockCallAsync = await ownerStub.EndBlock.CallAsync(new Empty());
            endBlockCallAsync.Value.ShouldBe(500*4+50);


            await ownerStub.FixEndBlock.SendAsync(new BoolValue
            {
                Value = true
            });


            var redepositAdjustFlag = await ownerStub.RedepositAdjustFlag.CallAsync(new Empty());
            redepositAdjustFlag.Value.ShouldBe(false);
            
            await SkipBlocks(skipBlock);
            await poolOneLpContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 2000000,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });
            
            await poolOnePoolContractStub.ReDeposit.SendAsync(new ReDepositInput
            {
                Amount = 2000000,
                User = Tom
            });

            await ownerStub.FixEndBlock.SendAsync(new BoolValue
            {
                Value = true
            });

            {
                var callAsync = await ownerStub.RedepositAdjustFlag.CallAsync(new Empty());
                callAsync.Value.ShouldBe(true);

                var blockCallAsync = await ownerStub.EndBlock.CallAsync(new Empty());
                blockCallAsync.Value.ShouldBe(2303L);
            }
            {
                var balanceCallAsync = tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = DAppContractAddress,
                    Symbol = Distributetoken
                });
                balanceCallAsync.Result.Balance.ShouldBe(9375000);
            }


            await tomLpTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 2000000,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });
            
            
            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = 2000000,
                Pid = 1
            });
            
            {
                var balanceCallAsync = tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = DAppContractAddress,
                    Symbol = Distributetoken
                });
                balanceCallAsync.Result.Balance.ShouldBe(6975000);
            }
            {
                var blockHeight = await GetCurrentBlockHeight();
                var skipBlocks = 2050 - blockHeight;
                await SkipBlocks(skipBlocks);

                await tomLpTokenStub.Approve.SendAsync(new ApproveInput
                {
                    Amount = 3000000,
                    Spender = DAppContractAddress,
                    Symbol = LpToken01
                });

                await tomPoolStub.Deposit.SendAsync(new DepositInput
                {
                    Amount = 3000000,
                    Pid = 1
                });

                await poolOneLpContractStub.Approve.SendAsync(new ApproveInput
                {
                    Amount = 1000000,
                    Spender = DAppContractAddress,
                    Symbol = LpToken01
                });

                await poolOnePoolContractStub.ReDeposit.SendAsync(new ReDepositInput
                {
                    Amount = 1000000,
                    User = Tom
                });
                
                
                var balanceCallAsync = tomTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = DAppContractAddress,
                    Symbol = Distributetoken
                });
                balanceCallAsync.Result.Balance.ShouldBeGreaterThan(0);
            }

            {
                var blockHeight = await GetCurrentBlockHeight();
                var skipsBlock = 2303L - blockHeight;
                var skipBlocks = await SkipBlocks(skipsBlock);
                skipBlocks.ShouldBe(2303L);

                {
                    await tomPoolStub.Withdraw.SendAsync(new WithdrawInput
                    {
                        Amount = 1000000,
                        Pid = 1
                    });

                    {
                        var callAsync = await tomLpTokenStub.GetBalance.CallAsync(new Token.GetBalanceInput
                        {
                            Owner = DAppContractAddress,
                            Symbol = LpToken01
                        });
                        
                        callAsync.Amount.ShouldBeGreaterThan(1000000);
                    }

                    await tomPoolStub.Withdraw.SendAsync(new WithdrawInput
                    {
                        Amount = 1000000,
                        Pid = 0
                    });

                    {
                        var blockCallAsync = await tomPoolStub.EndBlock.CallAsync(new Empty());
                        blockCallAsync.Value.ShouldBe(2303L);
                    }


                    {
                        var issuedRewardCallAsync = await tomPoolStub.IssuedReward.CallAsync(new Empty());
                        issuedRewardCallAsync.Value.ShouldBe("9374998");
                    }
                }

            }

        }
        
        [Fact]
        public async Task View_Func_Test()
        {
            var ownerStub = await Initialize();
            var allocPoint = 10;
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, true);
            var totalAllocPoint = await ownerStub.TotalAllocPoint.CallAsync(new Empty());
            ShouldBeTestExtensions.ShouldBe<long>(totalAllocPoint.Value, 20);
            
        }
        
        [Fact]
        public async Task Set_Test()
        {
            var ownerStub = await Initialize();
            await ownerStub.SetHalvingPeriod.SendAsync(new Int64Value
            {
                Value = 600
            });
            var value = await ownerStub.HalvingPeriod.CallAsync(new Empty());
            ShouldBeTestExtensions.ShouldBe<long>(value.Value, 600);
            
        }

        [Fact]
        public async Task Deposit_Twice_Test()
        {
            var ownerStub = await Initialize();
            var allocPoint = 10;
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, true);
            var amount = 10000000000;
            var tomPoolStub = GetPoolTwoContractStub(TomPair);
            var tomTokenStub = GetLpTokenContractStub(TomPair);
            var startBlock = (await tomPoolStub.StartBlock.CallAsync(new Empty())).Value;
            
            await tomTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = amount,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });
            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = amount / 2,
                Pid = 1,
            });
            // Go to the second deposit block. startblock+100
            var currentBlockHeight = await GetCurrentBlockHeight();

            var skipBlocks = startBlock.Add(100).Sub(currentBlockHeight);
            currentBlockHeight = await SkipBlocks(skipBlocks);
            ShouldBeTestExtensions.ShouldBe<long>(currentBlockHeight, 150);
            //deposit again
            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = amount / 2,
                Pid = 1
            });
            var depoistBlock = SafeMath.Add((long) currentBlockHeight, 1);
            // move to start+200
            skipBlocks = startBlock.Add(200).Sub(depoistBlock);
            currentBlockHeight = await SkipBlocks(skipBlocks);
            var pending = await tomPoolStub.Pending.CallAsync(new PendingInput
            {
                Pid = 1,
                User = Tom
            });
            var distributeTokenPerBlock = await tomPoolStub.DistributeTokenPerBlock.CallAsync(new Empty());

            var pendingOneDeposit = depoistBlock.Sub(startBlock)
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2);
            var pendingTwoDeposit = SafeMath.Add((long) currentBlockHeight, 1).Sub(depoistBlock)
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2);

            var pendingExpect = pendingOneDeposit.Add(pendingTwoDeposit);
            pending.ShouldBe(pendingTwoDeposit);
            
            await tomPoolStub.Withdraw.SendAsync(new WithdrawInput
            {
                Amount = 1,
                Pid = 1
            });

            await tomPoolStub.UpdatePool.SendAsync(new Int32Value
            {
                Value = 1
            });

            var tomCommonTokenStub = GetTokenContractStub(TomPair);
            var balance = await tomCommonTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = Distributetoken
            });
            balance.Balance.ShouldBe(pendingExpect);
            
        }

        [Fact]
        public async Task Deposit_After_Startblock_And_Withdraw_In_Stage_Two()
        {
            var ownerStub = await Initialize();
            var allocPoint = 10;
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            var amount = 10000000000;
            var tomPoolStub = GetPoolTwoContractStub(TomPair);
            var tomTokenStub = GetLpTokenContractStub(TomPair);
            await tomTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = amount,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });
            
            var startBlock = (await tomPoolStub.StartBlock.CallAsync(new Empty())).Value;
            var currentBlockHeight = await GetCurrentBlockHeight();
            var skipBlock = startBlock.Add(20).Sub(currentBlockHeight);
            currentBlockHeight = await SkipBlocks(skipBlock);
            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = amount,
                Pid = 1
            });
            var depositBlock = SafeMath.Add(currentBlockHeight, 1);

            //skip to withdraw blocks
            currentBlockHeight = await GetCurrentBlockHeight();
            skipBlock = startBlock.Add(600).Sub(currentBlockHeight);
            currentBlockHeight = await SkipBlocks(skipBlock);
            var distributeTokenPerBlock = await tomPoolStub.DistributeTokenPerBlock.CallAsync(new Empty());
            var pending = await tomPoolStub.Pending.CallAsync(new PendingInput
            {
                Pid = 1,
                User = Tom
            });
            var stageOneEndBlock = startBlock.Add(HalvingPeriod);
            var pendingStageOneExpect = stageOneEndBlock.Sub(depositBlock)
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2);
            var pendingStageTwoExpect = SafeMath.Add(currentBlockHeight, 1).Sub(stageOneEndBlock)
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value))
                .Div(2).Div(2);
            var pendingExpect = pendingStageOneExpect.Add(pendingStageTwoExpect);
            pending.ShouldBe(pendingExpect);

            var withdrawSendAsync = await tomPoolStub.Withdraw.SendAsync(new WithdrawInput
            {
                Amount = amount,
                Pid = 1
            });
            var blockNumber = withdrawSendAsync.TransactionResult.BlockNumber;
            var withdrawDistributeTokenStageOneExpect =
                stageOneEndBlock.Sub(depositBlock).Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2);
            var withdrawDistributeTokenStageTwoExpect =
                blockNumber.Sub(stageOneEndBlock).Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2).Div(2);
            var withdrawDistributeTokenExpect =
                withdrawDistributeTokenStageOneExpect.Add(withdrawDistributeTokenStageTwoExpect);
            var tomCommonTokenStub = GetTokenContractStub(TomPair);

            var balance = await tomCommonTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = Distributetoken
            });
            balance.Balance.ShouldBe(withdrawDistributeTokenExpect);
        }

        [Fact]
        public async Task Deposit_Before_Start_Block_And_Withdraw_In_Stage_Two()
        {
            var ownerStub = await Initialize();
            var allocPoint = 10;
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            var tomPoolStub = GetPoolTwoContractStub(TomPair);
            var tomTokenStub = GetLpTokenContractStub(TomPair);
            var amount = 10000000000;
            var startBlock = (await ownerStub.StartBlock.CallAsync(new Empty())).Value;
            await tomTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = amount,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });

            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = amount,
                Pid = 1
            });
            //skip to withdraw blocks (600)
            var currentBlockHeight = await GetCurrentBlockHeight();
            var skipBlocks = SafeMath.Add((long) startBlock, 600).Sub(currentBlockHeight);
            currentBlockHeight = await SkipBlocks(skipBlocks);
            var distributeTokenPerBlock = await tomPoolStub.DistributeTokenPerBlock.CallAsync(new Empty());
            var pending = await tomPoolStub.Pending.CallAsync(new PendingInput
            {
                Pid = 1,
                User = Tom
            });
            var stageOneEndBlock = SafeMath.Add((long) startBlock, HalvingPeriod);
            var pendingStageOneExpect = stageOneEndBlock.Sub(startBlock)
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2);
            var pendingStageTwoExpect = SafeMath.Add((long) currentBlockHeight, 1).Sub(stageOneEndBlock)
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2).Div(2);
            var pendingExpect = pendingStageOneExpect.Add(pendingStageTwoExpect);
            pending.ShouldBe(pendingExpect);

            await tomPoolStub.Withdraw.SendAsync(new WithdrawInput
            {
                Amount = amount,
                Pid = 1
            });

            var withdrawDistributeTokenStageOneExpect = stageOneEndBlock.Sub(startBlock)
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2);

            var withdrawDistributeTokenStageTwoExpect = SafeMath.Add((long) currentBlockHeight, 1).Sub(stageOneEndBlock)
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2).Div(2);

            var withdrawDistributeTokenExpect =
                withdrawDistributeTokenStageOneExpect.Add(withdrawDistributeTokenStageTwoExpect);
           
            var tomCommonTokenStub = GetTokenContractStub(TomPair);
            var balance = await tomCommonTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = Distributetoken
            });
            balance.Balance.ShouldBe(withdrawDistributeTokenExpect);
        }


        [Fact]
        public async Task Deposit_After_Startblock_And_Withdraw_In_Stage_One()
        {
            var ownerStub = await Initialize();
            var allocPoint = 10;
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            var startBlock = await ownerStub.StartBlock.CallAsync(new Empty());
            var tomPoolStub = GetPoolTwoContractStub(TomPair);
            var tomTokenStub = GetLpTokenContractStub(TomPair);
            await tomTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = 10000000000,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });
            var currentBlockHeight = await GetCurrentBlockHeight();
            var skipBlock = (startBlock.Value - currentBlockHeight + 20);
            currentBlockHeight = await SkipBlocks(skipBlock);
            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = 10000000000,
                Pid = 1
            });
            var depositBlock = SafeMath.Add(currentBlockHeight, 1);
            // skip to withdraw height.
            currentBlockHeight = await SkipBlocks(50);
            var distributeTokenPerBlock = await tomPoolStub.DistributeTokenPerBlock.CallAsync(new Empty());
            var pending = await tomPoolStub.Pending.CallAsync(new PendingInput
            {
                Pid = 1,
                User = Tom
            });
            var pendingExpect = (SafeMath.Add(currentBlockHeight, 1).Sub(depositBlock))
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value)).Div(2);
            pending.ShouldBe(pendingExpect);
            await tomPoolStub.Withdraw.SendAsync(new WithdrawInput
            {
                Amount = 10000000000,
                Pid = 1
            });

            var withdrawDistributeTokenExpect = SafeMath.Add((long) currentBlockHeight, 1).Sub(depositBlock)
                .Mul(Convert.ToInt64(distributeTokenPerBlock.Value).Div(2));
            
            var tomCommonTokenStub = GetTokenContractStub(TomPair);
            var balance = await tomCommonTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Tom,
                Symbol = Distributetoken
            });
            balance.Balance.ShouldBe(withdrawDistributeTokenExpect);
        }

        [Fact]
        public async Task Deposit_Before_StartBlock_And_Withdraw_In_Stage_One()
        {
            var ownerStub = await Initialize();
            var allocPoint = 10;
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            var startBlock = await ownerStub.StartBlock.CallAsync(new Empty());
            var tomPoolStub = GetPoolTwoContractStub(TomPair);
            var tomTokenStub = GetLpTokenContractStub(TomPair);
            await tomTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = 10000000000,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });
            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = 10000000000,
                Pid = 1
            });
            var currentBlock = await SkipBlocks(50);
            var distributeTokenPerBlock = await tomPoolStub.DistributeTokenPerBlock.CallAsync(new Empty());
            var pending = await tomPoolStub.Pending.CallAsync(new PendingInput
            {
                Pid = 1,
                User = Tom
            });

            var pendingExpect = distributeTokenPerBlock.Div(2).Mul(SafeMath.Sub((long) currentBlock, (long) startBlock.Value).Add(1));
            pendingExpect.ShouldBe(pending);

            await tomPoolStub.Withdraw.SendAsync(new WithdrawInput
            {
                Pid = 1,
                Amount = 10000000000
            });
            var currentBlockHeight = await GetCurrentBlockHeight();
            var withdrawDistributeTokenExpect =
                distributeTokenPerBlock.Div(2).Mul(SafeMath.Sub((long) currentBlockHeight, (long) startBlock.Value));
            
            var tomCommonTokenStub = GetTokenContractStub(TomPair);
            var balance = await tomCommonTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Symbol = Distributetoken,
                Owner = Tom
            });
            withdrawDistributeTokenExpect.ShouldBe(balance.Balance);
            
            var issuedReward = await ownerStub.IssuedReward.CallAsync(new Empty());
            ShouldBeTestExtensions.ShouldBe(issuedReward, withdrawDistributeTokenExpect);
            var totalReward = await ownerStub.TotalReward.CallAsync(new Empty());
            ShouldBeTestExtensions.ShouldBe<BigIntValue>(totalReward, 9375000);

            var distributeToken = await ownerStub.DistributeToken.CallAsync(new Empty());
            ShouldBeStringTestExtensions.ShouldBe(distributeToken.Value, Distributetoken);
            await ownerStub.SetFarmPoolOne.SendAsync(PoolOneMock);
            var farmPoolOne = await ownerStub.FarmPoolOne.CallAsync(new Empty());
            farmPoolOne.ShouldBe(PoolOneMock);

            var endBlock = await ownerStub.EndBlock.CallAsync(new Empty());
            ShouldBeTestExtensions.ShouldBe(endBlock.Value, 2050);
        }

        [Fact]
        public async Task SetDistributeTokenPerBlock_Should_Work()
        {
            var ownerStub = await Initialize();
            var allocPoint = 10;
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await ownerStub.SetDistributeTokenPerBlock.SendAsync(new Int64Value
            {
                Value = 500
            });
            var value = await ownerStub.DistributeTokenPerBlock.CallAsync(new Empty());
            ShouldBeStringTestExtensions.ShouldBe(value.Value, "500");
        }


        [Fact]
        public async Task Set_Should_Work()
        {
            var ownerStub = await Initialize();
            var allocPoint = 10;
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await ownerStub.Set.SendAsync(new SetInput
            {
                Pid = 1,
                AllocPoint = 20,
                WithUpdate = true,
                NewPerBlock = 500
            });
            var pool = await ownerStub.PoolInfo.CallAsync(new Int32Value
            {
                Value = 1
            });

            ShouldBeTestExtensions.ShouldBe<long>(pool.AllocPoint, 20);
            var int64Value = await ownerStub.TotalAllocPoint.CallAsync(new Empty());
            ShouldBeTestExtensions.ShouldBe<long>(int64Value.Value, 20 - 10 + 20);
            var value = await ownerStub.DistributeTokenPerBlock.CallAsync(new Empty());
            ShouldBeStringTestExtensions.ShouldBe(value.Value, "500");
        }

        [Fact]
        public async Task Add_Pool_Should_Works()
        {
            var ownerStub = await Initialize();
            var allocPoint = 10;
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocPoint, LpToken01, false);
            var pool = await ownerStub.PoolInfo.CallAsync(new Int32Value
            {
                Value = 1
            });
            ShouldBeStringTestExtensions.ShouldBe(pool.LpToken, LpToken01);
            ShouldBeTestExtensions.ShouldBe<long>(pool.AllocPoint, allocPoint);
            var length = await ownerStub.PoolLength.CallAsync(new Empty());
            ShouldBeTestExtensions.ShouldBe<long>(length.Value, 2);
            var currentBlockHeight = await GetCurrentBlockHeight();

            var reward = await ownerStub.Reward.CallAsync(new Int64Value
            {
                Value = currentBlockHeight
            });
            ShouldBeStringTestExtensions.ShouldBe(reward.Value, "10000");
        }

        [Fact]
        public async Task Deposit_And_Withdraw_Should_Works()
        {
            var ownerStub = await Initialize();
            var allocpoint = 10;
            await AddPoolFunc(ownerStub, allocpoint, LpToken01, false);
            await AddPoolFunc(ownerStub, allocpoint, LpToken01, false);
            var tomTokenStub = GetLpTokenContractStub(TomPair);
            await tomTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = 10000000,
                Spender = DAppContractAddress,
                Symbol = LpToken01
            });

            var tomPoolStub = GetPoolTwoContractStub(TomPair);
            await tomPoolStub.Deposit.SendAsync(new DepositInput
            {
                Amount = 10000000,
                Pid = 1
            });
            var user = await tomPoolStub.UserInfo.CallAsync(new UserInfoInput
            {
                Pid = 1,
                User = Tom
            });
            user.Amount.Value.ShouldBe("10000000");

            await tomPoolStub.Withdraw.SendAsync(new WithdrawInput
            {
                Amount = 500000,
                Pid = 1
            });

            user = await tomPoolStub.UserInfo.CallAsync(new UserInfoInput
            {
                Pid = 1,
                User = Tom
            });
            user.Amount.Value.ShouldBe("9500000");
        }

        private async Task AddPoolFunc(PoolTwoContractContainer.PoolTwoContractStub owenrStub,
            int allocPoint,
            string token,
            bool flag)
        {
            await owenrStub.Add.SendAsync(new AddInput
            {
                AllocPoint = allocPoint,
                LpToken = token,
                WithUpdate = flag
            });
        }
    }
}