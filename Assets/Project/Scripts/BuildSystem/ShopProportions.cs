using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Versioned conversion of the outdoor apron when the store construction unit grows.
    /// Interior cell addresses, stock keys, inventories and money remain unchanged.</summary>
    public static class ShopProportions
    {
        public const int Version=2;
        public const float Scale=1.65f;
        public const float PreviousCellSize=.1667f;
        public const float VersionOneCellSize=PreviousCellSize*1.5f;
        public const float CellSize=PreviousCellSize*Scale;
        public static bool Upgrade(GameData data,GridSystem grid,BuildItemCatalog catalog)
        {
            if(data==null || !data.usesModularStore || data.shopProportionsVersion>=Version)return false;
            // Convert a detached snapshot so a failed fit never leaves a half-migrated save in memory.
            var converted=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data));
            UpgradeSnapshot(converted,grid,catalog);
            data.floorTiles=converted.floorTiles;data.placedObjects=converted.placedObjects;data.shelfStocks=converted.shelfStocks;data.shopProportionsVersion=Version;
            return true;
        }
        private static void UpgradeSnapshot(GameData data,GridSystem grid,BuildItemCatalog catalog)
        {
            float sourceCellSize=data.shopProportionsVersion>=1?VersionOneCellSize:PreviousCellSize;
            float ratio=CellSize/sourceCellSize;
            var oldAsphalt=data.floorTiles.Where(tile=>tile.floor==0 && catalog.GetById(tile.itemId)?.isAsphalt==true)
                .ToDictionary(tile=>new Vector3Int(tile.x,0,tile.z),tile=>tile.itemId);
            var interior=data.floorTiles.Where(tile=>tile.floor==0 && catalog.GetById(tile.itemId)?.isAsphalt!=true)
                .Select(tile=>new Vector3Int(tile.x,0,tile.z)).ToHashSet();
            foreach(var placed in data.placedObjects.Where(placed=>placed.floor==0))
            {
                var anchor=new Vector3Int(placed.x,0,placed.z);
                var item=catalog.GetById(placed.itemId);
                if(item==null || !item.isParkingSpace && !oldAsphalt.ContainsKey(anchor))continue;
                int width=placed.rotated?item.footprint.y:item.footprint.x;
                int depth=placed.rotated?item.footprint.x:item.footprint.y;
                var center=grid.origin+new Vector3((placed.x+width*.5f)*sourceCellSize,0,(placed.z+depth*.5f)*sourceCellSize);
                placed.x=Mathf.RoundToInt((center.x-grid.origin.x)/CellSize-width*.5f);
                placed.z=Mathf.RoundToInt((center.z-grid.origin.z)/CellSize-depth*.5f);
                foreach(var stock in data.shelfStocks.Where(stock=>stock.buildItemId==placed.itemId && stock.anchorX==anchor.x && stock.anchorY==0 && stock.anchorZ==anchor.z
                    && stock.rotated==placed.rotated && stock.facing==placed.facing))
                {stock.anchorX=placed.x;stock.anchorZ=placed.z;}
            }
            data.floorTiles.RemoveAll(tile=>tile.floor==0 && catalog.GetById(tile.itemId)?.isAsphalt==true);
            if(oldAsphalt.Count>0)
            {
                int minX=Mathf.FloorToInt(oldAsphalt.Keys.Min(cell=>cell.x)/ratio)-1,maxX=Mathf.CeilToInt((oldAsphalt.Keys.Max(cell=>cell.x)+1)/ratio)+1;
                int minZ=Mathf.FloorToInt(oldAsphalt.Keys.Min(cell=>cell.z)/ratio)-1,maxZ=Mathf.CeilToInt((oldAsphalt.Keys.Max(cell=>cell.z)+1)/ratio)+1;
                for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
                {
                    var cell=new Vector3Int(x,0,z);
                    var old=new Vector3Int(Mathf.FloorToInt((x+.5f)*ratio),0,Mathf.FloorToInt((z+.5f)*ratio));
                    if(!interior.Contains(cell) && oldAsphalt.TryGetValue(old,out var id))
                        data.floorTiles.Add(new FloorTileSaveData{itemId=id,x=x,z=z});
                }
            }
            ReflowParking(data,grid,catalog);
            data.shopProportionsVersion=Version;
        }
        /// <summary>Enlarged bays move to the nearest valid saved asphalt, keeping the shared approach clear.</summary>
        public static void ReflowParking(GameData data,GridSystem grid,BuildItemCatalog catalog)
        {
            var asphalt=data.floorTiles.Where(tile=>tile.floor==0 && catalog.GetById(tile.itemId)?.isAsphalt==true)
                .Select(tile=>new Vector3Int(tile.x,0,tile.z)).ToHashSet();
            var occupied=new HashSet<Vector3Int>();var approaches=new HashSet<Vector3Int>();
            foreach(var placed in data.placedObjects.Where(placed=>placed.floor==0))
            {
                var item=catalog.GetById(placed.itemId);if(item==null || item.isParkingSpace)continue;
                var size=item.isWall?item.WallFootprint(placed.wallModuleVersion):item.footprint;
                occupied.UnionWith(grid.GetFootprintCells(new Vector3Int(placed.x,0,placed.z),size,placed.rotated,item.pivotOffset));
                if(item.isDoorway)approaches.UnionWith(Parking.Rule_ParkingSurface.DoorAccessCells(grid,item,new Vector3Int(placed.x,0,placed.z),placed.rotated,placed.facing));
            }
            foreach(var placed in data.placedObjects.Where(placed=>catalog.GetById(placed.itemId)?.isParkingSpace==true).OrderByDescending(placed=>placed.playerParking))
            {
                var item=catalog.GetById(placed.itemId);var original=new Vector3Int(placed.x,0,placed.z);bool found=false;
                for(int radius=0;radius<=96 && !found;radius++)for(int dx=-radius;dx<=radius && !found;dx++)
                    foreach(int dz in new[]{radius-Mathf.Abs(dx),-(radius-Mathf.Abs(dx))}.Distinct())
                    {
                        var anchor=original+new Vector3Int(dx,0,dz);
                        var cells=grid.GetFootprintCells(anchor,item.footprint,placed.rotated,item.pivotOffset).ToArray();
                        if(cells.Any(cell=>!asphalt.Contains(cell)||occupied.Contains(cell)||approaches.Contains(cell)))continue;
                        var access=Parking.Rule_ParkingSurface.ApproachCells(grid,item,anchor,placed.rotated,placed.facing).ToArray();
                        if(access.Any(cell=>!asphalt.Contains(cell)||occupied.Contains(cell)))continue;
                        placed.x=anchor.x;placed.z=anchor.z;placed.parkingModuleVersion=item.parkingGeometryVersion;
                        occupied.UnionWith(cells);approaches.UnionWith(access);found=true;break;
                    }
                if(!found)throw new System.InvalidOperationException("There is insufficient free asphalt for the resized parking. The save has not been overwritten.");
            }
        }
    }
}
