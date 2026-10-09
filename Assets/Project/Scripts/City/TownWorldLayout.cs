using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    [Serializable]
    public sealed class TownRoadTile
    {
        public Vector2Int cell;
        // North, east, south, west. Each connection must also exist on the neighbouring tile.
        public int connections;
        public Vector3 Position(float size) => new Vector3(cell.x*size,0,cell.y*size);
    }

    [Serializable]
    public sealed class TownExpansionPlot
    {
        public TerritoryId territory;
        public Rect bounds;
    }

    [Serializable]
    public sealed class TownDistrict
    {
        public string name;
        public Rect bounds;
        public Color color;
    }

    /// <summary>One coordinate system for the authored town in shop and driving views.</summary>
    [CreateAssetMenu(menuName="Retail Empire Tycoon/Town Layout")]
    public sealed class TownWorldLayout : ScriptableObject
    {
        public float roadTileSize=12;
        public float shopToCityScale=3;
        public Vector3 sourceShopOrigin;
        public Vector3 cityShopOrigin=new Vector3(-62,0,-108);
        public Rect cityBounds=new Rect(-288,-216,576,456);
        public Rect reservedShopPlot;
        public List<TownRoadTile> roads=new List<TownRoadTile>();
        public List<TownDistrict> districts=new List<TownDistrict>();
        public List<TownExpansionPlot> expansionPlots=new List<TownExpansionPlot>();
        public Vector3 MapShopPoint(Vector3 point) => cityShopOrigin+(point-sourceShopOrigin)*shopToCityScale;
    }

}
