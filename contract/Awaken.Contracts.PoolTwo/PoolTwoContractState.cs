using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Awaken.Contracts.PoolTwoContract
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class PoolTwoContractState : ContractState
    {
        public SingletonState<Address> Owner { get; set; }
        public Int64State StartBlock { get; set; }
        
        public Int64State RedepositStartBlock { get; set; }
        public BoolState RedepositAdjustFlag { get; set; }
        public Int64State EndBlock { get; set; }
        public StringState DistributeToken { get; set; }
        public SingletonState<BigIntValue> DistributeTokenPerBlock { get; set; }
        
        public MappedState<long,Address,UserInfoStruct> UserInfo { get; set; }
        public SingletonState<PoolInfo> PoolInfo { get; set; }
        public Int64State TotalAllocPoint { get; set; }
        public Int64State HalvingPeriod { get; set; }
        public SingletonState<Address> FarmPoolOne { get; set; }
        public SingletonState<BigIntValue> TotalReward { get; set; }
        public SingletonState<BigIntValue> IssuedReward { get; set; }
        
    }
}