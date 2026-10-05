using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.HUD;
using RetailEmpireTycoon.UI.Products;
using RetailEmpireTycoon.UI.Shop;
using RetailEmpireTycoon.UI.Windows;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit, repeatable UI migration. Never runs on import or changes the player save.</summary>
public static class ShopUiSetup
{
    public const string ArtFolder = "Assets/Art/ShopUi";
    public const string PrefabFolder = "Assets/Prefabs/ShopUi";
    private static ShopUi _ui;
    private static ShopUiTheme _theme;

    [MenuItem("Retail Empire/UI/Apply shop UI redesign %#F1")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != "Assets/Project/Scenes/Game.unity")
            throw new InvalidOperationException("Open the saved Game scene in Edit Mode before applying UI.");
        _theme = PrepareTheme(); _ui = new ShopUi(_theme);
        ShopUiPreviews.Generate();
        Directory.CreateDirectory(PrefabFolder);
        var shop = Object.FindObjectOfType<ShopWindow>(true);
        var inventory = Object.FindObjectOfType<BuildInventoryWindow>(true);
        var settings = Object.FindObjectOfType<ControlPanel>(true);
        if (shop == null || inventory == null || settings == null) throw new InvalidOperationException("Missing game UI controllers.");

        UnpackUi(shop.gameObject); UnpackUi(inventory.gameObject); UnpackUi(settings.settingPanel);
        HideLegacyHud(shop, inventory, settings);
        CreateCards(shop, inventory);
        BuildShop(shop); BuildInventory(inventory); BuildSettings(settings);
        UpdateSupportingPanels();
        var operations = Object.FindObjectOfType<ShopOperationsHud>(true);
        Set(operations, "employeePortrait", _theme.employeePortrait);
        var gameplayCanvas = (Canvas)Read(operations, "canvas");
        var edge = operations.GetComponent<ShopHudPresenter>() ?? operations.gameObject.AddComponent<ShopHudPresenter>();
        edge.canvas = gameplayCanvas; edge.money = Object.FindObjectOfType<MoneyController>(true);
        edge.controls = Object.FindObjectOfType<GameplayControls>(true); edge.windows = Object.FindObjectOfType<HUDController>(true);
        edge.inventory = inventory; edge.territory = Object.FindObjectOfType<TerritoryPurchaseModeManager>(true);
        edge.settings = settings; edge.work = Object.FindObjectOfType<WorkMinigame>(true); edge.pause = Object.FindObjectOfType<PauseManager>(true);
        shop.gameObject.SetActive(false); inventory.gameObject.SetActive(false); settings.settingPanel.SetActive(false);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Library/ShopUiQA");
        File.WriteAllText("Library/ShopUiQA/setup.txt", "Applied unified shop UI, cards, font, icons, settings and edge HUD. Player save untouched.");
        Debug.Log("Shop UI redesign applied. Play tests must use the isolated gameplay sandbox.");
    }

    private static ShopUiTheme PrepareTheme()
    {
        Directory.CreateDirectory("Assets/Resources/ShopUi"); AssetDatabase.Refresh();
        var theme = AssetDatabase.LoadAssetAtPath<ShopUiTheme>("Assets/Resources/ShopUi/Theme.asset");
        if (theme == null) { theme = ScriptableObject.CreateInstance<ShopUiTheme>(); AssetDatabase.CreateAsset(theme, "Assets/Resources/ShopUi/Theme.asset"); }
        theme.regularFont = PrepareFont("Regular");
        theme.headingFont = PrepareFont("Medium");
        theme.regularFont.fontWeightTable[7].regularTypeface = theme.headingFont;
        EditorUtility.SetDirty(theme.regularFont);
        theme.roundedPanel = RoundedSprite();
        theme.employeePortrait = ImportSprite(ArtFolder + "/EmployeePortrait.png", Vector4.zero);
        theme.staffPortraits = new[] { "Cashier", "Guard", "Stocker", "Cleaner" }
            .Select(role => ImportSprite(ArtFolder + "/Staff/" + role + ".png", Vector4.zero)).ToArray();
        EditorUtility.SetDirty(theme); return theme;
    }
    private static TMP_FontAsset PrepareFont(string weight)
    {
        string fontPath = ArtFolder + "/Fonts/Rubik-" + weight + ".ttf";
        var source = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (source == null) throw new InvalidOperationException("Rubik source font is required: " + fontPath);
        string assetPath = "Assets/Resources/ShopUi/Rubik" + weight + ".asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset(source); font.name = "Rubik " + weight + " Shop UI"; font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            string characters = string.Concat(Enumerable.Range(32, 95).Select(c => (char)c))
                + "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя×—–·›…";
            if (!font.TryAddCharacters(characters, out var missing) || !string.IsNullOrEmpty(missing))
                throw new InvalidOperationException("Rubik is missing required UI characters: " + missing);
            AssetDatabase.CreateAsset(font, assetPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        }
        return font;
    }
    private static Sprite RoundedSprite()
    {
        string path = ArtFolder + "/RoundedPanel.png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                Vector2 p = new Vector2(x - 31.5f, y - 31.5f);
                Vector2 q = new Vector2(Mathf.Max(Mathf.Abs(p.x) - 20, 0), Mathf.Max(Mathf.Abs(p.y) - 20, 0));
                float alpha = Mathf.Clamp01(12 - q.magnitude);
                texture.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
        }
        return ImportSprite(path, new Vector4(14,14,14,14));
    }
    private static Sprite ImportSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing UI art: " + path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.spriteBorder = border;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 1024;
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static void UnpackUi(GameObject target)
    {
        var root = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
        if (root != null) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }
    private static void HideLegacyHud(ShopWindow shop, BuildInventoryWindow inventory, ControlPanel settings)
    {
        // Preserve contextual and tutorial UI. Only replace the legacy screen-space HUD artwork/actions.
        var preserved = new System.Collections.Generic.List<Transform> { shop.transform, inventory.transform, settings.settingPanel.transform };
        foreach (var shelf in Object.FindObjectsOfType<RetailEmpireTycoon.Shelves.ShelfInfoWindow>(true)) preserved.Add(shelf.transform);
        foreach (var confirm in Object.FindObjectsOfType<ConfirmPurchaseUI>(true)) preserved.Add(confirm.transform);
        foreach (var tutorial in Object.FindObjectsOfType<TutorialController>(true))
        {
            if (tutorial.tutorialUI != null) preserved.Add(tutorial.tutorialUI.transform);
            if (tutorial.tutorialChoiceUI != null) preserved.Add(tutorial.tutorialChoiceUI.transform);
        }
        var operations = Object.FindObjectOfType<ShopOperationsHud>(true);
        foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.transform.IsChildOf(operations.transform)) continue;
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                if (preserved.Any(p => graphic.transform.IsChildOf(p)) || graphic.GetComponentInParent<ScreenFader>() != null || graphic.GetComponentInParent<SceneFader>() != null) continue;
                graphic.enabled = false;
                var button = graphic.GetComponent<Button>(); if (button != null) button.enabled = false;
            }
        }
    }
    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount-1;i>=0;i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
    }
    private static void PrepareWindow(RectTransform root, Vector2 maximumSize, bool rightAligned)
    {
        var canvas = root.GetComponentInParent<Canvas>();
        root.SetParent(canvas.transform, false); root.localScale = Vector3.one; root.localRotation = Quaternion.identity;
        root.anchorMin = root.anchorMax = root.pivot = rightAligned ? new Vector2(1,.5f) : new Vector2(.5f,.5f);
        root.anchoredPosition = rightAligned ? new Vector2(-18,0) : Vector2.zero; root.sizeDelta = maximumSize;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f; }
        ClearChildren(root);
        var image = root.GetComponent<Image>(); if (image != null) Object.DestroyImmediate(image);
        _ui.Panel(root, ShopUiTheme.Paper); _ui.Border(root);
        var fit = root.GetComponent<ShopPanelFit>() ?? root.gameObject.AddComponent<ShopPanelFit>(); fit.MaximumSize = maximumSize;
    }
    private static RectTransform Area(Transform parent, string name, float top, float bottom = 18)
    {
        var rect = _ui.Rect(name,parent,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
        rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(18,bottom); rect.offsetMax = new Vector2(-18,-top); return rect;
    }
    private static Button Link(Transform parent, string ru, string en, Vector2 position, Vector2 size, UnityAction action)
    {
        var button = _ui.Button(parent,ru,en,position,size,null);
        UnityEventTools.AddPersistentListener(button.onClick, action); return button;
    }
    private static Button LinkIcon(Transform parent, ShopIcon icon, Vector2 position, float size, UnityAction action)
    {
        var button = _ui.IconButton(parent,icon,position,size,null);
        UnityEventTools.AddPersistentListener(button.onClick, action); return button;
    }
    private static void Header(RectTransform root, string ru, string en, UnityAction close, UnityAction back = null)
    {
        var header = _ui.Rect("Window heading",root,new Vector2(0,1),new Vector2(0,1),new Vector2(8,-8),new Vector2(root.sizeDelta.x-16,54));
        header.anchorMax = new Vector2(1,1); header.sizeDelta = new Vector2(-16,54); _ui.Panel(header,ShopUiTheme.Ink);
        var heading = _ui.Heading(header,ru,en,new Vector2(back != null ? 64 : 16,-9),new Vector2(400,38),28); heading.color = ShopUiTheme.Paper;
        var closeButton = LinkIcon(header,ShopIcon.Close,Vector2.zero,38,close);
        var rect = (RectTransform)closeButton.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-8,-8);
        closeButton.image.color=ShopUiTheme.Ink; closeButton.GetComponentInChildren<ShopIconGraphic>().color=ShopUiTheme.Paper;
        if(back != null) { var button=LinkIcon(header,ShopIcon.Back,new Vector2(8,-8),38,back);button.image.color=ShopUiTheme.Ink;button.GetComponentInChildren<ShopIconGraphic>().color=ShopUiTheme.Paper; }
    }
    private static void BuildShop(ShopWindow shop)
    {
        var root=(RectTransform)shop.transform; PrepareWindow(root,new Vector2(620,620),false);
        Header(root,"Магазин","Store",shop.Close,shop.ShowMainTabs);
        var navigation=shop.GetComponent<ShopWindowNavigation>()??shop.gameObject.AddComponent<ShopWindowNavigation>(); navigation.shop=shop;
        navigation.tabs=new[] {
            Link(root,"Оборудование","Equipment",new Vector2(18,-76),new Vector2(190,40),shop.OpenBuildCategories),
            Link(root,"Товары","Products",new Vector2(218,-76),new Vector2(170,40),shop.OpenProducts),
            Link(root,"Персонал","Staff",new Vector2(398,-76),new Vector2(204,40),shop.OpenStaff) };
        var host=Area(root,"Store content",126);
        var home=Area(host,"Store departments",0,0); home.offsetMin=Vector2.zero;home.offsetMax=Vector2.zero;
        string[] titles={"Оборудование","Закупка товаров","Персонал"};string[] english={"Equipment","Product orders","Staff"};
        string[] descriptions={"Стеллажи, витрины, кассы и строительство","Пополнение склада для выкладки на полки","Найм кассиров, охраны, складовщиков и уборщиков"};
        string[] englishDescriptions={"Shelves, displays, registers and construction","Order stock for your shelves","Hire cashiers, guards, stockers and cleaners"};
        UnityAction[] actions={shop.OpenBuildCategories,shop.OpenProducts,shop.OpenStaff};
        ShopIcon[] icons={ShopIcon.Equipment,ShopIcon.Products,ShopIcon.Staff};
        _ui.Heading(home,"Развивай свой магазин","Grow your store",new Vector2(8,-12),new Vector2(560,36),24);
        for(int i=0;i<3;i++)
        {
            var button=Link(home,"","",new Vector2(0,-66-i*118),new Vector2(580,104),actions[i]);button.image.color=new Color32(232,235,216,255);
            _ui.Icon(button.transform,icons[i],new Vector2(-242,0),new Vector2(48,48));
            _ui.Heading(button.transform,titles[i],english[i],new Vector2(86,-19),new Vector2(450,30),23);
            _ui.Copy(button.transform,descriptions[i],englishDescriptions[i],new Vector2(86,-52),new Vector2(450,38),16).color=ShopUiTheme.Muted;
        }
        var categories=_ui.Rect("Equipment categories",root,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-125),new Vector2(580,36));
        navigation.categories=new[] {Link(categories,"Стеллажи","Shelves",Vector2.zero,new Vector2(158,34),shop.OpenCategory_Shelves),Link(categories,"Структуры","Structures",new Vector2(168,0),new Vector2(158,34),shop.OpenCategory_Structures)};
        var catalog=Area(root,"Store catalog",172);
        var content=Scroll(catalog,"Shop items",out var grid);
        root.GetComponent<ShopPanelFit>().SetGrid(grid);
        Set(shop,"mainCategoriesPanel",host.gameObject);Set(shop,"mainTabsPanel",home.gameObject);Set(shop,"tabsCategoriesPanel",categories.gameObject);
        Set(shop,"categoryViewPanel",catalog.gameObject);Set(shop,"listRoot",content);Set(shop,"backButton",root.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<ShopIconGraphic>()?.Icon==ShopIcon.Back));
        var empty=_ui.Copy(catalog,"В этом разделе пока нет предметов","No items in this category",new Vector2(24,-24),new Vector2(520,60),20);
        Set(shop,"emptyLabel",empty.gameObject);shop.ShowMainTabs();
    }
    private static void BuildInventory(BuildInventoryWindow inventory)
    {
        var root=(RectTransform)inventory.transform;PrepareWindow(root,new Vector2(620,620),false);Header(root,"Инвентарь","Inventory",inventory.Close);
        var nav=inventory.GetComponent<ShopWindowNavigation>()??inventory.gameObject.AddComponent<ShopWindowNavigation>();nav.inventory=inventory;nav.categories=Array.Empty<Button>();
        nav.tabs=new[] {Link(root,"Оборудование","Equipment",new Vector2(18,-76),new Vector2(280,40),inventory.ShowFurniture),Link(root,"Товары","Products",new Vector2(310,-76),new Vector2(292,40),inventory.ShowProducts)};
        var host=Area(root,"Inventory content",126);var view=Area(host,"Inventory catalog",0,0);view.offsetMin=view.offsetMax=Vector2.zero;
        var content=Scroll(view,"Owned items",out var grid);root.GetComponent<ShopPanelFit>().SetGrid(grid);
        Set(inventory,"mainCategoriesPanel",host.gameObject);Set(inventory,"categoryViewPanel",view.gameObject);Set(inventory,"listRoot",content);
        var empty=_ui.Copy(view,"Пока пусто. Купи предметы в магазине.","Nothing here yet. Buy items in the store.",new Vector2(24,-24),new Vector2(520,80),20);
        Set(inventory,"emptyLabel",empty.gameObject);
    }
    private static RectTransform Scroll(RectTransform parent,string name,out GridLayoutGroup grid)
    {
        var scrollArea=Area(parent,name,0,0);scrollArea.offsetMin=scrollArea.offsetMax=Vector2.zero;
        var scroll=scrollArea.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
        var viewport=Area(scrollArea,"Viewport",0,0);viewport.offsetMin=Vector2.zero;viewport.offsetMax=new Vector2(-12,0);viewport.gameObject.AddComponent<RectMask2D>();
        var content=_ui.Rect("Content",viewport,new Vector2(0,1),new Vector2(0,1),Vector2.zero,Vector2.zero);content.anchorMax=new Vector2(1,1);
        grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(278,220);grid.spacing=new Vector2(12,12);grid.padding=new RectOffset(2,2,2,2);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;
        var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.content=content;scroll.viewport=viewport;
        var rail=_ui.Rect("Scroll rail",scrollArea,new Vector2(1,0),new Vector2(1,0),Vector2.zero,new Vector2(6,0));rail.anchorMax=Vector2.one;_ui.Panel(rail,ShopUiTheme.Line);
        var handle=Area(rail,"Scroll thumb",0,0);handle.offsetMin=handle.offsetMax=Vector2.zero;var image=_ui.Panel(handle,ShopUiTheme.Green);
        var bar=rail.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=image;bar.direction=Scrollbar.Direction.BottomToTop;bar.navigation=new Navigation {mode=Navigation.Mode.None};
        scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }
    private static void CreateCards(ShopWindow shop,BuildInventoryWindow inventory)
    {
        var root=Card("Equipment card",out var icon,out var title,out var action);
        var build=root.gameObject.AddComponent<ShopItemCard>();build.icon=icon;build.nameText=title;build.buyButton=action;
        build.sizeText=_ui.Label(root,"",new Vector2(162,-60),new Vector2(98,54),15);
        build.priceText=_ui.Label(root,"",new Vector2(162,-123),new Vector2(100,32),23);build.priceText.color=ShopUiTheme.Gold;build.priceText.fontStyle=FontStyles.Bold;
        Set(shop,"buildCardPrefab",SaveCard(root,"EquipmentCard").GetComponent<ShopItemCard>());

        root=Card("Product card",out icon,out title,out action);var product=root.gameObject.AddComponent<ProductShopItemCard>();
        Set(product,"icon",icon);Set(product,"nameText",title);Set(product,"buyButton",action);
        title.rectTransform.sizeDelta = new Vector2(254, 28);
        var shelfHint = _ui.Label(root,"",new Vector2(12,-41),new Vector2(254,20),12); shelfHint.color = ShopUiTheme.Muted;
        Set(product,"shelfHintText",shelfHint);
        Set(product,"boxAmountText",_ui.Label(root,"",new Vector2(150,-65),new Vector2(116,44),14));
        Set(product,"ownedAmountText",_ui.Label(root,"",new Vector2(150,-105),new Vector2(116,36),14));
        var price=_ui.Label(root,"",new Vector2(150,-138),new Vector2(116,28),22);price.color=ShopUiTheme.Gold;price.fontStyle=FontStyles.Bold;Set(product,"priceText",price);
        Set(shop,"productCardPrefab",SaveCard(root,"ProductCard").GetComponent<ProductShopItemCard>());

        root=Card("Owned equipment",out icon,out title,out action,"Поставить","Place");var owned=root.gameObject.AddComponent<BuildInventoryItemRow>();owned.icon=icon;owned.nameText=title;owned.placeButton=action;
        owned.countText=_ui.Label(root,"",new Vector2(156,-80),new Vector2(106,66),20);
        Set(inventory,"rowPrefab",SaveCard(root,"OwnedEquipmentCard").GetComponent<BuildInventoryItemRow>());

        root=Card("Owned product",out icon,out title,out action,"Выложить","Restock");var stock=root.gameObject.AddComponent<ProductInventoryItemRow>();
        Set(stock,"icon",icon);Set(stock,"nameText",title);Set(stock,"stockButton",action);Set(stock,"buttonText",action.GetComponentInChildren<TMP_Text>());
        var type=_ui.Label(root,"",new Vector2(150,-62),new Vector2(116,46),14);type.color=ShopUiTheme.Muted;Set(stock,"productTypeText",type);
        Set(stock,"countText",_ui.Label(root,"",new Vector2(150,-111),new Vector2(116,50),20));
        Set(inventory,"productRowPrefab",SaveCard(root,"OwnedProductCard").GetComponent<ProductInventoryItemRow>());
    }
    private static RectTransform Card(string name,out Image icon,out TMP_Text title,out Button button,string ru="Купить",string en="Buy")
    {
        var root=_ui.Rect(name,null,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(278,220));
        _ui.Panel(root,new Color32(250,247,236,255));_ui.Border(root);
        title=_ui.Label(root,"",new Vector2(12,-10),new Vector2(254,46),18);title.fontStyle=FontStyles.Bold;
        var picture=_ui.Rect("Item preview",root,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-61),new Vector2(132,102));
        icon=_ui.Panel(picture,Color.white);icon.type=Image.Type.Simple;icon.preserveAspect=true;icon.raycastTarget=false;
        button=_ui.Button(root,ru,en,new Vector2(12,-172),new Vector2(254,36),null);
        return root;
    }
    private static GameObject SaveCard(RectTransform root,string name)
    {
        var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,PrefabFolder+"/"+name+".prefab");Object.DestroyImmediate(root.gameObject);return prefab;
    }
    private static void BuildSettings(ControlPanel control)
    {
        var root=(RectTransform)control.settingPanel.transform;PrepareWindow(root,new Vector2(530,610),false);Header(root,"Настройки","Settings",control.ExitSetting);
        var settings=root.GetComponent<ShopSettingsPanel>()??root.gameObject.AddComponent<ShopSettingsPanel>();
        settings.controlPanel=control;settings.operationsHud=Object.FindObjectOfType<ShopOperationsHud>(true);
        settings.language=Object.FindObjectOfType<LanguageSelector>(true);settings.sound=Object.FindObjectOfType<PlaySound>(true);
        _ui.Heading(root,"Звук","Audio",new Vector2(24,-84),new Vector2(450,28),21);
        settings.musicVolume=Slider(root,"Музыка","Music",125);settings.effectsVolume=Slider(root,"Эффекты","Effects",175);
        _ui.Copy(root,"Язык","Language",new Vector2(24,-242),new Vector2(185,34),18);
        settings.languageButton=_ui.Button(root,"Русский","English",new Vector2(220,-236),new Vector2(286,42),null);settings.languageButton.image.color=ShopUiTheme.Line;
        settings.hintsButton=_ui.Button(root,"Подсказки: вкл.","Hints: on",new Vector2(24,-292),new Vector2(482,40),null);
        settings.controlsButton=_ui.Button(root,"Управление","Controls",new Vector2(24,-348),new Vector2(482,46),null);
        _ui.Icon(settings.controlsButton.transform,ShopIcon.Settings,new Vector2(-206,0),new Vector2(26,26));
        settings.saveButton=_ui.Button(root,"Сохранить игру","Save game",new Vector2(24,-410),new Vector2(482,46),null);
        _ui.Icon(settings.saveButton.transform,ShopIcon.Save,new Vector2(-206,0),new Vector2(26,26));
        var save=Object.FindObjectOfType<SaveManager>(true);if(save!=null)save.saveButton=settings.saveButton;
        settings.menuButton=_ui.Button(root,"В главное меню","Main menu",new Vector2(24,-490),new Vector2(232,42),null);settings.menuButton.image.color=ShopUiTheme.Line;
        settings.exitButton=_ui.Button(root,"Выйти","Exit game",new Vector2(274,-490),new Vector2(232,42),null);settings.exitButton.image.color=ShopUiTheme.Line;
        settings.closeButton=_ui.Button(root,"Готово","Done",new Vector2(24,-550),new Vector2(482,36),null);
    }
    private static Slider Slider(RectTransform parent,string ru,string en,float y)
    {
        _ui.Copy(parent,ru,en,new Vector2(24,-y),new Vector2(180,30),18);
        var rail=_ui.Rect(en+" volume",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(220,-y),new Vector2(286,30));
        // The transparent hit area is larger than the vector rail, making dragging easy without stretching a bitmap.
        _ui.Panel(rail,Color.clear);
        var track=_ui.Rect("Track",rail,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(262,6));
        track.gameObject.AddComponent<ShopCapsuleGraphic>().color=ShopUiTheme.Line;
        var fill=Area(track,"Fill",0,0);fill.offsetMin=fill.offsetMax=Vector2.zero;
        fill.gameObject.AddComponent<ShopCapsuleGraphic>().color=ShopUiTheme.Green;
        var handleArea=Area(rail,"Handle travel",0,0);
        handleArea.anchorMin=new Vector2(0,.5f);handleArea.anchorMax=new Vector2(1,.5f);
        handleArea.pivot=new Vector2(.5f,.5f);
        handleArea.sizeDelta=new Vector2(-24,26);handleArea.anchoredPosition=Vector2.zero;
        // Slider drives the cross-axis anchors to stretch; the travel area's height defines the handle height.
        var handle=_ui.Rect("Handle",handleArea,new Vector2(0,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(20,0));
        var graphic=handle.gameObject.AddComponent<ShopCapsuleGraphic>();graphic.color=ShopUiTheme.Ink;
        var slider=rail.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=graphic;slider.minValue=0;slider.maxValue=1;slider.navigation=new Navigation {mode=Navigation.Mode.None};
        return slider;
    }
    private static void UpdateSupportingPanels()
    {
        foreach(var shelf in Object.FindObjectsOfType<RetailEmpireTycoon.Shelves.ShelfInfoWindow>(true))
        {
            UnpackUi(shelf.gameObject);var root=(RectTransform)shelf.transform;ClearChildren(root);root.localScale=Vector3.one;root.sizeDelta=new Vector2(280,118);
            var old=root.GetComponent<Image>();if(old!=null)Object.DestroyImmediate(old);_ui.Panel(root,ShopUiTheme.Paper);_ui.Border(root);
            var picture=_ui.Rect("Product",root,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-16),new Vector2(64,72));var icon=_ui.Panel(picture,Color.white);icon.type=Image.Type.Simple;icon.preserveAspect=true;
            Set(shelf,"productIcon",icon);Set(shelf,"productText",_ui.Label(root,"",new Vector2(88,-20),new Vector2(150,40),18));Set(shelf,"amountText",_ui.Label(root,"",new Vector2(88,-67),new Vector2(156,32),20));
            Set(shelf,"closeButton",LinkIcon(root,ShopIcon.Close,new Vector2(242,-8),28,shelf.Hide));Set(shelf,"panel",root);
        }
        foreach(var confirm in Object.FindObjectsOfType<ConfirmPurchaseUI>(true))
        {
            // Keep the controller and its callbacks; replace only its visual children.
            UnpackUi(confirm.gameObject);
            var root=(RectTransform)confirm.transform;
            PrepareWindow(root,new Vector2(450,240),false);
            _ui.Heading(root,"Покупка участка","Buy territory",new Vector2(24,-20),new Vector2(402,36),26);
            Set(confirm,"_title",_ui.Label(root,"",new Vector2(24,-74),new Vector2(402,84),19));
            Set(confirm,"_no",_ui.Button(root,"Отмена","Cancel",new Vector2(24,-180),new Vector2(190,40),null));
            Set(confirm,"_yes",_ui.Button(root,"Купить","Buy",new Vector2(236,-180),new Vector2(190,40),null));
            confirm.HideInstant();
        }
    }
    internal static Object Read(Object target,string name) => new SerializedObject(target).FindProperty(name).objectReferenceValue;
    internal static void Set(Object target,string name,Object value)
    {
        var data=new SerializedObject(target);var property=data.FindProperty(name);
        if(property==null)throw new InvalidOperationException(target.name+" is missing "+name);
        property.objectReferenceValue=value;data.ApplyModifiedPropertiesWithoutUndo();
    }
}
