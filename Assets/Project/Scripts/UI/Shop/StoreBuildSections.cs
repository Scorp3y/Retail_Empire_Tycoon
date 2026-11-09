using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Presentation sections for purchasable equipment, construction and decor.</summary>
    public sealed class StoreBuildSections : MonoBehaviour
    {
        private ShopWindow shop;
        private ShopUi ui;
        private RectTransform root;
        private RectTransform subfilters;
        private BuildCategory previous;
        private float width;
        private const string FloorsSection = "Floors & Ceilings";
        private const string DoorsSection = "Doors & Gates";
        private int subfilter;
        private static readonly string[] FloorLabels = { "All finishes", "Tile", "Wood", "Stone & Concrete", "Industrial", "Resilient", "Outdoor" };
        private static readonly string[] DoorLabels = { "All doors", "Swing", "Sliding", "Folding", "Revolving", "Vehicle gates" };
        public string Selected {get;private set;}="All";
        public bool ShowsFloors => shop.SelectedCategory == BuildCategory.Structures && Selected == FloorsSection;
        public bool ShowsDoors => shop.SelectedCategory == BuildCategory.Structures && Selected == DoorsSection;
        public bool IsCollectionEntry(BuildItemData item) => item != null &&
            (ShowsFloors ? item.usesMetricFloorPattern : ShowsDoors && item.prefab != null && item.prefab.GetComponent<CollectionDoor>() != null);
        public void Initialize(ShopWindow owner)
        {
            shop=owner;width=Screen.width<1100?784:1004;ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            root=ui.Rect("Build sections",shop.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-126),new Vector2(width,40));
            subfilters=ui.Rect("Construction filters",shop.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-165),new Vector2(width,34));
            shop.ViewChanged+=Refresh;Refresh();
        }
        private void OnDestroy(){if(shop!=null)shop.ViewChanged-=Refresh;}
        private void Refresh()
        {
            root.gameObject.SetActive(shop.IsBuildView);
            if(previous!=shop.SelectedCategory){Selected="All";subfilter=0;previous=shop.SelectedCategory;}
            bool showSubfilters=shop.IsBuildView && (ShowsFloors || ShowsDoors);
            subfilters.gameObject.SetActive(showSubfilters);
            shop.SetBuildSubfiltersVisible(showSubfilters);
            if(!shop.IsBuildView)return;
            foreach(Transform child in root){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            string[] sections=shop.SelectedCategory==BuildCategory.Shelf?new[]{"All","Shelves","Refrigeration","Freezers","Counters","Checkouts"}
                :shop.SelectedCategory==BuildCategory.Structures?new[]{"All","Walls",DoorsSection,FloorsSection,"Floor connections","Parking"}
                :new[]{"All","Plants","Seating","Lighting","Other"};
            for(int i=0;i<sections.Length;i++)
            {
                string section=sections[i];var button=ui.Button(root,section,new Vector2(i*width/sections.Length,0),new Vector2(width/sections.Length-6,34),()=>
                {
                    Selected=section;
                    subfilter=0;
                    shop.Refresh();
                    Refresh();
                });
                button.image.color=Selected==section?ShopUiTheme.Green:ShopUiTheme.Line;
                button.GetComponentInChildren<TMPro.TMP_Text>().fontSize=14;
            }
            if (!showSubfilters) return;
            var labels = ShowsFloors ? FloorLabels : DoorLabels;
            foreach (Transform child in subfilters) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            for (int i = 0; i < labels.Length; i++)
            {
                int group = i;
                float slot = width / labels.Length;
                var button = ui.Button(subfilters, labels[i], new Vector2(i * slot, 0), new Vector2(slot - 6, 28), () =>
                {
                    subfilter = group;
                    shop.Refresh();
                    Refresh();
                });
                button.image.color = group == subfilter ? ShopUiTheme.Green : ShopUiTheme.Line;
                button.GetComponentInChildren<TMPro.TMP_Text>().fontSize = 13;
            }
        }
        public bool Includes(BuildItemData item)
        {
            if(Selected=="All"||previous!=shop.SelectedCategory)return true;
            if (Section(item) != Selected) return false;
            if (subfilter == 0) return true;
            return ShowsFloors ? (int)item.floorFinishGroup == subfilter : ShowsDoors && DoorGroup(item) == subfilter;
        }
        private static int DoorGroup(BuildItemData item)
        {
            var door = item.prefab != null ? item.prefab.GetComponent<CollectionDoor>() : null;
            if (item.isUnloadingGate || door?.AllowsVehicles == true) return 5;
            return door?.Mechanism switch
            {
                DoorMechanism.Slide or DoorMechanism.Arc => 2,
                DoorMechanism.Fold => 3,
                DoorMechanism.Revolve => 4,
                _ => 1
            };
        }
        public static string Section(BuildItemData item)
        {
            string id=item.id.ToLowerInvariant();
            if(item.isCheckout)return "Checkouts";
            if(item.category==BuildCategory.Structures)
                return item.isParkingSpace?"Parking":item.storeyConnection!=StoreyConnectionKind.None?"Floor connections"
                    :item.isDoorway||item.isUnloadingGate?DoorsSection:item.isWall?"Walls"
                    :item.placementKind==PlacementKind.Floor?FloorsSection:"All";
            if(item.category==BuildCategory.Decoration)
                return id.Contains("plant")||id.Contains("flower")?"Plants":id.Contains("bench")||id.Contains("seat")?"Seating":id.Contains("lamp")||id.Contains("light")?"Lighting":"Other";
            return id.Contains("freezer")||id.Contains("cold_pantry")?"Freezers":id.Contains("chill")||id.Contains("refrigerat")?"Refrigeration"
                :id.Contains("counter")||id.Contains("stand")||id.Contains("bin")?"Counters":"Shelves";
        }
    }
}
