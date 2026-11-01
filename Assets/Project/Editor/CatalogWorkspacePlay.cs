using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Exercise catalog controls and multi-supplier basket through the installed gameplay UI.</summary>
[InitializeOnLoad]
public static class CatalogWorkspacePlay
{
    const string Pending="CatalogWorkspace.Play";
    const string Report="Library/CatalogWorkspace";
    static IEnumerator checks;
    static double deadline;
    static CatalogWorkspacePlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","RUNNING");
        var gameView=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
        gameView.position=new Rect(0,0,1500,900);gameView.Repaint();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){checks=Check();deadline=EditorApplication.timeSinceStartup+900;}
            Require(EditorApplication.timeSinceStartup<deadline,"Catalog UI checks timed out.");
            if(checks.MoveNext())return;Finish(null);
        }catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    static IEnumerator Check()
    {
        for(int i=0;i<8;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer present.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();var orders=starter.cityTrip.orders;orders.save=null;
        var shop=Object.FindObjectOfType<ShopWindow>(true);shop.gameObject.SetActive(true);shop.OpenProducts();yield return null;
        var browser=shop.ProductBrowser;
        int total=orders.productCatalog.Products.Count;
        Require(browser.VisibleCount==total,"Catalog omitted products.");
        browser.SelectDepartment("Fruit & Vegetables");Require(browser.VisibleCount>24&&browser.VisibleCount<total,"Department filter failed.");
        var input=shop.GetComponentInChildren<TMP_InputField>();input.text="quinoa";
        Require(browser.VisibleCount==1,"Global search did not cross department boundary.");
        input.text="not-a-product-123";Require(browser.VisibleCount==0,"Search empty state failed.");input.text="";
        browser.SelectDepartment("All products");
        var first=orders.productCatalog.GetById("product_quinoa");var second=orders.productCatalog.GetById("product_pumpkin");
        Pick(shop,"Supplier","Farmers market");
        Require(Visible(shop).All(p=>orders.productCatalog.GetById(p.name).StorageType==ProductStorageType.Produce),"Supplier filter is incorrect.");
        Pick(shop,"Supplier","All suppliers");Pick(shop,"Equipment","Refrigerated display");
        Require(Visible(shop).All(p=>{var type=orders.productCatalog.GetById(p.name).StorageType;return type==ProductStorageType.Dairy||type==ProductStorageType.Meat||type==ProductStorageType.ReadyMeals;}),"Cold equipment filter is incorrect.");
        Pick(shop,"Equipment","All equipment");Pick(shop,"Sort","Pack price");
        var prices=Visible(shop).Select(p=>orders.productCatalog.GetById(p.name).BuyPrice).ToArray();
        Require(prices.SequenceEqual(prices.OrderBy(p=>p)),"Price sorting failed.");Pick(shop,"Sort","Name");
        bool favorite=browser.IsFavorite(first);if(!favorite)browser.ToggleFavorite(first,false);
        Pick(shop,"Stock","Favorites");Require(Visible(shop).Any(p=>p.name==first.Id),"Favorite filter omitted saved item.");
        Pick(shop,"Stock","All stock");if(!favorite)browser.ToggleFavorite(first,false);
        browser.SelectDepartment("Pantry");Pick(shop,"Sections","Spices");
        Require(browser.VisibleCount>=6,"Spice subsection is incomplete.");Pick(shop,"Sections","All sections");browser.SelectDepartment("All products");
        browser.ClearBasket();browser.AddToCart(first,2);browser.AddToCart(second,3);
        int totalPrice=first.BuyPrice*2+second.BuyPrice*3;
        orders.money.SetMoney(totalPrice-1);Require(!browser.SubmitOrder()&&orders.Ledger.Entries.Count==0&&browser.Basket.Count==2,"Insufficient funds caused partial purchase.");
        orders.money.SetMoney(totalPrice);float capacity=orders.depotCapacityKg;orders.depotCapacityKg=.001f;
        Require(!browser.SubmitOrder()&&orders.money.Money==totalPrice&&orders.Ledger.Entries.Count==0,"Supplier capacity caused partial purchase.");orders.depotCapacityKg=capacity;
        Require(browser.SubmitOrder()&&orders.money.Money==0&&browser.Basket.Count==0,"Basket commit failed.");
        Require(orders.Ledger.Count(first.Id,DeliveryItemKind.Product,DeliveryLocation.Depot)==first.BoxAmount*2&&orders.Ledger.Count(second.Id,DeliveryItemKind.Product,DeliveryLocation.Depot)==second.BoxAmount*3,"Basket quantities wrong.");

        browser.SelectDepartment("Pantry");shop.Close();shop.gameObject.SetActive(true);yield return null;
        Require(shop.IsProductsView&&browser.Department=="Pantry","Reopening reset browsing state.");
        orders.money.SetMoney(100000);yield return null;
        ScreenCapture.CaptureScreenshot(Report+"/products.png");for(int i=0;i<12;i++)yield return null;
        browser.AddToCart(first,2);
        shop.GetComponentsInChildren<Button>().Single(b=>b.name=="Review order").onClick.Invoke();yield return null;
        ScreenCapture.CaptureScreenshot(Report+"/basket.png");for(int i=0;i<12;i++)yield return null;
        var close=Object.FindObjectsOfType<Button>().First(b=>b.name=="Close"&&b.transform.parent.name=="Review product order");close.onClick.Invoke();yield return null;
        shop.OpenCategory_Shelves();yield return null;ScreenCapture.CaptureScreenshot(Report+"/equipment.png");for(int i=0;i<12;i++)yield return null;
        Require(shop.GetComponentsInChildren<ShopItemCard>().Any(c=>c.nameText.text.ToLowerInvariant().Contains("checkout")),"Checkout missing from equipment.");
        shop.OpenCategory_Structures();yield return null;
        Require(!shop.GetComponentsInChildren<ShopItemCard>().Any(c=>c.nameText.text.ToLowerInvariant().Contains("checkout")),"Checkout remains in construction.");
        shop.OpenStaff();yield return null;ScreenCapture.CaptureScreenshot(Report+"/staff.png");for(int i=0;i<12;i++)yield return null;
        shop.OpenVehicles();yield return null;ScreenCapture.CaptureScreenshot(Report+"/vehicles.png");for(int i=0;i<12;i++)yield return null;
        browser.ClearBasket();Require(!browser.SubmitOrder(),"Empty basket accepted.");
        Require(File.Exists(Report+"/products.png")&&File.Exists(Report+"/basket.png"),"Screenshots missing.");
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static Transform[] Visible(ShopWindow shop)=>shop.transform.Find("Product browser/Product results/Content").Cast<Transform>().Where(t=>t.gameObject.activeSelf&&t.name.StartsWith("product_")).ToArray();
    static void Pick(ShopWindow shop,string control,string option)
    {
        shop.GetComponentsInChildren<Button>().Single(b=>b.name==control&&b.transform.parent.name=="Product browser").onClick.Invoke();
        Object.FindObjectsOfType<Button>().Single(b=>b.name==option&&b.transform.parent.parent.name=="Options").onClick.Invoke();
    }
    static void Finish(Exception error)
    {
        SessionState.SetBool(Pending,false);checks=null;Time.timeScale=1;
        File.WriteAllText(Report+"/result.txt",error==null?"PASS: full catalog, taxonomy, global search, empty state, multi-supplier basket, insufficient funds/capacity atomic rejection, exact payment/quantities, empty basket, reopen state, checkout grouping and screenshots.":"FAIL\n"+error);
        EditorApplication.isPlaying=false;EditorApplication.Exit(error==null?0:1);
    }
}
