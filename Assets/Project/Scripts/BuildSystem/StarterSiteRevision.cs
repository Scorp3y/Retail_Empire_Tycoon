using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>A one-time relocation of the exact default ground-floor building. Custom shops opt out.</summary>
    [Serializable]
    public sealed class StarterSiteRevision
    {
        public const int Version=1;
        [SerializeField] private bool configured;
        [SerializeField] private Vector2Int shift;
        [SerializeField] private List<PlacedBuildSaveData> previousObjects=new();
        [SerializeField] private List<FloorTileSaveData> previousFloors=new();
        [SerializeField] private List<FloorTileSaveData> accessAsphalt=new();
        [SerializeField] private RectInt oldBuilding;
        public Vector2Int Shift=>shift;

        public void Configure(GameData previous,Vector2Int offset,RectInt footprint,IEnumerable<FloorTileSaveData> asphalt)
        {
            if(previous==null || offset==Vector2Int.zero || footprint.width<=0 || footprint.height<=0)
                throw new ArgumentException("A previous starter layout, nonzero offset and building envelope are required.");
            var snapshot=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(previous));
            previousObjects=snapshot.placedObjects.Where(placed=>placed.itemId!="parking_01" && placed.floor==0).ToList();
            previousFloors=snapshot.floorTiles.Where(tile=>tile.itemId!="asphalt_01" && tile.floor==0).ToList();
            accessAsphalt=asphalt.Select(tile=>new FloorTileSaveData{itemId=tile.itemId,x=tile.x,z=tile.z}).ToList();
            if(previousObjects.Count==0 || previousFloors.Count==0 || accessAsphalt.Count==0)
                throw new ArgumentException("Starter relocation requires construction, indoor flooring and an asphalt connection.");
            shift=offset;oldBuilding=footprint;configured=true;
        }
        public bool TryApply(GameData data,GridSystem grid,BuildItemCatalog catalog)
        {
            if(!configured || data==null || !data.usesModularStore || data.starterSiteVersion>=Version || grid==null || catalog==null)return false;
            string ObjectKey(PlacedBuildSaveData item)=>$"{item.itemId}:{item.x}:{item.z}:{item.facing}:{item.rotated}:{item.wallModuleVersion}";
            string FloorKey(FloorTileSaveData item)=>$"{item.itemId}:{item.x}:{item.z}";
            var objects=data.placedObjects.Where(item=>item.itemId!="parking_01" && item.floor==0);
            var floors=data.floorTiles.Where(item=>item.itemId!="asphalt_01" && item.floor==0);
            if(!objects.Select(ObjectKey).OrderBy(key=>key).SequenceEqual(previousObjects.Select(ObjectKey).OrderBy(key=>key))
                || !floors.Select(FloorKey).OrderBy(key=>key).SequenceEqual(previousFloors.Select(FloorKey).OrderBy(key=>key)))return false;
            // Upper additions move with the building only when they stay within its old envelope.
            foreach(var placed in data.placedObjects.Where(item=>item.floor>0))
            {
                var item=catalog.GetById(placed.itemId);if(item==null)return false;
                if(grid.GetFootprintCells(new Vector3Int(placed.x,placed.floor,placed.z),item.footprint,placed.rotated,item.pivotOffset)
                    .Any(cell=>!oldBuilding.Contains(new Vector2Int(cell.x,cell.z))))return false;
            }
            if(data.floorTiles.Any(tile=>tile.floor>0 && !oldBuilding.Contains(new Vector2Int(tile.x,tile.z))))return false;
            var converted=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data));
            foreach(var placed in converted.placedObjects.Where(item=>item.itemId!="parking_01")){placed.x+=shift.x;placed.z+=shift.y;}
            foreach(var floor in converted.floorTiles.Where(item=>item.floor>0 || item.itemId!="asphalt_01")){floor.x+=shift.x;floor.z+=shift.y;}
            foreach(var stock in converted.shelfStocks){stock.anchorX+=shift.x;stock.anchorZ+=shift.y;}
            var inside=converted.floorTiles.Where(tile=>tile.floor==0 && tile.itemId!="asphalt_01").Select(tile=>new Vector2Int(tile.x,tile.z)).ToHashSet();
            var construction=new HashSet<Vector3Int>();
            foreach(var placed in converted.placedObjects.Where(item=>item.floor==0 && item.itemId!="parking_01"))
            {
                var item=catalog.GetById(placed.itemId);if(item==null)return false;
                construction.UnionWith(grid.GetFootprintCells(new Vector3Int(placed.x,0,placed.z),item.footprint,placed.rotated,item.pivotOffset));
            }
            foreach(var parking in converted.placedObjects.Where(item=>item.itemId=="parking_01"))
            {
                var item=catalog.GetById(parking.itemId);if(item==null)return false;
                if(grid.GetFootprintCells(new Vector3Int(parking.x,0,parking.z),item.footprint,parking.rotated,item.pivotOffset)
                    .Any(cell=>construction.Contains(cell)||inside.Contains(new Vector2Int(cell.x,cell.z))))return false;
            }
            converted.floorTiles.RemoveAll(tile=>tile.floor==0 && tile.itemId=="asphalt_01"
                && (inside.Contains(new Vector2Int(tile.x,tile.z)) || construction.Contains(new Vector3Int(tile.x,0,tile.z))));
            var existing=converted.floorTiles.Where(tile=>tile.floor==0).Select(tile=>new Vector2Int(tile.x,tile.z)).ToHashSet();
            foreach(var asphalt in accessAsphalt)
                if(!construction.Contains(new Vector3Int(asphalt.x,0,asphalt.z)) && existing.Add(new Vector2Int(asphalt.x,asphalt.z)))
                    converted.floorTiles.Add(new FloorTileSaveData{itemId=asphalt.itemId,x=asphalt.x,z=asphalt.z});
            data.placedObjects=converted.placedObjects;data.floorTiles=converted.floorTiles;data.shelfStocks=converted.shelfStocks;
            data.starterSiteVersion=Version;return true;
        }
    }
}
