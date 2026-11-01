using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Product browsing and draft basket. Paid orders remain owned by DeliveryOrders.</summary>
    public sealed class StoreProductBrowser : MonoBehaviour
    {
        private ShopUi ui;
        private RectTransform panel, sidebar, content;
        private ScrollRect scroll;
        private RectTransform activeModal;
        private TMP_Text count, summary;
        private TMP_InputField search;
        private IReadOnlyList<ProductItemData> products;
        private MoneyController money;
        private ProductInventory inventory;
        private DeliveryOrders orders;
        private ProductDepartments departments;
        private readonly Dictionary<ProductItemData,int> basket = new Dictionary<ProductItemData,int>();
        private readonly HashSet<string> favorites = new HashSet<string>();
        private readonly List<PlacedShelfStock> observedShelves=new List<PlacedShelfStock>();
        private string department="All products", section="All sections", query="";
        private int sort, filter, supplier, equipment;
        private bool ready,stockDirty;
        private int columns=3;
        private float resultWidth=800;
        private readonly Dictionary<ProductItemData,TMP_Text> stockLabels=new Dictionary<ProductItemData,TMP_Text>();
        private readonly string[] sorts={"Name","Pack price","Unit price","Profit per unit"};
        private readonly string[] filters={"All stock","In stock","Out of stock","On order","Favorites"};
        private readonly string[] suppliers={"All suppliers","Farmers market","Bakery","Wholesale depot"};
        private readonly string[] equipmentFilters={"All equipment","Fits my equipment","Produce rack","Bakery rack","Refrigerated display","Ambient shelves"};
        public IReadOnlyDictionary<ProductItemData,int> Basket => basket;
        public int VisibleCount { get; private set; }
        public string Department => department;

        public void Initialize(ShopWindow owner, IReadOnlyList<ProductItemData> items, MoneyController wallet, ProductInventory stock, DeliveryOrders delivery)
        {
            products=items;money=wallet;inventory=stock;orders=delivery;
            float width=Screen.width<1100?784:1004;columns=Screen.width<1100?2:3;resultWidth=width-204;float controlWidth=resultWidth/4;
            departments=new ProductDepartments();ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            foreach(var id in PlayerPrefs.GetString("Store.FavoriteProducts","").Split('|')) if(!string.IsNullOrEmpty(id))favorites.Add(id);
            panel=ui.Rect("Product browser",owner.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-126),new Vector2(width,486));
            sidebar=Scroll(panel,"Departments",new Vector2(0,-42),new Vector2(190,390),out _);
            content=Scroll(panel,"Product results",new Vector2(204,-126),new Vector2(resultWidth,306),out scroll);
            search=Input(panel,new Vector2(204,0),new Vector2(resultWidth,36));search.onValueChanged.AddListener(value=>{query=value.Trim();RefreshResults(true);});
            ui.Button(panel,"Sections",new Vector2(204,-42),new Vector2(controlWidth-6,32),()=>Choose(departmentsForSelection(),value=>{section=value;RefreshResults(true);}));
            ui.Button(panel,"Sort",new Vector2(204+controlWidth,-42),new Vector2(controlWidth-6,32),()=>Choose(sorts,value=>{sort=Array.IndexOf(sorts,value);RefreshResults(true);}));
            ui.Button(panel,"Stock",new Vector2(204+controlWidth*2,-42),new Vector2(controlWidth-6,32),()=>Choose(filters,value=>{filter=Array.IndexOf(filters,value);RefreshResults(true);}));
            ui.Button(panel,"Supplier",new Vector2(204+controlWidth*3,-42),new Vector2(controlWidth-6,32),()=>Choose(suppliers,value=>{supplier=Array.IndexOf(suppliers,value);RefreshResults(true);}));
            ui.Button(panel,"Equipment",new Vector2(204,-82),new Vector2(188,32),()=>Choose(equipmentFilters,value=>{equipment=Array.IndexOf(equipmentFilters,value);RefreshResults(true);}));
            count=ui.Label(panel,"",new Vector2(400,-84),new Vector2(width-400,34),14);
            summary=ui.Label(panel,"",new Vector2(0,-444),new Vector2(width-230,40),16);
            ui.Button(panel,"Review order",new Vector2(width-204,-444),new Vector2(204,36),ReviewBasket);
            ready=true;panel.gameObject.SetActive(false);
            if(money!=null)money.Changed+=MoneyChanged;
            if(inventory!=null)inventory.Changed+=StockChanged;
            if(orders!=null)orders.Changed+=StockChanged;
        }
        private void OnDisable(){ObserveShelves(false);if(activeModal!=null)ui.CloseModal(activeModal);}
        private void OnDestroy()
        {
            ObserveShelves(false);
            if(money!=null)money.Changed-=MoneyChanged;
            if(inventory!=null)inventory.Changed-=StockChanged;
            if(orders!=null)orders.Changed-=StockChanged;
        }
        private void MoneyChanged(int value){ UpdateSummary(); }
        private void StockChanged(){stockDirty=true;}
        private void ShelfChanged(PlacedShelfStock shelf){stockDirty=true;}
        private void ObserveShelves(bool observe)
        {
            foreach(var shelf in observedShelves)if(shelf!=null)shelf.Changed-=ShelfChanged;
            observedShelves.Clear();
            if(!observe)return;
            observedShelves.AddRange(FindObjectsOfType<PlacedShelfStock>());
            foreach(var shelf in observedShelves)shelf.Changed+=ShelfChanged;
        }
        private void LateUpdate()
        {
            if(!ready||!panel.gameObject.activeSelf||!stockDirty)return;stockDirty=false;
            if(filter>=1&&filter<=3){RefreshResults(false);return;}
            var shelves=FindObjectsOfType<PlacedShelfStock>();
            foreach(var pair in stockLabels)pair.Value.text=StockText(pair.Key,shelves.Where(s=>s.CurrentProduct==pair.Key).Sum(s=>s.CurrentAmount));
        }
        private string StockText(ProductItemData product,int onShelf)=>$"Warehouse: {inventory.GetCount(product)} · Shelf: {onShelf}\nOn order: {Awaiting(product)} · {ShopText.ShelfHint(product)}";
        public void Show(bool visible)
        {
            if(!ready)return;
            panel.gameObject.SetActive(visible);
            ObserveShelves(visible);
            if(visible){RebuildDepartments();RefreshResults(false);}
        }
        public void SelectDepartment(string value)
        {
            department=value;section="All sections";RebuildDepartments();RefreshResults(true);
        }
        private IEnumerable<string> departmentsForSelection()=>new[]{"All sections"}.Concat(products.Where(p=>department=="All products"||departments.For(p).department==department).Select(p=>departments.For(p).section).Distinct().OrderBy(s=>s));
        private void RebuildDepartments()
        {
            Clear(sidebar);int index=0;
            foreach(var name in new[]{"All products"}.Concat(products.Select(p=>departments.For(p).department).Distinct().OrderBy(s=>s)))
            {
                int quantity=products.Count(p=>name=="All products"||departments.For(p).department==name);
                var button=ui.Button(sidebar,name+" ("+quantity+")",new Vector2(0,-index++*42),new Vector2(182,38),()=>SelectDepartment(name));
                button.GetComponentInChildren<TMP_Text>().fontSize=14;
                button.image.color=name==department?ShopUiTheme.Green:ShopUiTheme.Line;
            }
            sidebar.sizeDelta=new Vector2(sidebar.sizeDelta.x,index*42);
        }
        public void RefreshResults(bool reset)
        {
            if(!ready)return;
            float position=scroll.verticalNormalizedPosition;stockLabels.Clear();Clear(content);
            var shelves=FindObjectsOfType<PlacedShelfStock>();
            var stock=shelves.Where(s=>s.CurrentProduct!=null).GroupBy(s=>s.CurrentProduct.Id).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.CurrentAmount));
            IEnumerable<ProductItemData> result=products.Where(p=>
                (string.IsNullOrWhiteSpace(query)?department=="All products"||departments.For(p).department==department: p.DisplayName.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)
                && (!string.IsNullOrWhiteSpace(query)||section=="All sections"||departments.For(p).section==section)
                && (filter!=1||inventory.GetCount(p)>0||stock.ContainsKey(p.Id)) && (filter!=2||(inventory.GetCount(p)==0&&!stock.ContainsKey(p.Id)))
                && (filter!=3||Awaiting(p)>0) && (filter!=4||favorites.Contains(p.Id))
                && (supplier==0||SupplierRouting.Name(SupplierRouting.ForProduct(p.StorageType))==suppliers[supplier])
                && (equipment==0||equipment==1&&shelves.Any(s=>s.IsProductAllowed(p))
                    ||equipment==2&&p.StorageType==ProductStorageType.Produce||equipment==3&&p.StorageType==ProductStorageType.Bakery
                    ||equipment==4&&(p.StorageType==ProductStorageType.Dairy||p.StorageType==ProductStorageType.Meat||p.StorageType==ProductStorageType.ReadyMeals)
                    ||equipment==5&&(p.StorageType==ProductStorageType.Groceries||p.StorageType==ProductStorageType.Oil||p.StorageType==ProductStorageType.Drinks||p.StorageType==ProductStorageType.Preserves||p.StorageType==ProductStorageType.Snacks)));
            result=sort==1?result.OrderBy(p=>p.BuyPrice).ThenBy(p=>p.DisplayName):sort==2?result.OrderBy(p=>(float)p.BuyPrice/p.BoxAmount).ThenBy(p=>p.DisplayName):sort==3?result.OrderByDescending(p=>p.SellPrice-(float)p.BuyPrice/p.BoxAmount).ThenBy(p=>p.DisplayName):result.OrderBy(p=>p.DisplayName);
            int i=0;
            foreach(var product in result){stock.TryGetValue(product.Id,out int onShelf);Card(product,i++,onShelf);}
            VisibleCount=i;content.sizeDelta=new Vector2(content.sizeDelta.x,Mathf.Ceil(i/(float)columns)*250);
            count.text=$"{(string.IsNullOrWhiteSpace(query)?department:"Search results")} · {i} products · {sorts[sort]}\n{section} · {filters[filter]} · {suppliers[supplier]} · {equipmentFilters[equipment]}";
            if(i==0)ui.Label(content,"No products match. Change the filters or search.",Vector2.zero,new Vector2(750,60),18);
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=reset?1:position;UpdateSummary();
        }
        private void Card(ProductItemData product,int index,int onShelf)
        {
            var card=ui.Rect(product.Id,content,new Vector2(0,1),new Vector2(0,1),new Vector2(index%columns*(resultWidth/columns),-index/columns*250),new Vector2(256,240));
            ui.Panel(card,ShopUiTheme.Paper);ui.Border(card);
            ui.Label(card,product.DisplayName,new Vector2(10,-8),new Vector2(208,40),17);
            var star=ui.Button(card,"",new Vector2(220,-8),new Vector2(28,28),()=>ToggleFavorite(product));
            ui.Icon(star.transform,ShopIcon.Star,Vector2.zero,new Vector2(18,18),favorites.Contains(product.Id)?ShopUiTheme.Gold:ShopUiTheme.Muted);
            var icon=ui.Rect("Product image",card,new Vector2(0,1),new Vector2(0,1),new Vector2(8,-50),new Vector2(90,90)).gameObject.AddComponent<Image>();icon.sprite=product.Icon;icon.preserveAspect=true;icon.raycastTarget=false;
            ui.Label(card,$"Pack: {product.BoxAmount}\n${product.BuyPrice} / pack\n${(float)product.BuyPrice/product.BoxAmount:0.##} / unit\nSell: ${product.SellPrice}",new Vector2(108,-50),new Vector2(142,86),14);
            stockLabels[product]=ui.Label(card,StockText(product,onShelf),new Vector2(10,-142),new Vector2(238,44),12);
            int packs=1;var quantity=ui.Label(card,"1",new Vector2(42,-201),new Vector2(32,28),17);quantity.alignment=TextAlignmentOptions.Center;
            ui.Button(card,"-",new Vector2(10,-200),new Vector2(30,30),()=>{packs=Math.Max(1,packs-1);quantity.text=packs.ToString();});
            ui.Button(card,"+",new Vector2(76,-200),new Vector2(30,30),()=>{packs=Math.Min(999,packs+1);quantity.text=packs.ToString();});
            ui.Button(card,"Add",new Vector2(118,-200),new Vector2(128,30),()=>AddToCart(product,packs));
        }
        private int Awaiting(ProductItemData product)=>orders==null?0:orders.Ledger.Entries.Where(e=>e.kind==DeliveryItemKind.Product&&e.itemId==product.Id).Sum(e=>e.quantity);
        public bool IsFavorite(ProductItemData product)=>favorites.Contains(product.Id);
        public void ToggleFavorite(ProductItemData product,bool persist=true)
        {
            if(!favorites.Add(product.Id))favorites.Remove(product.Id);
            if(persist){PlayerPrefs.SetString("Store.FavoriteProducts",string.Join("|",favorites));PlayerPrefs.Save();}
            RefreshResults(false);
        }
        public void AddToCart(ProductItemData product,int packs)
        {
            if(product==null||!products.Contains(product)||packs<1||packs>999)throw new ArgumentException("Invalid basket line.");
            basket.TryGetValue(product,out int current);basket[product]=Math.Min(999,current+packs);UpdateSummary();
        }
        public bool SubmitOrder()
        {
            if(orders==null||!orders.OrderProducts(basket))return false;
            basket.Clear();UpdateSummary();return true;
        }
        public void ClearBasket(){basket.Clear();UpdateSummary();}
        private void UpdateSummary()
        {
            if(summary==null)return;
            summary.text=$"Basket: {basket.Values.Sum()} packs · ${basket.Sum(l=>(long)l.Key.BuyPrice*l.Value):N0}\n{basket.Sum(l=>l.Key.UnitWeightKg*l.Key.BoxAmount*l.Value):0.#} kg · {basket.Sum(l=>l.Key.UnitVolumeM3*l.Key.BoxAmount*l.Value):0.###} m³ · Balance: ${money.Money:N0}";
        }
        private void ReviewBasket()
        {
            var modal=activeModal=ui.Modal("Review product order",GetComponentInParent<Canvas>().transform,new Vector2(720,560));
            ui.Heading(modal,"Review order","Review order",new Vector2(20,-14),new Vector2(610,34),24);
            ui.Button(modal,"Close",new Vector2(610,-14),new Vector2(90,32),()=>ui.CloseModal(modal));
            var lines=Scroll(modal,"Basket lines",new Vector2(20,-60),new Vector2(680,350),out _);int i=0;
            foreach(var line in basket.ToArray())
            {
                var product=line.Key;
                ui.Label(lines,$"{product.DisplayName} × {line.Value} packs · ${product.BuyPrice*(long)line.Value}\n{SupplierRouting.Name(SupplierRouting.ForProduct(product.StorageType))}",new Vector2(0,-i*58),new Vector2(540,54),15);
                ui.Button(lines,"Remove",new Vector2(550,-i++*58),new Vector2(112,32),()=>{basket.Remove(product);ui.CloseModal(modal);ReviewBasket();UpdateSummary();});
            }
            lines.sizeDelta=new Vector2(680,i*58);
            var notice=ui.Label(modal,summary.text,new Vector2(20,-420),new Vector2(680,62),16);
            var pay=ui.Button(modal,"Pay & order",new Vector2(480,-500),new Vector2(220,40),()=>{if(SubmitOrder())ui.CloseModal(modal);else notice.text=orders!=null?orders.Notice:"Delivery service unavailable.";});
            pay.interactable=basket.Count>0&&basket.Sum(l=>(long)l.Key.BuyPrice*l.Value)<=money.Money;
            ui.Button(modal,"Clear basket",new Vector2(20,-500),new Vector2(160,40),()=>{ClearBasket();ui.CloseModal(modal);});
        }
        private void Choose(IEnumerable<string> values,Action<string> select)
        {
            var options=values.ToArray();var modal=activeModal=ui.Modal("Catalog options",GetComponentInParent<Canvas>().transform,new Vector2(420,Math.Min(570,options.Length*40+70)));
            ui.Button(modal,"Close",new Vector2(300,-12),new Vector2(100,30),()=>ui.CloseModal(modal));
            var list=Scroll(modal,"Options",new Vector2(16,-50),new Vector2(388,Math.Min(500,options.Length*40)),out _);int i=0;
            foreach(var value in options)ui.Button(list,value,new Vector2(0,-i++*40),new Vector2(376,36),()=>{select(value);ui.CloseModal(modal);});
            list.sizeDelta=new Vector2(388,options.Length*40);
        }
        private TMP_InputField Input(Transform parent,Vector2 position,Vector2 size)
        {
            var root=ui.Rect("Search products",parent,new Vector2(0,1),new Vector2(0,1),position,size);ui.Panel(root,ShopUiTheme.Paper);ui.Border(root);
            var field=root.gameObject.AddComponent<TMP_InputField>();var label=ui.Label(root,"",new Vector2(10,-4),size-new Vector2(20,8),17);
            var hint=ui.Label(root,"Search all products...",new Vector2(10,-4),size-new Vector2(20,8),17);hint.color=ShopUiTheme.Muted;
            field.textViewport=root;field.textComponent=(TextMeshProUGUI)label;field.placeholder=hint;field.lineType=TMP_InputField.LineType.SingleLine;return field;
        }
        private RectTransform Scroll(Transform parent,string name,Vector2 position,Vector2 size,out ScrollRect scrolling)
        {
            var viewport=ui.Rect(name,parent,new Vector2(0,1),new Vector2(0,1),position,size);ui.Panel(viewport,new Color(0,0,0,.001f));viewport.gameObject.AddComponent<RectMask2D>();
            var body=ui.Rect("Content",viewport,new Vector2(0,1),new Vector2(0,1),Vector2.zero,size);
            scrolling=viewport.gameObject.AddComponent<ScrollRect>();scrolling.content=body;scrolling.viewport=viewport;scrolling.horizontal=false;scrolling.movementType=ScrollRect.MovementType.Clamped;scrolling.scrollSensitivity=30;
            var track=ui.Rect("Scrollbar",viewport,new Vector2(1,1),new Vector2(1,1),Vector2.zero,new Vector2(7,size.y));ui.Panel(track,ShopUiTheme.Line);
            var handle=ui.Rect("Handle",track,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            var graphic=ui.Panel(handle,ShopUiTheme.Green);var bar=track.gameObject.AddComponent<Scrollbar>();bar.targetGraphic=graphic;bar.handleRect=handle;bar.direction=Scrollbar.Direction.BottomToTop;
            scrolling.verticalScrollbar=bar;
            return body;
        }
        private static void Clear(Transform parent){foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
    }
}
