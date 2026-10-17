using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

/// <summary>Stage-three residential parcels. Does not describe suppliers, editable shop land or nature.</summary>
internal sealed class TownResidentialPlan
{
    public const string AssetRoot = "Assets/Art/TownResidentialLayout";
    public const string HierarchyRoot = "Residential stage 3";
    public IReadOnlyList<TownResidentialBlock> Blocks { get; }
    public IReadOnlyList<Rect> AllowedAreas { get; }

    private TownResidentialPlan(TownResidentialBlock[] blocks)
    {
        Blocks = Array.AsReadOnly(blocks);
        AllowedAreas = Array.AsReadOnly(blocks.Select(b => new Rect(b.Bounds.xMin - 2, b.Bounds.yMin - 2,
            b.Bounds.width + 4, b.Bounds.height + 4)).ToArray());
    }

    public static TownResidentialPlan Create()
    {
        var definitions = TownArchitectureModels.Create().ToDictionary(d => d.Id);
        var blocks = new[] {
            Block("WestCorner", "Западный жилой район", "Два фасадных ряда", -210,-66,84,36),
            Block("WestCourtyard", "Западный жилой район", "Г- и П-образный двор", -210,-18,84,60),
            Block("WestNorth", "Западный жилой район", "Короткие секции вокруг зелени", -204,60,78,78),
            Block("ShopNeighbour", "Западный жилой район", "Г- и П-образный двор", -204,-138,78,60),
            Block("NorthWest", "Северный жилой район", "Короткие секции вокруг зелени", -108,56,72,82),
            Block("NorthCentre", "Северный жилой район", "Двор со ступенчатым ориентиром", -18,54,84,84),
            Block("NorthEast", "Северный жилой район", "Развёрнутый двор с ориентиром", 78,54,84,84),
            Block("RiverNorth", "Приречный жилой район", "Короткие секции вокруг зелени", 180,54,78,84),
            Block("NorthFront", "Северный жилой район", "Низкий фасадный ряд", 78,150,84,36),
            Block("SouthCentre", "Западный жилой район", "Два фасадных ряда", -18,-186,84,36),
            Block("RiverSouth", "Приречный жилой район", "Три корпуса и зелёная полоса", 180,-186,78,36),
            Block("RiverLinear", "Приречный жилой район", "Линейные корпуса и зелёный двор", 180,-78,78,120),
            Block("OfficeNeighbour", "Приречный жилой район", "Смешанный низкий ряд и входной сад", 180,150,78,36)
        };
        foreach (var block in blocks)
        {
            var placements = new List<TownResidentialBuilding>();
            void Place(string id, float x, float z, float yaw = 0)
            {
                if (block.Id == "NorthEast")
                {
                    x = block.Bounds.width - x;
                    z = block.Bounds.height - z;
                    yaw = (yaw + 180) % 360;
                }
                var definition = definitions[id];
                placements.Add(new TownResidentialBuilding(block.Id + "." + placements.Count, definition,
                    new Vector3(block.Bounds.xMin + x, 0, block.Bounds.yMin + z), yaw));
            }
            float w = block.Bounds.width, h = block.Bounds.height;
            if (block.Id == "RiverLinear")
            {
                Place("Sectional3",11,24,270);
                Place("UCourtyard4",26,64);
                Place("LCorner3",18,102,90);
                Place("TownhouseRow6",w-11,27,90);
                Place("Mansard3",w-11,61,90);
                Place("Sectional3",w-11,97,90);
                Place("TownhouseRow6",38,13,180);
                Place("TownhouseRow3",40,33,180);
            }
            else if (h < 45 && w < 80)
            {
                Place("Sectional5",w/2,h-9);
                Place("TownhouseRow3",17,9,180);
                Place("Mansard3",w-18,9,180);
            }
            else if (h < 45)
            {
                Place("Sectional3",21,10,180);
                Place("TownhouseRow3",52,10,180);
                Place("Sectional5",31,h-10);
                Place("Mansard3",w-14,h-10);
            }
            else if (h < 70)
            {
                Place("LCorner3",17,16);
                Place("UCourtyard4",w-23,19);
                Place("Sectional5",w/2,h-9);
            }
            else
            {
                Place(block.Id == "NorthWest" ? "Sectional3" : "Sectional5",w/2,10,180);
                Place(block.Id == "WestNorth" ? "LCorner5" : "LCorner3",17,h<80?29:32);
                Place("TownhouseRow3",w-15,33,180);
                Place("TownhouseRow3",w-15,51,180);
                Place("UCourtyard4",23,h-18,180);
                Place(w >= 84 ? "Stepped469" : "Mansard3",w >= 84 ? w-21 : w-15,h-10);
            }
            block.InitializeBuildings(placements);
        }
        return new TownResidentialPlan(blocks);
    }

