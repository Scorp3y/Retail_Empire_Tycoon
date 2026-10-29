using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Updates only the exact default frontage. Player construction and customized paving opt out atomically.</summary>
    [Serializable]
    public sealed class StarterForecourtRevision
    {
        public const int Version=2;
        [SerializeField] private bool configured;
        [SerializeField] private GameData previous=new(), replacement=new();
        [SerializeField] private RectInt buildingEnvelope;
        [SerializeField] private Vector2Int buildingShift;

        public void Configure(GameData before,GameData after,RectInt envelope,Vector2Int shift)
        {
            if(before==null || after==null || before.starterSiteVersion!=1 || after.starterSiteVersion!=Version)
                throw new ArgumentException("The frontage revision requires version-one and version-two starter snapshots.");
            previous=Clone(before);replacement=Clone(after);buildingEnvelope=envelope;buildingShift=shift;configured=true;
        }
        public bool TryApply(GameData data,GridSystem grid,BuildItemCatalog catalog)
        {
            if(!configured || data==null || grid==null || catalog==null || !data.usesModularStore || data.starterSiteVersion!=1)return false;
            string Key(PlacedBuildSaveData item)=>JsonUtility.ToJson(item);
            string TileKey(FloorTileSaveData item)=>JsonUtility.ToJson(item);
            if(!data.placedObjects.Where(item=>item.floor==0).Select(Key).OrderBy(key=>key).SequenceEqual(previous.placedObjects.Select(Key).OrderBy(key=>key))
                || !data.floorTiles.Where(tile=>tile.floor==0).Select(TileKey).OrderBy(key=>key).SequenceEqual(previous.floorTiles.Select(TileKey).OrderBy(key=>key)))return false;
            foreach(var placed in data.placedObjects.Where(item=>item.floor>0))
            {
                var item=catalog.GetById(placed.itemId);if(item==null)return false;
                if(grid.GetFootprintCells(new Vector3Int(placed.x,placed.floor,placed.z),item.footprint,placed.rotated,item.pivotOffset)
                    .Any(cell=>!buildingEnvelope.Contains(new Vector2Int(cell.x,cell.z))))return false;
            }
            if(data.floorTiles.Any(tile=>tile.floor>0 && !buildingEnvelope.Contains(new Vector2Int(tile.x,tile.z))))return false;
            var converted=Clone(data);var target=Clone(replacement);
            foreach(var placed in converted.placedObjects.Where(item=>item.floor>0)){placed.x+=buildingShift.x;placed.z+=buildingShift.y;}
            foreach(var tile in converted.floorTiles.Where(tile=>tile.floor>0)){tile.x+=buildingShift.x;tile.z+=buildingShift.y;}
            foreach(var stock in converted.shelfStocks){stock.anchorX+=buildingShift.x;stock.anchorZ+=buildingShift.y;}
            converted.placedObjects.RemoveAll(item=>item.floor==0);converted.placedObjects.AddRange(target.placedObjects);
            converted.floorTiles.RemoveAll(tile=>tile.floor==0);converted.floorTiles.AddRange(target.floorTiles);
            data.placedObjects=converted.placedObjects;data.floorTiles=converted.floorTiles;data.shelfStocks=converted.shelfStocks;
            data.starterSiteVersion=Version;return true;
        }
        private static GameData Clone(GameData data)=>JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data));
    }
}
