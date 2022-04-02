using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Awaken.Contracts.PoolTwoContract
{
    public partial class PoolTwoContract
    {
        public override Empty SetLpTokenAddress(SetLpTokenAddressInput input)
        {   
            AssertSenderIsOwner();
            Assert(input.AwakenTokenContractAddress!=null&&!new Address().Equals(input.AwakenTokenContractAddress),"Invalid input.");
            State.LpTokenContract.Value = input.AwakenTokenContractAddress;
            return new Empty();
        }

        /**
         * withdraw
         */
        public override Empty Withdraw(WithdrawInput input)
        {
            WithdrawDistributeToken(input.Pid, input.Amount, Context.Sender);
            return new Empty();
        }
        
        private void WithdrawDistributeToken(int pid, BigIntValue amount, Address sender)
        {
            var pool = State.PoolInfo.Value.PoolList[pid];
            var user = State.UserInfo[pid][sender];
            Assert(user.Amount >= amount, "Insufficient amount.");
            UpdatePool(pid);
            var pendingAmount = user.Amount
                .Mul(pool.AccDistributeTokenPerShare)
                .Div(new BigIntValue
                {
                    Value = Extension
                })
                .Sub(user.RewardDebt);

            if (pendingAmount > 0)
            {
                SafeDistributeTokenTransfer(sender, pendingAmount);
                Context.Fire(new ClaimRevenue
                {
                    User = sender,
                    Pid = pid,
                    TokenSymbol = State.DistributeToken.Value,
                    Amount = pendingAmount,
                    TokenType = 0
                });
            }

            if (!long.TryParse(amount.Value, out long parseOut))
            {
                throw new AssertionException($"Failed to parse {amount.Value}");
            }

            if (amount > 0)
            {
                user.Amount = user.Amount.Sub(amount);
                pool.TotalAmount = pool.TotalAmount.Sub(amount);
                State.LpTokenContract.Transfer.Send(new Token.TransferInput
                {
                    Symbol = pool.LpToken,
                    Amount = parseOut,
                    To = sender
                });
            }

            user.RewardDebt = user.Amount.Mul(pool.AccDistributeTokenPerShare).Div(new BigIntValue
            {
                Value = Extension
            });

            State.UserInfo[pid][sender] = user;
            State.PoolInfo.Value.PoolList[pid] = pool;
            Context.Fire(new Withdraw
            {
                Pid = pid,
                Amount = parseOut,
                User = sender
            });
        }

        /**
         * deposit
         */
        public override Empty Deposit(DepositInput input)
        {
            Assert(input.Pid == 1, "Invalid pid");
            DepositDistributeToken(input.Pid, input.Amount, Context.Sender);
            return new Empty();
        }

        /**
         * ReDeposit
         */
        public override Empty ReDeposit(ReDepositInput input)
        {
            Assert(Context.Sender == State.FarmPoolOne.Value, "Invalid sender");
            DepositDistributeToken(0, input.Amount, input.User);
            return new Empty();
        }


        private void DepositDistributeToken(int pid, BigIntValue amount, Address sender)
        {
            var pool = State.PoolInfo.Value.PoolList[pid];
            var user = State.UserInfo[pid][sender] ?? new UserInfoStruct
            {
                Amount = 0,
                RewardDebt = 0
            };
            UpdatePool(pid);
            if (user.Amount > 0)
            {
                var pendingAmount = user.Amount
                    .Mul(pool.AccDistributeTokenPerShare)
                    .Div(new BigIntValue
                    {
                        Value = Extension
                    })
                    .Sub(user.RewardDebt);
                if (pendingAmount > 0)
                {
                    SafeDistributeTokenTransfer(sender, pendingAmount);
                    Context.Fire(new ClaimRevenue
                    {
                        User = sender,
                        Pid = pid,
                        TokenSymbol = State.DistributeToken.Value,
                        Amount = pendingAmount,
                        TokenType = 0
                    });
                }
            }

            if (!long.TryParse(amount.Value, out long parseOut))
            {
                throw new AssertionException($"Failed to parse {amount.Value}");
            }

            if (amount > 0)
            {
                State.LpTokenContract.TransferFrom.Send(new Token.TransferFromInput
                {
                    From = Context.Sender,
                    To = Context.Self,
                    Amount = parseOut,
                    Symbol = pool.LpToken
                });
                user.Amount = user.Amount.Add(amount);
                pool.TotalAmount = pool.TotalAmount.Add(amount);
            }

            user.RewardDebt = user.Amount.Mul(pool.AccDistributeTokenPerShare).Div(new BigIntValue
            {
                Value = Extension
            });
            State.UserInfo[pid][sender] = user;
            State.PoolInfo.Value.PoolList[pid] = pool;
            Context.Fire(new Deposit
            {
                Amount = parseOut,
                Pid = pid,
                User = sender
            });
        }

        private void SafeDistributeTokenTransfer(Address to, BigIntValue amount)
        {
            var distributeTokenBal = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Owner = Context.Self,
                Symbol = State.DistributeToken.Value
            }).Balance;
            amount = amount > distributeTokenBal ? distributeTokenBal : amount;

            if (!long.TryParse(amount.Value, out long parseOut))
            {
                throw new AssertionException($"Failed to parse {amount.Value}");
            }

            State.TokenContract.Transfer.Send(new TransferInput
            {
                To = to,
                Amount = parseOut,
                Symbol = State.DistributeToken.Value
            });
        }

        /**
         *  Set
         */
        public override Empty Set(SetInput input)
        {
            AssertSenderIsOwner();
            if (input.WithUpdate)
            {
                MassUpdatePools(new Empty());
            }

            State.TotalAllocPoint.Value =
                State.TotalAllocPoint.Value - State.PoolInfo.Value.PoolList[input.Pid].AllocPoint + input.AllocPoint;

            State.PoolInfo.Value.PoolList[input.Pid].AllocPoint = input.AllocPoint;
            State.DistributeTokenPerBlock.Value = input.NewPerBlock;
            Context.Fire(new WeightSet
            {
                Pid = input.Pid,
                NewAllocationPoint = input.AllocPoint
            });

            Context.Fire(new DistributeTokenPerBlockSet
            {
                NewDistributeTokenPerBlock = input.NewPerBlock
            });
            return new Empty();
        }

        /**
         *  Add
         */
        public override Empty Add(AddInput input)
        {
            AssertSenderIsOwner();
            Assert(!string.IsNullOrWhiteSpace(input.LpToken), "Token symbol null.");
            if (input.WithUpdate)
            {
                MassUpdatePools(new Empty());
            }

            var lastRewardBlock = Context.CurrentHeight > State.StartBlock.Value
                ? Context.CurrentHeight
                : State.StartBlock.Value;
            State.TotalAllocPoint.Value += input.AllocPoint;
            var count = State.PoolInfo.Value.PoolList.Count;
            State.PoolInfo.Value.PoolList.Add(new PoolInfoStruct
            {
                AllocPoint = input.AllocPoint,
                LpToken = input.LpToken,
                LastRewardBlock = lastRewardBlock,
                AccDistributeTokenPerShare = 0,
                TotalAmount = 0
            });
            Context.Fire(new PoolAdded
            {
                Pid = count,
                Token = input.LpToken,
                AllocationPoint = input.AllocPoint,
                LastRewardBlockHeight = lastRewardBlock,
                PoolType = 1,
            });
            return new Empty();
        }

        /**
         *  SetDistributeTokenPerBlock
         */
        public override Empty SetDistributeTokenPerBlock(Int64Value input)
        {
            AssertSenderIsOwner();
            MassUpdatePools(new Empty());
            State.DistributeTokenPerBlock.Value = input.Value;
            return new Empty();
        }

        /**
         * 
         */
        public override Empty MassUpdatePools(Empty input)
        {
            var length = State.PoolInfo.Value.PoolList.Count;
            for (int i = 0; i < length; i++)
            {
                UpdatePool(i);
            }

            return new Empty();
        }


        public override Empty UpdatePool(Int32Value input)
        {
            return UpdatePool(input.Value);
        }


        private Empty UpdatePool(int pid)
        {
            var pool = State.PoolInfo.Value.PoolList[pid];
            if (Context.CurrentHeight <= pool.LastRewardBlock)
            {
                return new Empty();
            }

            var lpSupply = pool.TotalAmount;
            if (lpSupply.Equals(0))
            {
                pool.LastRewardBlock = Context.CurrentHeight;
                State.PoolInfo.Value.PoolList[pid] = pool;
                return new Empty();
            }

            var blockReward = GetDistributeTokenBlockReward(new Int64Value
            {
                Value = pool.LastRewardBlock
            });
            if (blockReward <= 0)
            {
                return new Empty();
            }

            var distributeTokenReward = blockReward.Mul(pool.AllocPoint).Div(State.TotalAllocPoint.Value);

            State.IssuedReward.Value = State.IssuedReward.Value.Add(distributeTokenReward);
            pool.AccDistributeTokenPerShare = pool.AccDistributeTokenPerShare.Add(
                distributeTokenReward.Mul(new BigIntValue
                {
                    Value = Extension
                }).Div(lpSupply)
            );
            pool.LastRewardBlock = Context.CurrentHeight;
            State.PoolInfo.Value.PoolList[pid] = pool;
            Context.Fire(new UpdatePool
            {
                Pid = pid,
                DistributeTokenAmount = distributeTokenReward,
                UpdateBlockHeight = Context.CurrentHeight
            });

            return new Empty();
        }


        /**
         * 
         */
        public override Empty SetFarmPoolOne(Address input)
        {
            AssertSenderIsOwner();
            State.FarmPoolOne.Value = input;
            return new Empty();
        }

        /**
         * 
         */
        public override Empty SetHalvingPeriod(Int64Value input)
        {
            AssertSenderIsOwner();
            State.HalvingPeriod.Value = input.Value;
            Context.Fire(new HalvingPeriodSet
            {
                Period = State.HalvingPeriod.Value
            });
            return new Empty();
        }

        /**
         * 
         */
        public override Empty FixEndBlock(BoolValue input)
        {
            AssertSenderIsOwner();
            if (input.Value)
            {
                MassUpdatePools(new Empty());
            }

            var restReward = State.TotalReward.Value.Sub(State.IssuedReward.Value);
            var blockHeightEnd = GetEndBlock(restReward);
            State.EndBlock.Value = blockHeightEnd;
            return new Empty();
        }

        private long GetEndBlock(BigIntValue restReward)
        {
            var blockHeightBegin = Context.CurrentHeight > State.StartBlock.Value
                ? Context.CurrentHeight
                : State.StartBlock.Value;
            var blockHeightEnd = State.EndBlock.Value;
            while (!restReward.Equals(new BigIntValue(0)))
            {
                GetEndBlock(blockHeightBegin, restReward, out var newBlockHeighEnd, out var newReward);
                restReward = newReward;
                blockHeightEnd = newBlockHeighEnd;
                blockHeightBegin = newBlockHeighEnd;
            }

            return blockHeightEnd;
        }

        private void GetEndBlock(long blockHeightBegin, BigIntValue restReward, out long blockEnd,
            out BigIntValue reward)
        {
            var value = Phase(blockHeightBegin).Value;
            var phase = blockHeightBegin > State.StartBlock.Value &&
                        blockHeightBegin.Sub(State.StartBlock.Value) % (State.HalvingPeriod.Value) == 0
                ? value.Add(1)
                : value;
            var endRewardBlock = (phase + 1).Mul(State.HalvingPeriod.Value).Add(State.StartBlock.Value);
            var tmp = State.DistributeTokenPerBlock.Value.Div(1 << (int) phase);
            if (phase < 200 && tmp.Value.Equals("0"))
            {
                blockEnd = endRewardBlock;
                reward = 0;
                return;
            }

            Assert(tmp > 0, "Error");
            reward = new BigIntValue(0);
            reward = tmp.Mul(endRewardBlock.Sub(blockHeightBegin));
            if (restReward >= reward)
            {
                blockEnd = endRewardBlock;
                reward = restReward.Sub(reward);
            }
            else
            {
                var blockEndStr = restReward.Div(tmp).Add(blockHeightBegin).Value;
                if (!long.TryParse(blockEndStr, out blockEnd))
                {
                    throw new AssertionException($"Failed to parse {blockEndStr}");
                }
                reward = new BigIntValue(0);
            }
        }
    }
}