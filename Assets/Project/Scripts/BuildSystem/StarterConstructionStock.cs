using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>New-game construction stock only. Existing saves never pass through this grant.</summary>
    [Serializable]
    public sealed class StarterConstructionStock
    {
        private const string BrickPaversId = "Floor_26_BrickPavers";
        private const string CeramicFloorId = "Floor_01_CreamCeramic";
        private const int BulkFloorCount = 50000;
        [SerializeField] private BuildItemData[] shelves = Array.Empty<BuildItemData>();
        [SerializeField] private BuildItemData floor, wall, door, gate, parking;
        [SerializeField] private BuildItemData personalParking;
        [SerializeField] private BuildItemData[] transports=Array.Empty<BuildItemData>();

        internal bool HasSingleCellOpenings => door != null && gate != null && door.HasVersionedOpening && gate.HasVersionedOpening;

        public void ConfigureTransports(IEnumerable<BuildItemData> items)
        {
            var choices=items.Where(item=>item!=null).Distinct().ToArray();
            if(choices.Length!=4 || choices.Any(item=>item.storeyConnection==StoreyConnectionKind.None)
                || choices.Select(item=>item.storeyConnection).Distinct().Count()!=3)
                throw new ArgumentException("Starter transport stock needs stairs, an elevator and both escalator definitions.");
            transports=choices;
        }

        public void Configure(IEnumerable<BuildItemData> shelfChoices, BuildItemData floorItem,
            BuildItemData wallItem, BuildItemData doorItem, BuildItemData gateItem, BuildItemData parkingItem)
        {
            shelves=shelfChoices.Where(item=>item!=null && item.category==BuildCategory.Shelf).Distinct().ToArray();
            floor=floorItem;wall=wallItem;door=doorItem;gate=gateItem;parking=parkingItem;
            Validate();
        }

        public void AddTo(GameData data, System.Random random, IEnumerable<BuildItemData> catalog = null)
        {
            if(data==null)throw new ArgumentNullException(nameof(data));
            if(random==null)throw new ArgumentNullException(nameof(random));
            Validate();
            var choices=(BuildItemData[])shelves.Clone();
            for(int i=choices.Length-1;i>0;i--)
            {
                int j=random.Next(i+1);var item=choices[i];choices[i]=choices[j];choices[j]=item;
            }
            void Add(BuildItemData item,int count)
            {
                var entry=data.buildInventory.FirstOrDefault(stock=>stock.itemId==item.id);
                if(entry==null)data.buildInventory.Add(new BuildInventorySaveEntry{itemId=item.id,count=count});
                else entry.count+=count;
            }
            for(int i=0;i<5;i++)Add(choices[i],1);
            var wallStyles = BuildItemCatalog.LoadWallCollection();
            var stripe = wallStyles.FirstOrDefault(item => item.id == BuildItemData.StoreStripeId);
            Add(floor,20);Add(stripe ?? wall,20);Add(door,1);Add(gate,1);Add(parking,2);
            foreach(var transport in transports)Add(transport,1);
            foreach (var wallStyle in wallStyles)
                if (!wallStyle.IsWallCorner) Add(wallStyle, 10);
            foreach (var floorStyle in BuildItemCatalog.LoadFloorCollection())
                Add(floorStyle, floorStyle.id is BrickPaversId or CeramicFloorId ? BulkFloorCount : 50);
            foreach (var doorStyle in BuildItemCatalog.LoadDoorCollection())
                Add(doorStyle, 3);
            var decorations = BuildItemCatalog.LoadDecorCollection()
                .Concat(catalog ?? Array.Empty<BuildItemData>())
                .Where(item => item != null && item.category == BuildCategory.Decoration && !item.hiddenFromShop && item.prefab != null)
                .GroupBy(item => item.id);
            foreach (var decoration in decorations)
            {
                int current = data.buildInventory.FirstOrDefault(entry => entry.itemId == decoration.Key)?.count ?? 0;
                if (current < 3) Add(decoration.First(), 3 - current);
            }
            if (personalParking != null) Add(personalParking, 2);
        }

        private void Validate()
        {
            if(shelves==null || shelves.Length<5 || shelves.Any(item=>item==null || item.category!=BuildCategory.Shelf)
                || floor==null || floor.isAsphalt || wall==null || door==null || gate==null || parking==null
                || transports==null || transports.Any(item=>item==null || item.storeyConnection==StoreyConnectionKind.None))
                throw new InvalidOperationException("Starter construction stock requires five shelf choices, an indoor floor, wall, door, gate and parking.");
        }
    }
}
