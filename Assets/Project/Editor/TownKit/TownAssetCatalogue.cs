using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal sealed class TownAssetDefinition
    {
        public string Category { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public Func<TownMeshBuilder> Build { get; }
        public TownAssetDefinition(string category,string id,string displayName,Func<TownMeshBuilder> build)
        { Category=category;Id=id;DisplayName=displayName;Build=build; }
    }

    internal static class TownAssetCatalogue
    {
        public static List<TownAssetDefinition> Create()
        {
            var assets=new List<TownAssetDefinition>();
            void Add(string category,string id,string name,Func<TownMeshBuilder> build)=>assets.Add(new TownAssetDefinition(category,id,name,build));
            const string buildings="Buildings",roads="Roads",logistics="Logistics",street="Street",nature="Nature",vehicles="Vehicles",shop="PlayerShop";
            Add(buildings,"HouseCream","Частный дом",()=>TownBuildings.House(TownColor.Cream));
            Add(buildings,"HouseBrick","Кирпичный дом",()=>TownBuildings.House(TownColor.Brick));
            Add(buildings,"Townhouse","Таунхаус",()=>TownBuildings.House(TownColor.Brick,2,true));
            Add(buildings,"ApartmentSmall","Малый многоквартирный дом",TownBuildings.Apartment);
            Add(buildings,"Garage","Гараж",TownBuildings.Garage);
            Add(buildings,"WholesaleBase","Продуктовая оптовая база",TownBuildings.Wholesale);
            Add(buildings,"FarmersMarket","Фермерский рынок",TownBuildings.FarmersMarket);
            Add(buildings,"EquipmentSupplier","Магазин торгового оборудования",()=>TownBuildings.Retail("ОБОРУДОВАНИЕ",14,9,equipment:true));
            Add(buildings,"ConstructionSupplier","Строительство и декор",()=>TownBuildings.Retail("СТРОЙМАРКЕТ",15,10,construction:true));
            Add(buildings,"CarDealership","Автосалон",()=>TownBuildings.Retail("АВТО",16,10,dealership:true));
            Add(buildings,"Cafe","Кафе",()=>TownBuildings.Cafe("КАФЕ",TownColor.Brick));
            Add(buildings,"Bakery","Пекарня",()=>TownBuildings.Cafe("ПЕКАРНЯ",TownColor.Cream));
            Add(buildings,"Pharmacy","Аптека",()=>TownBuildings.Cafe("АПТЕКА",TownColor.Mint,outdoorTables:false));
            Add(buildings,"FlowerShop","Цветочный магазин",()=>TownBuildings.Cafe("ЦВЕТЫ",TownColor.Cream,true));

            Add(roads,"RoadStraight","Прямая дорога 12 м",()=>TownRoads.Road("straight"));
            Add(roads,"RoadCorner","Поворот дороги 90 градусов",()=>TownRoads.Road("corner"));
            Add(roads,"RoadTee","Т-образный перекрёсток",()=>TownRoads.Road("tee"));
            Add(roads,"RoadCross","Крестовой перекрёсток",()=>TownRoads.Road("cross"));
            Add(roads,"RoadDeadEnd","Тупик с разворотом",()=>TownRoads.Road("deadend"));
            Add(roads,"SidewalkStraight","Прямой тротуар",()=>TownRoads.Sidewalk(false));
            Add(roads,"SidewalkCorner","Угловой тротуар",()=>TownRoads.Sidewalk(true));
            Add(roads,"Curb","Бордюр",()=>TownProps.Create(m=>m.Box(new Vector3(0,.09f,0),new Vector3(.25f,.18f,12),TownColor.Pavement)));
            Add(roads,"DrivewayCurb","Пониженный въезд",()=>TownProps.Create(m=>m.ExtrudeX(Vector3.zero,4,new[]{new Vector2(0,-1),new Vector2(.14f,-1),new Vector2(.04f,1),new Vector2(0,1)},TownColor.Pavement)));
            Add(roads,"Crosswalk","Пешеходный переход",TownRoads.Crossing);
            Add(roads,"LaneMarkings","Разметка полос",()=>TownProps.Create(m=>{for(int i=0;i<8;i++)m.Ground(0,-5.2f+i*1.5f,.12f,.8f,.006f,TownColor.White);}));
            Add(roads,"AsphaltPad","Асфальтовая площадка 12 на 12 м",()=>TownProps.Create(m=>m.Box(new Vector3(0,.025f,0),new Vector3(12,.05f,12),TownColor.Asphalt)));
            Add(roads,"ParkingBay","Парковочное место",()=>TownRoads.Parking(1));
            Add(roads,"CustomerParking","Парковка на три машины",()=>TownRoads.Parking(3));
            Add(roads,"PlazaTile","Плитка площади",TownRoads.Plaza);
            Add(roads,"GravelTile","Гравийное покрытие",()=>TownProps.Create(m=>{m.Ground(0,0,4,4,.005f,TownColor.Sand);for(int i=0;i<24;i++)m.Ellipsoid(new Vector3(Mathf.Sin(i*3.1f)*1.7f,.018f,Mathf.Cos(i*2.1f)*1.7f),new Vector3(.06f,.018f,.07f),TownColor.Stone,4,2);}));
            Add(roads,"ParkPath","Парковая дорожка",()=>TownProps.Create(m=>{m.Box(new Vector3(0,.02f,0),new Vector3(2,.04f,4),TownColor.Pavement);for(int i=0;i<4;i++)m.Ground(0,-1.5f+i,2,.02f,.043f,TownColor.Stone);}));

            Add(logistics,"LoadingGate","Погрузочные ворота",()=>TownProps.Create(m=>TownArchitecturalParts.LoadingDoor(m,new Vector3(0,1.8f,0),3.5f,3.5f)));
            Add(logistics,"LoadingCanopy","Погрузочный навес",()=>TownProps.Create(TownProps.LoadingCanopy));
            Add(logistics,"PickupStopZone","Зона остановки для погрузки",TownRoads.StopZone);
            Add(logistics,"IndustrialFence","Промышленное ограждение",()=>TownProps.Create(m=>TownProps.Fence(m,4,2.2f,true)));
            Add(logistics,"WarehouseRack","Складской стеллаж с коробками",()=>TownProps.Create(m=>TownProps.WarehouseRack(m,Vector3.zero)));
            Add(logistics,"Pallet","Деревянный поддон",()=>TownProps.Create(m=>TownProps.Pallet(m,Vector3.zero)));
            Add(logistics,"BoxSmall","Малая коробка",()=>TownProps.Create(m=>TownProps.CardboardBox(m,Vector3.zero,new Vector3(.4f,.35f,.45f))));
            Add(logistics,"BoxMedium","Средняя коробка",()=>TownProps.Create(m=>TownProps.CardboardBox(m,Vector3.zero,new Vector3(.65f,.55f,.7f))));
            Add(logistics,"BoxLarge","Большая коробка",()=>TownProps.Create(m=>TownProps.CardboardBox(m,Vector3.zero,new Vector3(1,.85f,.9f))));
            Add(logistics,"FruitCrate","Ящик фруктов",()=>TownProps.Create(m=>TownProps.Crate(m,Vector3.zero,false)));
            Add(logistics,"VegetableCrate","Ящик овощей",()=>TownProps.Create(m=>TownProps.Crate(m,Vector3.zero,true)));
            Add(logistics,"CargoContainer","Грузовой контейнер",TownProps.Container);
            Add(logistics,"RetailShelf","Выставочный торговый стеллаж",()=>TownProps.Create(m=>TownProps.Shelf(m,Vector3.zero,2.4f,2.1f,.8f,true)));
            Add(logistics,"Refrigerator","Выставочный холодильник",()=>TownProps.Create(m=>TownProps.Refrigerator(m,Vector3.zero)));
            Add(logistics,"CheckoutCounter","Кассовый прилавок",()=>TownProps.Create(m=>TownProps.Checkout(m,Vector3.zero)));
            Add(logistics,"PickupSign","Знак выдачи заказов без стрелки",()=>TownProps.Create(m=>{m.Box(new Vector3(0,1,0),new Vector3(.09f,2,.09f),TownColor.Frame);TownArchitecturalParts.Sign(m,new Vector3(0,2.05f,0),1.9f,.75f,"ВЫДАЧА");}));

            Add(street,"StreetLamp","Фонарный столб",TownProps.StreetLamp);
            Add(street,"StopSign","Знак STOP",()=>TownProps.TrafficSign("stop"));
            Add(street,"YieldSign","Знак уступить дорогу",()=>TownProps.TrafficSign("yield"));
            Add(street,"ParkingSign","Знак парковки",()=>TownProps.TrafficSign("parking"));
            Add(street,"OneWaySign","Знак одностороннего движения",()=>TownProps.TrafficSign("oneway"));
            Add(street,"Bench","Скамья",()=>TownProps.Create(TownProps.Bench));
            Add(street,"LitterBin","Урна",()=>TownProps.Create(m=>{m.Cylinder(new Vector3(0,.42f,0),.27f,.27f,.84f,TownColor.Frame);m.Cylinder(new Vector3(0,.86f,0),.3f,.3f,.08f,TownColor.Metal);m.Cylinder(new Vector3(0,.905f,0),.15f,.15f,.012f,TownColor.Rubber);}));
            Add(street,"BusStop","Остановка",TownProps.BusStop);
            Add(street,"DomesticFence","Забор двора",()=>TownProps.Create(m=>TownProps.Fence(m,3,1.15f,false)));
            Add(street,"DomesticGate","Калитка",()=>TownProps.Create(m=>TownProps.Fence(m,1.2f,1.15f,false)));
            Add(street,"FlowerPlanter","Цветочная кадка",()=>TownProps.Create(m=>TownProps.Planter(m,Vector3.zero,true)));
            Add(street,"FlowerBed","Клумба",()=>TownProps.Create(m=>{m.Box(new Vector3(0,.12f,0),new Vector3(2.2f,.24f,1.4f),TownColor.Cream);m.Ground(0,0,2,1.2f,.245f,TownColor.Soil);for(int x=-1;x<=1;x++)TownNature.Grass(m,new Vector3(x*.65f,.25f,0),true,9);}));
            Add(street,"Mailbox","Почтовый ящик",()=>TownProps.Create(m=>{m.Box(new Vector3(0,.65f,0),new Vector3(.1f,1.3f,.1f),TownColor.Frame);m.Box(new Vector3(0,1.35f,0),new Vector3(.48f,.42f,.52f),TownColor.Blue);m.Box(new Vector3(0,1.4f,.27f),new Vector3(.28f,.035f,.03f),TownColor.Rubber);}));
            Add(street,"WasteContainer","Хозяйственный контейнер",TownProps.WasteContainer);
            Add(street,"Bollard","Ограничительный столбик",()=>TownProps.Create(m=>{m.Cylinder(new Vector3(0,.5f,0),.09f,.075f,1,TownColor.Frame);m.Cylinder(new Vector3(0,.77f,0),.092f,.088f,.14f,TownColor.Yellow);}));

            Add(nature,"TreeRound","Круглая крона",()=>TownProps.Create(m=>TownNature.Tree(m,Vector3.zero,0)));
            Add(nature,"TreeTall","Высокое лиственное дерево",()=>TownProps.Create(m=>TownNature.Tree(m,Vector3.zero,1)));
            Add(nature,"TreeWide","Широкая крона",()=>TownProps.Create(m=>TownNature.Tree(m,Vector3.zero,2)));
            Add(nature,"Pine","Ель",()=>TownProps.Create(m=>TownNature.Pine(m,Vector3.zero)));
            Add(nature,"BushRound","Круглый куст",()=>TownNature.Bush(false));
            Add(nature,"BushWide","Широкий куст",()=>TownNature.Bush(true));
            Add(nature,"Hedge","Живая изгородь",TownNature.Hedge);
            Add(nature,"GrassClump","Пучок травы",()=>TownProps.Create(m=>TownNature.Grass(m,Vector3.zero,false)));
            Add(nature,"FlowerPatch","Трава с цветами",()=>TownProps.Create(m=>TownNature.Grass(m,Vector3.zero,true)));
            Add(nature,"RockGroup","Группа камней",TownNature.Rocks);
            Add(nature,"GrassyHill","Невысокий холм",TownNature.Hill);
            Add(nature,"TreeBelt","Лесополоса",TownNature.TreeBelt);
            Add(nature,"MeadowPatch","Участок луга",TownNature.Meadow);
            Add(nature,"ExitBarrier","Шлагбаум выезда",TownProps.Barrier);

            Add(vehicles,"DeliveryPickup","Пикап доставки",TownVehicles.Pickup);
            Add(vehicles,"VisitorSedan","Легковой автомобиль",()=>TownVehicles.Passenger(TownColor.Orange));
            Add(vehicles,"VisitorHatchback","Хэтчбек",()=>TownVehicles.Passenger(TownColor.Leaf,true));
            Add(vehicles,"DisplayCar","Автомобиль автосалона",()=>TownVehicles.Passenger(TownColor.White));
            Add(vehicles,"BoxTruck","Грузовой фургон",TownVehicles.Truck);
            Add(vehicles,"ForkliftStatic","Декоративный погрузчик",TownVehicles.Forklift);
            Add(vehicles,"PickupCargoBoxes","Коробки для кузова",TownVehicles.Cargo);

            Add(shop,"PlayerShopShell","Открытая модель магазина и склада",TownBuildings.PlayerShop);
            Add(shop,"PersonalPickupBay","Личная парковка пикапа",()=>TownRoads.Parking(1,true));
            Add(shop,"StoreReceivingGate","Ворота приёмки магазина",()=>TownProps.Create(m=>{TownArchitecturalParts.LoadingDoor(m,new Vector3(0,1.55f,0),2.8f,3);TownArchitecturalParts.Sign(m,new Vector3(0,3.42f,.05f),3.6f,.55f,"СКЛАД");}));
            Add(shop,"StoreSign","Вывеска магазина",()=>TownProps.Create(m=>{foreach(float x in new[]{-1.3f,1.3f})m.Box(new Vector3(x,1.3f,0),new Vector3(.1f,2.6f,.1f),TownColor.Frame);TownArchitecturalParts.Sign(m,new Vector3(0,2.7f,0),3.4f,1,"МАГАЗИН");}));
            Add(shop,"ReservedPlotMarkers","Обозначение участка расширения",()=>TownProps.Create(m=>{foreach(float x in new[]{-4f,4f})foreach(float z in new[]{-4f,4f})m.Box(new Vector3(x,.22f,z),new Vector3(.12f,.44f,.12f),TownColor.Yellow);for(int i=0;i<8;i++){foreach(float x in new[]{-4f,4f})m.Ground(x,-3.5f+i,.055f,.5f,.008f,TownColor.White);foreach(float z in new[]{-4f,4f})m.Ground(-3.5f+i,z,.5f,.055f,.008f,TownColor.White);}}));
            return assets;
        }
    }
}
