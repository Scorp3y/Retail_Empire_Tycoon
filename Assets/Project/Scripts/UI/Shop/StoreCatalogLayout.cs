using System;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Six shopping departments, preserving installed scenes and their references.</summary>
    public static class StoreCatalogLayout
    {
        public static void Apply(ShopWindow shop)
        {
            var fit=shop.GetComponent<ShopPanelFit>();
            float width=Screen.width<1100?820:1040;
            if(fit!=null)fit.MaximumSize=new Vector2(width,650);
            var navigation=shop.GetComponent<ShopWindowNavigation>();
            if(navigation==null)return;
            foreach(var button in navigation.tabs)if(button!=null){button.gameObject.SetActive(false);UnityEngine.Object.Destroy(button.gameObject);}
            var ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            string[] titles={"Products","Equipment","Construction","Staff","Vehicles","Decor"};
            Action[] actions={shop.OpenProducts,shop.OpenCategory_Shelves,shop.OpenCategory_Structures,shop.OpenStaff,shop.OpenVehicles,shop.OpenCategory_Decoration};
            navigation.tabs=new Button[6];
            for(int i=0;i<6;i++)navigation.tabs[i]=ui.Button(shop.transform,titles[i],new Vector2(18+i*(width-36)/6,-76),new Vector2((width-36)/6-4,36),actions[i]);
            foreach(var button in navigation.categories)if(button!=null)button.gameObject.SetActive(false);
        }
    }
}
