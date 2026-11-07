using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using RetailEmpireTycoon.Core;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>A new-game snapshot; only an exactly matching, untouched starter layout can be migrated.</summary>
    [CreateAssetMenu(menuName = "Retail Empire Tycoon/Starter Store")]
    public sealed class StarterStoreBlueprint : ScriptableObject
    {
        internal const int RightSideSiteVersion = 3;
        public const int RightSideShiftCells = 12;
        internal int RightSideWestCell => initialState.placedObjects.Where(p => p.itemId == "wallcorner_01").Min(p => p.x) + 1 + RightSideShiftCells;
        public GameData initialState = new GameData();
        public Rect serviceYard;
        public Vector3 pickupPosition;
        public Vector3 pickupEuler;
        public Vector3 cameraFocus;
        public PlacedBuildSaveData legacyPlayerParking;
        public int proportionsVersion;
        public List<PlacedBuildSaveData> previousStarterStructure = new List<PlacedBuildSaveData>();
        public List<FloorTileSaveData> previousAccessAsphalt = new List<FloorTileSaveData>();
        public List<PlacedBuildSaveData> previousAccessParking = new List<PlacedBuildSaveData>();
        public bool hasStraightStreetAccess;
        public Vector3 streetEntry;
        public Vector3 drivewayEntry;
        [SerializeField] private StarterConstructionStock constructionStock = new StarterConstructionStock();
        [SerializeField] private StarterSiteRevision siteRevision = new StarterSiteRevision();
        [SerializeField] private StarterForecourtRevision forecourtRevision = new StarterForecourtRevision();
        [SerializeField] private List<PlacedBuildSaveData> frontageDecorations = new();
        public StarterForecourtRevision ForecourtRevision=>forecourtRevision;
        public StarterSiteRevision SiteRevision=>siteRevision;
        public StarterConstructionStock ConstructionStock=>constructionStock;

        // Grant once after the historical layout migrations. Custom frontages receive the
        // same pieces in storage, so no new prop can obstruct their doors or construction.
        internal void GrantFrontageDecor(GameData data, GridSystem grid, BuildItemCatalog catalog, bool newGame = false)
        {
            if (!data.usesModularStore || data.frontageDecorGranted || frontageDecorations.Count == 0) return;
            bool DefaultWall(PlacedBuildSaveData p) => p.floor == 0 &&
                IsStarterStructure(p);
            string WallKey(PlacedBuildSaveData p) => $"{p.itemId}:{p.x}:{p.z}:{p.facing}";
            bool standardLayout = newGame || data.starterSiteVersion == StarterForecourtRevision.Version &&
                data.placedObjects.Where(DefaultWall).Select(WallKey).OrderBy(key => key)
                    .SequenceEqual(initialState.placedObjects.Where(DefaultWall).Select(WallKey).OrderBy(key => key));
            var occupied = new HashSet<Vector3Int>();
            if (standardLayout && !newGame)
            {
                foreach (var placed in data.placedObjects.Where(item => item.floor == 0))
                {
                    var item = catalog.GetById(placed.itemId);
                    if (item == null) { standardLayout = false; break; }
                    occupied.UnionWith(grid.GetFootprintCells(new Vector3Int(placed.x, 0, placed.z),
                        item.footprint, placed.rotated, item.pivotOffset));
                }
            }
            foreach (var decor in frontageDecorations)
            {
                bool canPlace = standardLayout;
                if (canPlace && !newGame)
                {
                    var item = catalog.GetById(decor.itemId);
                    canPlace = item != null && !grid.GetFootprintCells(new Vector3Int(decor.x, 0, decor.z),
                        item.footprint, decor.rotated, item.pivotOffset).Any(occupied.Contains);
                }
                if (canPlace) data.placedObjects.Add(JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(decor)));
                else
                {
                    var entry = data.buildInventory.FirstOrDefault(item => item.itemId == decor.itemId);
                    if (entry == null) data.buildInventory.Add(new BuildInventorySaveEntry {itemId = decor.itemId, count = 1});
                    else entry.count++;
                }
            }
            data.frontageDecorGranted = true;
        }
        // Historical migrations compare X/Z only. A multi-storey layout is custom and must never be flattened.
        private static bool HasUpperConstruction(GameData data) => data.floorTiles.Any(f=>f.floor!=0)||data.placedObjects.Any(p=>p.floor!=0);

        private static bool IsStarterStructure(PlacedBuildSaveData p) => p.itemId == "wall_01" || p.itemId == "wallcorner_01" ||
            p.itemId == BuildItemData.StoreStripeId || p.itemId == BuildItemData.StoreStripeCornerId ||
            p.itemId == "door_01" || p.itemId == "unloading_gate_01";

        internal bool ReplaceDefaultWalls(GameData data, BuildItemCatalog catalog)
        {
            if (!data.usesModularStore || data.starterSiteVersion != initialState.starterSiteVersion) return false;
            if (!data.placedObjects.Any(p => p.floor == 0 && IsStraightStarterWall(p) && p.wallModuleVersion < BuildItemData.TwoCellWallVersion)) return false;
            string Key(PlacedBuildSaveData p)
            {
                string id = p.itemId == BuildItemData.StoreStripeId ? "wall_01" :
                    p.itemId == BuildItemData.StoreStripeCornerId ? "wallcorner_01" : p.itemId;
                return $"{id}:{p.x}:{p.z}:{p.facing}:{p.rotated}:{p.wallModuleVersion}";
            }
            var original = initialState.placedObjects.Where(p => p.floor == 0 && IsStarterStructure(p));
            var installed = data.placedObjects.Where(p => p.floor == 0 && (IsStarterStructure(p) ||
                (catalog != null && catalog.Items.Any(item => item != null && item.id == p.itemId && item.isWall)))).ToArray();
            if (!installed.Select(Key).OrderBy(key => key).SequenceEqual(original.Select(Key).OrderBy(key => key))) return false;
            var stripe = Resources.Load<BuildItemData>("WallCollection/" + BuildItemData.StoreStripeId);
            var corner = Resources.Load<BuildItemData>("WallCollection/" + BuildItemData.StoreStripeCornerId);
            if (stripe == null || corner == null || stripe.WallPlacementVersion != BuildItemData.TwoCellWallVersion) return false;

            var corners = installed.Where(p => p.itemId == "wallcorner_01" || p.itemId == BuildItemData.StoreStripeCornerId).ToArray();
            if (corners.Length != 4) return false;
            int west = corners.Min(p => p.x), south = corners.Min(p => p.z);
            var walls = installed.Where(IsStraightStarterWall).ToArray();
            var replacements = new List<PlacedBuildSaveData>();
            foreach (var row in walls.GroupBy(p => (p.facing, across: p.rotated ? p.x : p.z)))
            {
                int Along(PlacedBuildSaveData p) => p.rotated ? p.z : p.x;
                var ordered = row.OrderBy(Along).ToArray();
                if (ordered.Length % 2 != 0) return false;
                for (int i = 0; i < ordered.Length; i += 2)
                {
                    var first = ordered[i];
                    // Preserve the door and gate openings. Pair only adjacent segments on the same side of an opening.
                    if (Along(ordered[i + 1]) != Along(first) + 1) return false;
                    replacements.Add(NewWall(first, stripe,
                        first.x + (first.rotated && first.x == west ? 1 : 0),
                        first.z + (!first.rotated && first.z == south ? 1 : 0)));
                }
            }
            foreach (var oldCorner in corners)
                replacements.Add(NewWall(oldCorner, corner, oldCorner.x + (oldCorner.x == west ? 1 : 0),
                    oldCorner.z + (oldCorner.z == south ? 1 : 0)));
            data.placedObjects.RemoveAll(p => walls.Contains(p) || corners.Contains(p));
            data.placedObjects.AddRange(replacements);
            return true;

            bool IsStraightStarterWall(PlacedBuildSaveData p) => p.itemId == "wall_01" || p.itemId == BuildItemData.StoreStripeId;
            PlacedBuildSaveData NewWall(PlacedBuildSaveData source, BuildItemData item, int x, int z) => new()
            { itemId = item.id, x = x, z = z, floor = source.floor, facing = source.facing, rotated = source.rotated,
                wallModuleVersion = item.WallPlacementVersion };
        }

        internal void FitDefaultOpeningsAndFloor(GameData data, BuildItemCatalog catalog)
        {
            if (!data.usesModularStore || !constructionStock.HasSingleCellOpenings ||
                data.starterSiteVersion != initialState.starterSiteVersion) return;
            bool IsOpening(PlacedBuildSaveData p) => p.itemId == "door_01" || p.itemId == "unloading_gate_01";
            if (!data.placedObjects.Any(p => p.floor == 0 && IsOpening(p) && p.wallModuleVersion < BuildItemData.TwoCellWallVersion)) return;

            // Reuse the wall revision to recognize both a new shop and an untouched save from the previous update.
            var expected = new GameData { usesModularStore = true, starterSiteVersion = initialState.starterSiteVersion,
                placedObjects = new List<PlacedBuildSaveData>(initialState.placedObjects) };
            if (!ReplaceDefaultWalls(expected, null)) return;
            string Key(PlacedBuildSaveData p) => $"{p.itemId}:{p.x}:{p.z}:{p.facing}:{p.rotated}:{p.wallModuleVersion}";
            var installed = data.placedObjects.Where(p => p.floor == 0 && (IsStarterStructure(p) ||
                (catalog != null && catalog.Items.Any(item => item != null && item.id == p.itemId && item.isWall)))).ToArray();
            if (!installed.Select(Key).OrderBy(key => key).SequenceEqual(expected.placedObjects.Where(IsStarterStructure)
                .Select(Key).OrderBy(key => key))) return;

            var corners = installed.Where(p => p.itemId == BuildItemData.StoreStripeCornerId).ToArray();
            int west = corners.Min(p => p.x), east = corners.Max(p => p.x);
            int south = corners.Min(p => p.z), north = corners.Max(p => p.z);
            bool Outside(FloorTileSaveData tile) => tile.x < west || tile.x > east || tile.z < south || tile.z > north;
            var oldBorder = initialState.floorTiles.Where(tile => tile.floor == 0 && tile.itemId == "floor_graphite" && Outside(tile))
                .Select(tile => new Vector2Int(tile.x, tile.z)).ToHashSet();
            if (catalog != null)
            {
                // A custom fixture may already use a strip that the previous wall revision exposed.
                foreach (var placed in data.placedObjects.Where(p => p.floor == 0 && !IsStarterStructure(p)))
                {
                    var item = catalog.Items.FirstOrDefault(candidate => candidate != null && candidate.id == placed.itemId);
                    if (item == null) return;
                    var size = placed.rotated ? new Vector2Int(item.footprint.y, item.footprint.x) : item.footprint;
                    var offset = placed.rotated ? new Vector2Int(item.pivotOffset.y, item.pivotOffset.x) : item.pivotOffset;
                    var area = new RectInt(new Vector2Int(placed.x, placed.z) + offset, size);
                    oldBorder.RemoveWhere(cell => area.Contains(cell));
                }
            }
            foreach (var opening in installed.Where(IsOpening))
            {
                if (opening.rotated && opening.x == west - 1) opening.x = west;
                if (!opening.rotated && opening.z == south - 1) opening.z = south;
                opening.wallModuleVersion = BuildItemData.TwoCellWallVersion;
            }
            data.floorTiles.RemoveAll(tile => tile.floor == 0 && tile.itemId == "floor_graphite" && oldBorder.Contains(new Vector2Int(tile.x, tile.z)));
        }

        internal bool MoveDefaultStoreRight(GameData data, bool newGame = false)
        {
            if (!data.usesModularStore || data.starterSiteVersion != StarterForecourtRevision.Version || HasUpperConstruction(data)) return false;
            if (!newGame)
            {
                var expected = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(initialState));
                GrantFrontageDecor(expected, null, null, newGame: true);
                ReplaceDefaultWalls(expected, null);
                FitDefaultOpeningsAndFloor(expected, null);
                // Move only the previous default site. Its inventory and shelf contents can vary freely.
                if (!data.placedObjects.Select(p => JsonUtility.ToJson(p)).OrderBy(key => key).SequenceEqual(
                    expected.placedObjects.Select(p => JsonUtility.ToJson(p)).OrderBy(key => key)) ||
                    !data.floorTiles.Select(t => JsonUtility.ToJson(t)).OrderBy(key => key).SequenceEqual(
                    expected.floorTiles.Select(t => JsonUtility.ToJson(t)).OrderBy(key => key))) return false;
            }

            var corners = data.placedObjects.Where(p => p.itemId == BuildItemData.StoreStripeCornerId).ToArray();
            if (corners.Length != 4) return false;
            int west = corners.Min(p => p.x), south = corners.Min(p => p.z);
            var asphalt = data.floorTiles.Where(t => t.itemId == "asphalt_01").ToArray();
            // Keep the full depth of the existing warehouse apron on the left.
            int north = asphalt.Where(t => t.x == west - 2).Select(t => t.z).DefaultIfEmpty(south - 2).Max();
            if (north < south) return false;
            foreach (var placed in data.placedObjects.Where(p => p.itemId != "parking_01" && !p.itemId.StartsWith("frontage_", System.StringComparison.Ordinal)))
                placed.x += RightSideShiftCells;
            foreach (var tile in data.floorTiles.Where(t => t.itemId != "asphalt_01")) tile.x += RightSideShiftCells;
            foreach (var stock in data.shelfStocks) stock.anchorX += RightSideShiftCells;

            var occupied = data.floorTiles.Select(t => new Vector2Int(t.x, t.z)).ToHashSet();
            for (int x = west - 1; x < west + RightSideShiftCells; x++)
            for (int z = south - 1; z <= north; z++)
                if (occupied.Add(new Vector2Int(x, z))) data.floorTiles.Add(new FloorTileSaveData { itemId = "asphalt_01", x = x, z = z });
            data.starterSiteVersion = RightSideSiteVersion;
            return true;
        }

        internal void ClearStarterSidewalkOverhang(GameData data)
        {
            if (data.starterSiteVersion != RightSideSiteVersion) return;
            int west = RightSideWestCell;
            foreach (var original in frontageDecorations.Where(p => p.x < west))
            {
                // A fixture the player moved is theirs. Return only pieces still on the trimmed default stretch.
                var placed = data.placedObjects.FirstOrDefault(p => p.itemId == original.itemId && p.x == original.x &&
                    p.z == original.z && p.floor == original.floor && p.facing == original.facing && p.rotated == original.rotated);
                if (placed == null) continue;
                data.placedObjects.Remove(placed);
                var stock = data.buildInventory.FirstOrDefault(entry => entry.itemId == placed.itemId);
                if (stock == null) data.buildInventory.Add(new BuildInventorySaveEntry { itemId = placed.itemId, count = 1 });
                else stock.count++;
            }
        }

        /// <summary>Moves only the exact default forecourt. User-painted floors and moved/added bays
        /// opt out; saved buildings, inventory, money and purchased plots are never removed.</summary>
        public bool TryUpgradeUnmodifiedAccess(GameData data,GridSystem grid,BuildItemCatalog catalog)
        {
            if(data==null||HasUpperConstruction(data)||!data.usesModularStore||grid==null||catalog==null||previousAccessAsphalt.Count==0||previousAccessParking.Count!=3)return false;
            string FloorKey(FloorTileSaveData f)=>$"{f.itemId}:{f.x}:{f.z}";
            string BayKey(PlacedBuildSaveData p)=>$"{p.x}:{p.z}:{p.facing}:{p.rotated}:{p.playerParking}";
            var asphalt=data.floorTiles.Where(f=>f.itemId=="asphalt_01").ToArray();
            var bays=data.placedObjects.Where(p=>p.itemId=="parking_01").ToArray();
            if(!asphalt.Select(FloorKey).OrderBy(k=>k).SequenceEqual(previousAccessAsphalt.Select(FloorKey).OrderBy(k=>k))
                ||!bays.Select(BayKey).OrderBy(k=>k).SequenceEqual(previousAccessParking.Select(BayKey).OrderBy(k=>k)))return false;
            var oldCells=new HashSet<Vector3Int>(asphalt.Select(f=>new Vector3Int(f.x,0,f.z)));
            var newFloors=initialState.floorTiles.Where(f=>f.itemId=="asphalt_01").ToArray();
            var newCells=new HashSet<Vector3Int>(newFloors.Select(f=>new Vector3Int(f.x,0,f.z)));
            var changed=new HashSet<Vector3Int>(oldCells);changed.SymmetricExceptWith(newCells);
            // A player's prop on the retained asphalt can still collide with a shifted bay.
            foreach(var bay in initialState.placedObjects.Where(p=>p.itemId=="parking_01"))
            {
                var item=catalog.GetById(bay.itemId);if(item==null)return false;
                changed.UnionWith(grid.GetFootprintCells(new Vector3Int(bay.x,0,bay.z),item.footprint,bay.rotated,item.pivotOffset));
            }
            if(data.floorTiles.Any(f=>f.itemId!="asphalt_01"&&newCells.Contains(new Vector3Int(f.x,0,f.z))))return false;
            foreach(var placed in data.placedObjects.Where(p=>p.itemId!="parking_01"))
            {
                var item=catalog.GetById(placed.itemId);if(item==null)return false;
                if(grid.GetFootprintCells(new Vector3Int(placed.x,0,placed.z),item.footprint,placed.rotated,item.pivotOffset).Any(changed.Contains))return false;
            }
            data.floorTiles.RemoveAll(f=>f.itemId=="asphalt_01");
            data.floorTiles.AddRange(newFloors.Select(f=>new FloorTileSaveData{itemId=f.itemId,x=f.x,z=f.z}));
            data.placedObjects.RemoveAll(p=>p.itemId=="parking_01");
            data.placedObjects.AddRange(initialState.placedObjects.Where(p=>p.itemId=="parking_01").Select(p=>JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(p))));
            return true;
        }

        public bool TryUpgradeUnmodifiedStructure(GameData data,GridSystem grid,BuildItemCatalog catalog)
        {
            if(data==null||HasUpperConstruction(data)||!data.usesModularStore||previousStarterStructure.Count==0||grid==null||catalog==null)return false;
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
            if(data==null||HasUpperConstruction(data)||!data.usesModularStore||legacyPlayerParking==null||legacyPlayerParking.itemId!="parking_01"||grid==null||catalog==null)return false;
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
            GrantFrontageDecor(result, null, null, newGame: true);
            ReplaceDefaultWalls(result, null);
            FitDefaultOpeningsAndFloor(result, null);
            MoveDefaultStoreRight(result, newGame: true);
            ClearStarterSidewalkOverhang(result);
            constructionStock.AddTo(result, new System.Random());
            return result;
        }
    }
}
