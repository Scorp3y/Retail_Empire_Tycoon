using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>A new-game snapshot; only an exactly matching, untouched starter layout can be migrated.</summary>
    [CreateAssetMenu(menuName = "Retail Empire Tycoon/Starter Store")]
    public sealed class StarterStoreBlueprint : ScriptableObject
    {
        public GameData initialState = new GameData();
        public Rect serviceYard;
        public Vector3 pickupPosition;
        public Vector3 pickupEuler;
        public Vector3 cameraFocus;
        public PlacedBuildSaveData legacyPlayerParking;
        public int proportionsVersion;
        public List<PlacedBuildSaveData> previousStarterStructure = new List<PlacedBuildSaveData>();

        public bool TryUpgradeUnmodifiedStructure(GameData data,GridSystem grid,BuildItemCatalog catalog)
        {
            if(data==null||!data.usesModularStore||previousStarterStructure.Count==0||grid==null||catalog==null)return false;
            bool IsStructure(PlacedBuildSaveData p)=>p.itemId=="wall_01"||p.itemId=="wallcorner_01"||p.itemId=="door_01"||p.itemId=="unloading_gate_01";
            string Key(PlacedBuildSaveData p)=>$"{p.itemId}:{p.x}:{p.z}:{p.facing}:{p.rotated}:{p.wallModuleVersion}";
            var existing=data.placedObjects.Where(IsStructure).ToArray();
            if(existing.Any(p=>p.wallModuleVersion>=3)||!existing.Select(Key).OrderBy(k=>k).SequenceEqual(previousStarterStructure.Select(Key).OrderBy(k=>k)))return false;
            var replacements=initialState.placedObjects.Where(IsStructure).ToArray();
            var occupied=new HashSet<Vector3Int>();
            foreach(var placed in replacements)
            {
                var item=catalog.GetById(placed.itemId);if(item==null)return false;
                foreach(var cell in grid.GetFootprintCells(new Vector3Int(placed.x,0,placed.z),item.footprint,placed.rotated,item.pivotOffset))
                    if(!occupied.Add(cell))return false;
            }
            foreach(var placed in data.placedObjects.Where(p=>!IsStructure(p)))
            {
                var item=catalog.GetById(placed.itemId);if(item==null)return false;
                if(grid.GetFootprintCells(new Vector3Int(placed.x,0,placed.z),item.footprint,placed.rotated,item.pivotOffset).Any(occupied.Contains))return false;
            }
            data.placedObjects.RemoveAll(p=>IsStructure(p));
            foreach(var placed in replacements)data.placedObjects.Add(JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(placed)));
            return true;
        }

        /// <summary>Upgrades only the untouched legacy exterior; a customized driveway is never overwritten.</summary>
        public bool TryUpgradeUnmodifiedFrontage(GameData data,GridSystem grid,BuildItemCatalog catalog)
        {
            if(data==null||!data.usesModularStore||legacyPlayerParking==null||legacyPlayerParking.itemId!="parking_01"||grid==null||catalog==null)return false;
            var parking=data.placedObjects.Where(p=>p.itemId=="parking_01").ToArray();
            var legacy=legacyPlayerParking;
            if(parking.Length!=1||!parking[0].playerParking||parking[0].x!=legacy.x||parking[0].z!=legacy.z
                ||parking[0].facing!=legacy.facing||parking[0].rotated!=legacy.rotated)return false;
            var newParking=initialState.placedObjects.Where(p=>p.itemId=="parking_01").ToArray();
            if(newParking.Length!=3)return false;
            var additions=initialState.floorTiles.Where(f=>f.itemId=="asphalt_01").ToArray();
            var cells=new HashSet<Vector3Int>(additions.Select(f=>new Vector3Int(f.x,0,f.z)));
            if(data.floorTiles.Any(f=>f.itemId!="asphalt_01"&&cells.Contains(new Vector3Int(f.x,0,f.z))))return false;
            foreach(var placed in data.placedObjects.Where(p=>p!=parking[0]))
            {
                var item=catalog.Items.FirstOrDefault(i=>i!=null&&i.id==placed.itemId);
                // Unknown items must not be silently moved or covered by the upgraded surface.
                if(item==null)return false;
                if(grid.GetFootprintCells(new Vector3Int(placed.x,0,placed.z),item.footprint,placed.rotated,item.pivotOffset).Any(cells.Contains))return false;
            }
            var existing=new HashSet<Vector3Int>(data.floorTiles.Select(f=>new Vector3Int(f.x,0,f.z)));
            foreach(var floor in additions)
                if(existing.Add(new Vector3Int(floor.x,0,floor.z)))data.floorTiles.Add(new FloorTileSaveData{itemId=floor.itemId,x=floor.x,z=floor.z});
            data.placedObjects.Remove(parking[0]);
            foreach(var bay in newParking)
                data.placedObjects.Add(JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(bay)));
            return true;
        }
        public GameData CreateNewGame(int startingMoney)
        {
            var result = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(initialState));
            result.usesModularStore = true;
            result.playerMoney = startingMoney;
            return result;
        }
    }
}
