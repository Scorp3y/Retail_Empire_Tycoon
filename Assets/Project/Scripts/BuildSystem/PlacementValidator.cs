using System.Collections.Generic;
using UnityEngine;
using MyShopGame.Core;

namespace MyShopGame.BuildSystem
{
    public enum PlaceFailReason
    {
        None,
        NotPurchased,
        Overlap,
        NoAccess,
        RuleFailed
    }

    public readonly struct PlacementRequest
    {
        public readonly BuildItemData item;
        public readonly Vector3Int anchorCell;
        public readonly bool rotated;
        public readonly int facing;

        public PlacementRequest(BuildItemData item, Vector3Int anchorCell, bool rotated, int facing)
        {
            this.item = item;
            this.anchorCell = anchorCell;
            this.rotated = rotated;
            this.facing = facing;
        }
    }

    public readonly struct PlacementResult
    {
        public readonly bool ok;
        public readonly PlaceFailReason reason;
        public readonly string message;

        public PlacementResult(bool ok, PlaceFailReason reason, string message)
        {
            this.ok = ok;
            this.reason = reason;
            this.message = message;
        }

        public static PlacementResult Success()
            => new PlacementResult(true, PlaceFailReason.None, string.Empty);

        public static PlacementResult Fail(PlaceFailReason reason, string message)
            => new PlacementResult(false, reason, message);
    }

    public sealed class PlacementValidator
    {
        private readonly List<IPlacementRule> _rules;

        public PlacementValidator(List<IPlacementRule> rules)
        {
            _rules = rules ?? new List<IPlacementRule>();
        }

        public PlacementResult CanPlace(PlacementRequest req)
        {
            foreach (var rule in _rules)
            {
                if (rule == null) continue;
                if (!rule.EnabledFor(req.item)) continue;

                var r = rule.Evaluate(req);
                if (!r.ok) return r;
            }

            return PlacementResult.Success();
        }
    }
}
