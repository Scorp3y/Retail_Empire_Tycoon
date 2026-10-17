using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Grounded industrial scenery, with explicit real physical pieces and roof/body coverage.</summary>
    internal static class TownSpecialModels
    {
        public static IReadOnlyList<TownSpecialModel> Create()
        {
            return new[] {
                new TownSpecialModel("WorkshopHall", "Сервисный корпус 30×14 м", () => Building(30,14,5.6f,TownColor.Brick,true), "body"),
                new TownSpecialModel("WorkshopOffice", "Офис мастерской 12×8 м", () => Building(12,8,3.1f,TownColor.Cream,false), "body"),
                new TownSpecialModel("WorkshopStore", "Склад мастерской 18×10 м", () => Building(18,10,4.4f,TownColor.Metal,true), "body"),
                new TownSpecialModel("WorkshopCanopy", "Открытый рабочий навес 24×10 м", () => Canopy(24,10,4.5f), "roof"),
                new TownSpecialModel("ConstructionFrame", "Трёхэтажный каркас 24×18 м", Frame, "roof"),
                new TownSpecialModel("TowerCrane", "Башенный кран — статический ориентир", Crane),
                new TownSpecialModel("SiteCabin", "Бытовка 8×4 м", () => Cabin(), "body"),
                new TownSpecialModel("SiteCanopy", "Навес хранения 10×8 м", () => Canopy(10,8,4.2f), "roof"),
                new TownSpecialModel("SiteStore", "Закрытый склад 10×6 м", () => Building(10,6,3.5f,TownColor.Cream,true), "body"),
                new TownSpecialModel("SiteFence", "Трёхметровая секция хозяйственного забора", Fence),
                new TownSpecialModel("Barrels", "Три бочки на поддоне", Barrels),
                new TownSpecialModel("MaterialPallet", "Поддон с блоками", Materials),
                new TownSpecialModel("PipeRack", "Открытый стеллаж труб", Pipes),
                new TownSpecialModel("ToolStation", "Рабочий стол с инструментом", Tools)
            };
        }

        private static TownSpecialDraft Building(float width, float depth, float height, TownColor color, bool loading)
        {
            var d = new TownSpecialDraft(); var m = d.Mesh;
            d.Box("body", Vector3.up * height / 2, new Vector3(width,height,depth), color);
            float entry=loading?width/2-2.3f:0;
            m.Box(new Vector3(0,.18f,0),new Vector3(width+.18f,.36f,depth+.18f),TownColor.Stone);
            m.Gable(new Vector3(0,height,0),width+.4f,depth+.4f,.8f,loading?TownColor.RoofDark:TownColor.Mint,color);
            foreach (float side in new[] {-1f,1f})
                for (float x=-width/2+2; x<width/2-1; x+=4)
                    if (side < 0 || (!loading&&Mathf.Abs(x-entry)>1.95f))
                        TownArchitecturalParts.Window(m,new Vector3(x,2.0f,side*(depth/2+.08f)),1.8f,1.3f,false,side<0);
            if (loading)
            {
                for (float x=-width/2+4; x<width/2-4; x+=7)
                {
                    if(Mathf.Abs(x-entry)<3.2f) continue;
                    TownArchitecturalParts.LoadingDoor(m,new Vector3(x,1.75f,depth/2+.08f),4.2f,3.2f,false,false);
                    m.Box(new Vector3(x,3.45f,depth/2+.13f),new Vector3(4.4f,.18f,.1f),TownColor.Yellow);
                }
            }
            TownArchitecturalParts.Door(m,new Vector3(entry,1.26f,depth/2+.14f),1.6f,2.4f,false);
            d.Box("step",new Vector3(entry,.03f,depth/2+.52f),new Vector3(2.2f,.06f,.9f),TownColor.Stone);
            m.Box(new Vector3(entry,2.85f,depth/2+.65f),new Vector3(2.8f,.15f,1.3f),TownColor.Mint);
            // The vent foot is computed at its X on the gable, not suspended at a fixed ridge height.
            float ventX=-width*.25f, roofY=height+.8f*(1-Mathf.Abs(ventX)/(width/2+.2f));
            m.Box(new Vector3(ventX,roofY+.3f,0),new Vector3(.8f,.65f,.8f),TownColor.Metal);
            return d;
        }

        private static TownSpecialDraft Canopy(float width, float depth, float height)
        {
            var d=new TownSpecialDraft(); var m=d.Mesh;
            foreach(float x in new[]{-width/2+.3f,width/2-.3f})
                foreach(float z in new[]{-depth/2+.3f,depth/2-.3f})
                {
                    d.Box("post"+d.Solids.Count,new Vector3(x,height/2,z),new Vector3(.24f,height,.24f),TownColor.Frame);
                    m.Box(new Vector3(x,.12f,z),new Vector3(.55f,.24f,.55f),TownColor.Stone);
                }
            d.Box("roof",new Vector3(0,height+.12f,0),new Vector3(width,.24f,depth),TownColor.RoofDark);
            foreach(float z in new[]{-depth/2+.3f,depth/2-.3f})
            {
                m.Box(new Vector3(0,height-.32f,z),new Vector3(width,.16f,.14f),TownColor.Frame);
                for(float x=-width/2+.3f; x<width/2-.5f; x+=3)
                {
                    VisualBeam(m,new Vector3(x,height-.65f,z),new Vector3(Mathf.Min(x+3,width/2-.3f),height,z),.09f,TownColor.Metal);
                    VisualBeam(m,new Vector3(x,height,z),new Vector3(Mathf.Min(x+3,width/2-.3f),height-.65f,z),.09f,TownColor.Metal);
                }
            }
            return d;
        }

        private static TownSpecialDraft Cabin()
        {
            var d=new TownSpecialDraft(); var m=d.Mesh;
            d.Box("body",new Vector3(0,1.4f,0),new Vector3(8,2.8f,4),TownColor.Mint);
            m.Box(new Vector3(0,.1f,0),new Vector3(8.2f,.2f,4.2f),TownColor.Frame);
            m.Box(new Vector3(0,2.86f,0),new Vector3(8.2f,.12f,4.2f),TownColor.RoofDark);
            TownArchitecturalParts.Door(m,new Vector3(-2.5f,1.21f,2.1f),1.4f,2.3f,false);
            d.Box("step",new Vector3(-2.5f,.03f,2.48f),new Vector3(2,.06f,.9f),TownColor.Stone);
            TownArchitecturalParts.Window(m,new Vector3(.8f,1.65f,2.08f),2.6f,1.3f);
            for(float x=-3.5f; x<=3.5f; x+=1) m.Box(new Vector3(x,1.4f,-2.025f),new Vector3(.025f,2.6f,.025f),TownColor.Frame);
            return d;
        }

        private static TownSpecialDraft Frame()
        {
            var d=new TownSpecialDraft(); var m=d.Mesh;
            d.Box("foundation",new Vector3(0,.1f,0),new Vector3(24,.2f,18),TownColor.Stone);
            foreach(float x in new[]{-11.6f,-5.8f,0,5.8f,11.6f})
                foreach(float z in new[]{-8.6f,0,8.6f})
                    d.Box("column"+d.Solids.Count,new Vector3(x,4.7f,z),new Vector3(.65f,9.4f,.65f),TownColor.Pavement);
            for(int floor=1; floor<=3; floor++)
                d.Box(floor==3?"roof":"slab"+floor,new Vector3(0,floor*3.1f,0),new Vector3(24,.22f,18),TownColor.Stone);
            for(int floor=0; floor<2; floor++)
                for(int bay=0; bay<3; bay++)
                    d.Box("infill"+d.Solids.Count,new Vector3(-8.7f+bay*5.8f,floor*3.1f+1.1f,8.6f),new Vector3(4.8f,2.0f,.3f),TownColor.Brick);
            // Scaffold is real upright support, not an enclosing collider around the unfinished rooms.
            foreach(float z in new[]{-7f,-3f,1f,5f,8f})
                d.Box("scaffold"+d.Solids.Count,new Vector3(-12.7f,3.3f,z),new Vector3(.1f,6.6f,.1f),TownColor.Wood);
            for(int level=1; level<=2; level++)
                m.Box(new Vector3(-12.5f,level*3.1f,0),new Vector3(1.2f,.12f,16),TownColor.WoodLight);
            return d;
        }

        private static TownSpecialDraft Crane()
        {
            var d=new TownSpecialDraft(); var m=d.Mesh;
            d.Box("base",new Vector3(0,.12f,0),new Vector3(2.8f,.24f,2.8f),TownColor.Stone);
            foreach(float x in new[]{-.7f,.7f})foreach(float z in new[]{-.7f,.7f})
                d.Box("tower"+d.Solids.Count,new Vector3(x,8.2f,z),new Vector3(.16f,16.4f,.16f),TownColor.Yellow);
            for(int level=0; level<8; level++)
                foreach(float z in new[]{-.7f,.7f})
                {
                    VisualBeam(m,new Vector3(-.7f,level*2+.2f,z),new Vector3(.7f,level*2+2.1f,z),.09f,TownColor.Yellow);
                    VisualBeam(m,new Vector3(.7f,level*2+.2f,z),new Vector3(-.7f,level*2+2.1f,z),.09f,TownColor.Yellow);
                }
            d.Box("jib",new Vector3(3,16.6f,0),new Vector3(18,.2f,1.4f),TownColor.Yellow);
            for(float x=-6; x<12; x+=2)
            {
                VisualBeam(m,new Vector3(x,16.5f,.6f),new Vector3(x+2,17.5f,.6f),.09f,TownColor.Yellow);
                VisualBeam(m,new Vector3(x,17.5f,.6f),new Vector3(x+2,16.5f,.6f),.09f,TownColor.Yellow);
                VisualBeam(m,new Vector3(x,17.5f,-.6f),new Vector3(x+2,17.5f,-.6f),.09f,TownColor.Yellow);
            }
            d.Box("counterweight",new Vector3(-4.8f,16.8f,0),new Vector3(2,1.2f,1.2f),TownColor.Stone);
            m.Box(new Vector3(1.3f,15.3f,0),new Vector3(1.4f,1.5f,1.5f),TownColor.Mint);
            m.Box(new Vector3(2.015f,15.4f,0),new Vector3(.03f,.9f,1.1f),TownColor.Glass);
            VisualBeam(m,new Vector3(10,16.5f,0),new Vector3(10,2.4f,0),.04f,TownColor.Frame);
            d.Box("hook",new Vector3(10,2.3f,0),new Vector3(.25f,.4f,.25f),TownColor.Frame);
            return d;
        }

        private static TownSpecialDraft Fence()
        {
            var d=new TownSpecialDraft();
            d.Box("panel",new Vector3(0,1,0),new Vector3(3,1.9f,.12f),TownColor.Metal);
            foreach(float x in new[]{-1.44f,1.44f})
                d.Box("post"+d.Solids.Count,new Vector3(x,1.05f,0),new Vector3(.12f,2.1f,.16f),TownColor.Frame);
            for(float x=-1.2f; x<1.4f; x+=.3f)
                d.Mesh.Box(new Vector3(x,1,.075f),new Vector3(.035f,1.8f,.025f),TownColor.Frame);
            return d;
        }

        private static TownSpecialDraft Barrels()
        {
            var d=new TownSpecialDraft();
            d.Box("pallet",new Vector3(0,.1f,0),new Vector3(1.8f,.2f,1.4f),TownColor.Wood);
            for(int i=0;i<3;i++)
            {
                float x=-.6f+i*.6f;
                d.Mesh.Cylinder(new Vector3(x,.75f,0),.26f,.26f,1.1f,i==1?TownColor.Red:TownColor.Blue);
                d.Solids.Add(new TownSpecialSolid("barrel"+i,new Vector3(x,.75f,0),new Vector3(.52f,1.1f,.52f)));
                foreach(float y in new[]{.3f,1.15f}) d.Mesh.Cylinder(new Vector3(x,y,0),.275f,.275f,.055f,TownColor.Frame);
            }
            return d;
        }

        private static TownSpecialDraft Materials()
        {
            var d=new TownSpecialDraft();
            d.Box("pallet",new Vector3(0,.1f,0),new Vector3(3,.2f,2),TownColor.Wood);
            for(int y=0;y<3;y++)for(int x=0;x<4;x++)for(int z=0;z<2;z++)
                d.Box("block"+d.Solids.Count,new Vector3(-1.05f+x*.7f,.34f+y*.28f,-.45f+z*.9f),new Vector3(.64f,.28f,.8f),TownColor.Pavement);
            return d;
        }

        private static TownSpecialDraft Pipes()
        {
            var d=new TownSpecialDraft();
            foreach(float x in new[]{-1.8f,1.8f})foreach(float z in new[]{-.7f,.7f})
                d.Box("post"+d.Solids.Count,new Vector3(x,1.1f,z),new Vector3(.14f,2.2f,.14f),TownColor.Frame);
            foreach(float y in new[]{.3f,1.2f})
            {
                d.Box("shelf"+d.Solids.Count,new Vector3(0,y,0),new Vector3(4,.12f,1.8f),TownColor.Metal);
                for(int i=-2;i<=2;i++) d.Mesh.Cylinder(new Vector3(0,y+.18f,i*.3f),.12f,.12f,3.6f,TownColor.Pavement,8,Quaternion.Euler(0,0,90));
            }
            return d;
        }

        private static TownSpecialDraft Tools()
        {
            var d=new TownSpecialDraft();
            foreach(float x in new[]{-1.3f,1.3f})foreach(float z in new[]{-.4f,.4f})
                d.Box("leg"+d.Solids.Count,new Vector3(x,.5f,z),new Vector3(.12f,1,.12f),TownColor.Frame);
            d.Box("top",new Vector3(0,1.05f,0),new Vector3(3,.12f,1),TownColor.WoodLight);
            d.Box("back",new Vector3(0,1.45f,-.45f),new Vector3(3,.8f,.08f),TownColor.Metal);
            for(int i=0;i<6;i++)d.Mesh.Box(new Vector3(-1+i*.4f,1.5f,-.39f),new Vector3(.08f,.4f,.08f),TownColor.Yellow);
            return d;
        }

        private static void VisualBeam(TownMeshBuilder m,Vector3 a,Vector3 b,float width,TownColor color)
        {
            var delta=b-a;
            m.Box((a+b)/2,new Vector3(width,delta.magnitude,width),color,Quaternion.FromToRotation(Vector3.up,delta.normalized));
        }
    }

    internal sealed class TownSpecialDraft
    {
        public TownMeshBuilder Mesh { get; } = new TownMeshBuilder();
        public List<TownSpecialSolid> Solids { get; } = new List<TownSpecialSolid>();
        public void Box(string name,Vector3 centre,Vector3 size,TownColor color)
        { Mesh.Box(centre,size,color); Solids.Add(new TownSpecialSolid(name,centre,size)); }
    }

    internal sealed class TownSpecialModel
    {
        private readonly Func<TownSpecialDraft> compose;
        public string Id { get; }
        public string Label { get; }
        public string CoveragePart { get; }
        public Bounds VisualBounds { get; }
        public IReadOnlyList<TownSpecialSolid> Solids { get; }
        public TownSpecialModel(string id,string label,Func<TownSpecialDraft> compose,string coveragePart=null)
        {
            Id=id; Label=label; this.compose=compose; CoveragePart=coveragePart;
            var draft=compose(); var mesh=draft.Mesh.Build(id);
            try
            {
                VisualBounds=mesh.bounds;
                if(Mathf.Abs(mesh.bounds.min.y)>.0001f) throw new InvalidOperationException(id+": model must start at ground zero; actual minimum "+mesh.bounds.min.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                if(draft.Solids.Select(s=>s.Id).Distinct().Count()!=draft.Solids.Count) throw new InvalidOperationException(id+": duplicate physical piece.");
                Solids=Array.AsReadOnly(draft.Solids.ToArray());
                if(coveragePart!=null&&!Solids.Any(s=>s.Id==coveragePart)) throw new InvalidOperationException(id+": missing real body/roof coverage piece.");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        public Mesh BuildMesh() => compose().Mesh.Build(Id);
    }

    internal sealed class TownSpecialSolid
    {
        public string Id { get; }
        public Vector3 Centre { get; }
        public Vector3 Size { get; }
        public Bounds Bounds => new Bounds(Centre,Size);
        public TownSpecialSolid(string id,Vector3 centre,Vector3 size)
        { Id=id; Centre=centre; Size=size; }
    }
}
