using System;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Awaken.Contracts.PoolTwoContract
{
    public partial class PoolTwoContract
    {
        /**
         * TotalReward
         */
        public override BigIntValue TotalReward(Empty input)
        {
            return State.TotalReward.Value;
        }

        /**
         * IssuedReward
         */
        public override BigIntValue IssuedReward(Empty input)
        {
            return State.IssuedReward.Value;
        }

        /**
         * DistributeToken
         */
        public override StringValue DistributeToken(Empty input)
        {
            return new StringValue
            {
                Value = State.DistributeToken.Value
            };
        }

        /**
         * HalvingPeriod
         */
        public override Int64Value HalvingPeriod(Empty input)
        {
            return new Int64Value
            {
                Value = State.HalvingPeriod.Value
            };
        }

        /**
         *  FarmPoolOne
         */
        public override Address FarmPoolOne(Empty input)
        {
            return State.FarmPoolOne.Value;
        }

        /**
         *  PoolInfo
         */
        public override PoolInfoStruct PoolInfo(Int32Value input)
        {
            return State.PoolInfo.Value.PoolList[input.Value];
        }

        /**
         *  Pending
         */
        public override BigIntValue Pending(PendingInput input)
        {
            var pool = State.PoolInfo.Value.PoolList[input.Pid];
            var user = State.UserInfo[input.Pid][input.User] ?? new UserInfoStruct
            {
                Amount = 0,
                RewardDebt = 0
            };
            var accDistributeTokenPerShare = pool.AccDistributeTokenPerShare;
            var lpSupply = pool.TotalAmount;
            if (user.Amount > 0)
            {
                if (Context.CurrentHeight > pool.LastRewardBlock)
                {
                    var blockReward = GetDistributeTokenBlockReward(pool.LastRewardBlock);
                    var distributeTokenReward = blockReward.Mul(pool.AllocPoint).Div(State.TotalAllocPoint.Value);
                    accDistributeTokenPerShare = accDistributeTokenPerShare.Add(
                        distributeTokenReward.Mul(new BigIntValue
                        {
                            Value = Extension
                        }).Div(lpSupply)
                    );
                    return accDistributeTokenPerShare.Mul(user.Amount).Div(new BigIntValue
                    {
                        Value = Extension
                    }).Sub(user.RewardDebt);
                }

                return accDistributeTokenPerShare.Mul(user.Amount).Div(new BigIntValue
                {
                    Value = Extension
                }).Sub(user.RewardDebt);
            }

            return new BigIntValue(0);
        }

        /**
         *  GetDistributeTokenBlockReward
         */
        public override BigIntValue GetDistributeTokenBlockReward(Int64Value input)
        {
            return GetDistributeTokenBlockReward(input.Value);
        }


        private BigIntValue GetDistributeTokenBlockReward(long lastRewardBlock)
        {
            var blockReward = new BigIntValue(0);
            var rewardBlock = Context.CurrentHeight > State.EndBlock.Value
                ? State.EndBlock.Value
                : Context.CurrentHeight;
            if (rewardBlock <= lastRewardBlock)
            {
                return new BigIntValue(0);
            }

            var n = Phase(lastRewardBlock).Value;
            var m = Phase(rewardBlock).Value;
            while (n < m)
            {
                n++;
                var r = n.Mul(State.HalvingPeriod.Value).Add(State.StartBlock.Value);
                blockReward = blockReward.Add(
                    Reward(new Int64Value
                    {
                        Value = r
                    }).Mul((r.Sub(lastRewardBlock).ToString()))
                );
                lastRewardBlock = r;
            }

            blockReward = blockReward.Add(
                Reward(new Int64Value
                {
                    Value = rewardBlock
                }).Mul(rewardBlock.Sub(lastRewardBlock))
            );
            return blockReward;
        }

        /**
         *  Reward
         */
        public override BigIntValue Reward(Int64Value input)
        {
            var phase = Phase(input);
            return State.DistributeTokenPerBlock.Value.Div(1 << Convert.ToInt32(phase.Value));
        }

        /**
         *  Phase
         */
        public override Int64Value Phase(Int64Value input)
        {
            return Phase(input.Value);
        }

        private Int64Value Phase(long blockNumber)
        {
            if (State.HalvingPeriod.Value == 0)
            {
                return new Int64Value
                {
                    Value = 0
                };
            }

            if (blockNumber > State.StartBlock.Value)
            {
                return new Int64Value
                {
                    Value = (blockNumber - State.StartBlock.Value - 1) / State.HalvingPeriod.Value
                };
            }

            return new Int64Value();
        }

        /**
         *  PoolLength
         */
        public override Int64Value PoolLength(Empty input)
        {
            return new Int64Value
            {
                Value = State.PoolInfo.Value.PoolList.Count
            };
        }

        /**
         *  UserInfo
         */
        public override UserInfoStruct UserInfo(UserInfoInput input)
        {
            return State.UserInfo[input.Pid][input.User];
        }

        /**
         * DistributeTokenPerBlock
         */
        public override BigIntValue DistributeTokenPerBlock(Empty input)
        {
            return State.DistributeTokenPerBlock.Value;
        }

        /**
         *  TotalAllocPoint
         */
        public override Int64Value TotalAllocPoint(Empty input)
        {
            return new Int64Value
            {
                Value = State.TotalAllocPoint.Value
            };
        }

        /**
         * StartBlock
         */
        public override Int64Value StartBlock(Empty input)
        {
            return new Int64Value
            {
                Value = State.StartBlock.Value
            };
        }

        /**
         * endBlock
         */
        public override Int64Value EndBlock(Empty input)
        {
            return new Int64Value
            {
                Value = State.EndBlock.Value
            };
        }
    }
}