    private static TownResidentialBlock Block(string id,string district,string scheme,float x,float z,float w,float h)
        => new TownResidentialBlock(id,district,scheme,new Rect(x,z,w,h));
}

internal sealed class TownResidentialBlock
{
    public string Id { get; }
    public string District { get; }
    public string Scheme { get; }
    public Rect Bounds { get; }
    public IReadOnlyList<TownResidentialBuilding> Buildings { get; private set; } = Array.Empty<TownResidentialBuilding>();
    public IReadOnlyList<TownResidentialWalkway> Walkways { get; private set; } = Array.Empty<TownResidentialWalkway>();
    public IReadOnlyList<TownResidentialWalkway> VehicleApproaches { get; } = Array.Empty<TownResidentialWalkway>();

    public TownResidentialBlock(string id,string district,string scheme,Rect bounds)
    { Id=id; District=district; Scheme=scheme; Bounds=bounds; }
    public void InitializeBuildings(IEnumerable<TownResidentialBuilding> buildings)
    {
        if(Buildings.Count != 0) throw new InvalidOperationException("Parcel buildings are already authored.");
        Buildings=Array.AsReadOnly(buildings.ToArray());
    }
    public void InitializeWalkways(IEnumerable<TownResidentialWalkway> paths)
        => Walkways=Array.AsReadOnly(paths.ToArray());
}

internal sealed class TownResidentialBuilding
{
    public string Id { get; }
    public string ModelId { get; }
    public string Family { get; }
    public Vector3 Position { get; }
    public float Yaw { get; }
    public TownArchitectureModel Model { get; }
    public Matrix4x4 Pose => Matrix4x4.TRS(Position,Quaternion.Euler(0,Yaw,0),Vector3.one);
    public Bounds[] WorldBodies => Model.Volumes.Select(v => TransformBounds(v.BodyBounds)).ToArray();
    public Vector3[] WorldEntrances => Model.Entrances.Select(e => Pose.MultiplyPoint3x4(e.Position)).ToArray();
    public TownResidentialBuilding(string id,TownArchitectureDefinition definition,Vector3 position,float yaw)
    { Id=id; ModelId=definition.Id; Family=definition.Family; Position=position; Yaw=yaw; Model=definition.Build(); }
    public Bounds TransformBounds(Bounds local)
    {
        var result=new Bounds(Pose.MultiplyPoint3x4(local.min),Vector3.zero);
        for(int i=1;i<8;i++) result.Encapsulate(Pose.MultiplyPoint3x4(new Vector3(
            (i&1)==0?local.min.x:local.max.x,(i&2)==0?local.min.y:local.max.y,(i&4)==0?local.min.z:local.max.z)));
        return result;
    }
}

internal sealed class TownResidentialWalkway
{
    public string Id { get; }
    public Vector3 From { get; }
    public Vector3 To { get; }
    public float Width { get; }
    public TownResidentialWalkway(string id,Vector3 from,Vector3 to,float width=2.2f)
    { Id=id; From=from; To=to; Width=width; }
}
