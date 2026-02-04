using MyShopGame.Core;

namespace MyShopGame.BuildSystem
{
    public interface IPlacementRule
    {
        bool EnabledFor(BuildItemData item);
        PlacementResult Evaluate(PlacementRequest req);
    }
}
