using AElf;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Awaken.Contracts.PoolTwoContract
{
    
    /// <summary>
    /// Contract Initialize method.
    /// </summary>
    public partial class PoolTwoContract : PoolTwoContractContainer.PoolTwoContractBase
    {
        private const string Extension = "1000000000000";
        
        public override Empty Initialize(InitializeInput input)
        {
            Assert(State.Owner.Value == null, "Already initialized.");
            State.Owner.Value = input.Owner == null || input.Owner.Value.IsNullOrEmpty() ? Context.Sender : input.Owner;
            Assert(input.StartBlock >= Context.CurrentHeight,"Invalid StartBlock.");
            State.DistributeToken.Value = input.DistributeToken;
            State.DistributeTokenPerBlock.Value = input.DistributeTokenPerBlock;
            State.HalvingPeriod.Value = input.HalvingPeriod;
            State.StartBlock.Value = input.StartBlock;
            Assert(input.RedepositStartBlock>=State.StartBlock.Value,"Invalid Redeposit StartBlock.");
            State.RedepositStartBlock.Value = input.RedepositStartBlock;
            State.TotalReward.Value = input.TotalReward;
            State.RedepositAdjustFlag.Value = false;
            State.IssuedReward.Value = new BigIntValue(0);
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.PoolInfo.Value = new PoolInfo();
            State.LpTokenContract.Value = input.AwakenTokenContract;
            
            FixEndBlock(new BoolValue
            {
                Value = false
            });
            return new Empty(); 
        }
        
        /// <summary>
        /// Authority guard
        /// </summary>
        private void AssertSenderIsOwner()
        {
            Assert(State.Owner.Value != null, "Contract not initialized.");
            Assert(Context.Sender == State.Owner.Value, "Not Owner.");
        }
    }
}