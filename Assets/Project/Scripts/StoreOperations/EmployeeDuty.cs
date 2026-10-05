using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;
using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>One employee claims one job, walks there, performs it and releases the claim.</summary>
    public sealed class EmployeeDuty : MonoBehaviour
    {
        private StoreOperations _shop;
        private ShopCharacter _character;
        private ShopperVisit _visit;
        private ShopDirtSpot _dirt;
        private PlacedShelfStock _shelf;
        private ProductItemData _product;
        private float _taskTime, _retry, _travelTime;
        public StaffRole Role { get; private set; }
        public void Initialize(StoreOperations shop, ShopCharacter character, StaffRole role) { _shop = shop; _character = character; Role = role; }
        public void Advance(float seconds, bool working)
        {
            if (!working) { CancelDuty(); return; }
            if (_visit != null && !_shop.Visits.ContainsVisit(_visit)) CancelDuty();
            if (_visit == null && _dirt == null && _shelf == null)
            {
                _retry -= seconds; if (_retry > 0) return; _retry = 1;
                if (!FindDuty()) return;
            }
            _character.Advance(seconds); _travelTime += seconds;
            if (_travelTime > 35) { CancelDuty(); return; }
            if (Role == StaffRole.Guard && _visit != null && Vector3.Distance(transform.position, _visit.Character.transform.position) <= 0.25f)
            {
                // Reaching the thief interrupts the escape while the short capture action finishes.
                _visit.Restrain(); _taskTime += seconds;
                if (_taskTime >= 1.5f) { _shop.CatchThief(_visit); CancelDuty(); }
                return;
            }
            if (Role == StaffRole.Guard && _visit != null && !_character.Arrived)
            {
                _retry -= seconds;
                if (_retry <= 0) { _retry = 0.8f; _character.MoveTo(_visit.Character.transform.position); }
            }
            if (!_character.Arrived) return;
            if (_visit != null && Role == StaffRole.Guard && Vector3.Distance(transform.position, _visit.Character.transform.position) > 0.25f)
            { _taskTime = 0; if (!_character.MoveTo(_visit.Character.transform.position)) CancelDuty(); return; }
            _taskTime += seconds;
            if (_taskTime < (Role == StaffRole.Guard ? 1.5f : _shop.Balance.staffTaskSeconds)) return;
            switch (Role)
            {
                case StaffRole.Cashier: _shop.CompleteCheckout(_visit); break;
                case StaffRole.Guard: _shop.CatchThief(_visit); break;
                case StaffRole.Cleaner: _shop.Clean(_dirt); break;
                case StaffRole.Stocker:
                    if (_shelf != null && !_shop.IsShelfInPlayerWork(_shelf)) _shelf.RefillFromInventory(_shop.Warehouse, _product);
                    break;
            }
            CancelDuty();
        }
        private bool FindDuty()
        {
            Vector3 target = transform.position;
            switch (Role)
            {
                case StaffRole.Cashier:
                    _visit = _shop.ClaimCheckout(); if (_visit == null) return false;
                    target = _shop.CheckoutPosition + Vector3.forward * 0.25f; break;
                case StaffRole.Guard:
                    foreach (var visit in _shop.Visits)
                        if (visit.TryClaimThreat()) { _visit = visit; break; }
                    if (_visit == null) return false; target = _visit.Character.transform.position; break;
                case StaffRole.Cleaner:
                    foreach (var dirt in _shop.Dirt) if (dirt != null && dirt.TryClaim()) { _dirt = dirt; break; }
                    if (_dirt == null) return false; target = _dirt.transform.position; break;
                case StaffRole.Stocker:
                    if (!_shop.TryClaimRestock(out _shelf, out _product)) return false;
                    if (!_shop.TryShelfApproach(_shelf, transform.position, out target)) { CancelDuty(); return false; }
                    break;
            }
            if (_character.MoveTo(target)) return true;
            CancelDuty(); return false;
        }
        public void CancelDuty()
        {
            _character?.Stop();
            _visit?.Release(); if (_dirt != null) _dirt.Release(); if (_shelf != null) _shop.ReleaseRestock(_shelf);
            _visit = null; _dirt = null; _shelf = null; _product = null; _taskTime = _travelTime = 0;
        }
        private void OnDestroy() { if (_shop != null) CancelDuty(); }
    }

    internal static class VisitCollection
    {
        public static bool ContainsVisit(this System.Collections.Generic.IReadOnlyList<ShopperVisit> visits, ShopperVisit target)
        {
            for (int i = 0; i < visits.Count; i++) if (visits[i] == target) return true;
            return false;
        }
    }
}
