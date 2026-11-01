using System.Linq;
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
        private BuildCategory previous;
        private float width;
        public string Selected {get;private set;}="All";
        public void Initialize(ShopWindow owner)
        {
            shop=owner;width=Screen.width<1100?784:1004;ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            root=ui.Rect("Build sections",shop.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-126),new Vector2(width,40));
            shop.ViewChanged+=Refresh;Refresh();
        }
        private void OnDestroy(){if(shop!=null)shop.ViewChanged-=Refresh;}
        private void Refresh()
        {
            root.gameObject.SetActive(shop.IsBuildView);if(!shop.IsBuildView)return;
            if(previous!=shop.SelectedCategory){Selected="All";previous=shop.SelectedCategory;}
            foreach(Transform child in root){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            string[] sections=shop.SelectedCategory==BuildCategory.Shelf?new[]{"All","Shelves","Refrigeration","Freezers","Counters","Checkouts"}
                :shop.SelectedCategory==BuildCategory.Structures?new[]{"All","Walls","Doors & Gates","Floors & Ceilings","Floor connections","Parking"}
                :new[]{"All","Plants","Seating","Lighting","Other"};
            for(int i=0;i<sections.Length;i++)
            {
                string section=sections[i];var button=ui.Button(root,section,new Vector2(i*width/sections.Length,0),new Vector2(width/sections.Length-6,34),()=>{Selected=section;shop.Refresh();});
                button.image.color=Selected==section?ShopUiTheme.Green:ShopUiTheme.Line;
                button.GetComponentInChildren<TMPro.TMP_Text>().fontSize=14;
            }
        }
        public bool Includes(BuildItemData item)
        {
            if(Selected=="All"||previous!=shop.SelectedCategory)return true;
            return Section(item)==Selected;
        }
        public static string Section(BuildItemData item)
        {
            string id=item.id.ToLowerInvariant();
            if(item.isCheckout)return "Checkouts";
            if(item.category==BuildCategory.Structures)
                return item.isParkingSpace?"Parking":item.storeyConnection!=StoreyConnectionKind.None?"Floor connections"
                    :item.isDoorway||item.isUnloadingGate?"Doors & Gates":item.isWall?"Walls":"Floors & Ceilings";
            if(item.category==BuildCategory.Decoration)
                return id.Contains("plant")||id.Contains("flower")?"Plants":id.Contains("bench")||id.Contains("seat")?"Seating":id.Contains("lamp")||id.Contains("light")?"Lighting":"Other";
            return id.Contains("freezer")||id.Contains("cold_pantry")?"Freezers":id.Contains("chill")||id.Contains("refrigerat")?"Refrigeration"
                :id.Contains("counter")||id.Contains("stand")||id.Contains("bin")?"Counters":"Shelves";
        }
    }
}
