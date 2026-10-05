using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;
using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    public enum VisitStage { Shopping, Queueing, Waiting, Escaping, Leaving }

    /// <summary>A basket reserves goods until payment or theft; saves never lose unpaid goods.</summary>
    public sealed class ShopperVisit
    {
        public ShopCharacter Character { get; }
        public PlacedShelfStock Shelf { get; }
        public ProductItemData Product { get; }
        public int Quantity { get; }
        public bool IsThief { get; }
        public VisitStage Stage { get; private set; }
        public float WaitingSeconds { get; private set; }
        public int RemainingCatchClicks { get; private set; }
        public bool Claimed { get; private set; }
        public bool Restrained { get; private set; }

        public ShopperVisit(ShopCharacter character, PlacedShelfStock shelf, int quantity, bool thief, int catchClicks)
        {
            Character = character; Shelf = shelf; Product = shelf.CurrentProduct; Quantity = quantity;
            IsThief = thief; RemainingCatchClicks = catchClicks;
        }
        public void ChangeStage(VisitStage stage) { Stage = stage; WaitingSeconds = 0; }
        public void Wait(float seconds) { WaitingSeconds += seconds; }
        public bool TryClaim() { if (Claimed || Stage != VisitStage.Waiting) return false; Claimed = true; return true; }
        public void Release() { Claimed = false; Restrained = false; }
        public bool TryClaimThreat() { if (Claimed || Stage != VisitStage.Escaping) return false; Claimed = true; return true; }
        public void Restrain() { if (Claimed && Stage == VisitStage.Escaping) Restrained = true; }
        public bool CatchClick()
        {
            if (!IsThief || Stage != VisitStage.Escaping) return false;
            RemainingCatchClicks = Mathf.Max(0, RemainingCatchClicks - 1);
            return RemainingCatchClicks == 0;
        }
        public bool CommitGoods()
        {
            if (Shelf == null || Shelf.CurrentProduct != Product || Shelf.CurrentAmount < Quantity) return false;
            for (int i = 0; i < Quantity; i++) Shelf.TryTakeOne(out _);
            return true;
        }
    }
}
