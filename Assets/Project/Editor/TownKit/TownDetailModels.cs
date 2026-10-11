using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Grounded, metre-scale visual models. No stock, supplier or traffic behaviour.</summary>
    internal static partial class TownDetailModels
    {
        public static List<TownAssetDefinition> Create()
        {
            var models=new List<TownAssetDefinition>();
            void Add(string category,string id,string label,Action<TownMeshBuilder> draw)
                =>models.Add(new TownAssetDefinition(category,id,label,()=>{var mesh=new TownMeshBuilder();draw(mesh);return mesh;}));
            Add("Buildings","CornerCafe","Угловое кафе",m=>Building(m,24,14,2,TownColor.Brick,"КАФЕ",true));
            Add("Buildings","PostOffice","Почта",m=>Building(m,20,12,2,TownColor.Cream,"ПОЧТА",false));
            Add("Buildings","AutoWorkshop","Городская мастерская",Workshop);
            Add("Buildings","TownhousePair","Два таунхауса",Townhouses);
            Add("Buildings","OfficeAnnex","Невысокий офис",m=>Building(m,20,12,3,TownColor.Cream,"",false));
            Add("Street","BicycleRack","Велопарковка с велосипедами",Bikes);
            Add("Street","BusShelter","Стеклянная остановка",Shelter);
            Add("Street","DrainGrate","Ливневая решётка",Drain);
            Add("Street","ManholeCover","Канализационный люк",Manhole);
            Add("Street","UtilityCabinet","Шкаф городских коммуникаций",Cabinet);
            Add("Street","BollardGroup","Три столбика",m=>{for(int i=-1;i<=1;i++){m.Cylinder(new Vector3(i*1.2f,.45f,0),.1f,.08f,.9f,TownColor.Frame);m.Cylinder(new Vector3(i*1.2f,.7f,0),.1f,.1f,.12f,TownColor.White);}});
            Add("Street","TrafficSignal","Светофор — визуальная модель",Signal);
            Add("Courts","PlaySlide","Горка",Slide);
            Add("Courts","SwingSet","Качели",Swings);
            Add("Courts","Sandpit","Песочница",Sandpit);
            Add("Courts","OutdoorGym","Спортивные турники",Gym);
            Add("Courts","BasketballCourt","Площадка для баскетбола",Court);
            Add("Courts","BinEnclosure","Огороженная площадка контейнеров",Bins);
            Add("Courts","Pergola","Пергола со скамьёй",Pergola);
            Add("Courts","CafeTerrace","Терраса с зонтиками",Terrace);
            Add("Landmarks","ClockColumn","Городские часы",Clock);
            Add("Landmarks","ParkArch","Входная арка парка",Arch);
            Add("Landmarks","RiverReeds","Прибрежный камыш",Reeds);
            Add("Facades","EntranceCanopy","Входной козырёк",Canopy);
            Add("Facades","AirConditioner","Внешний блок кондиционера",AirConditioner);
            Add("Facades","VentilationBank","Крышная вентиляция",Vents);
            Add("Facades","Downpipe","Водосточная труба",Downpipe);
            Add("Facades","RoofAerial","Антенна",Aerial);
            Add("Facades","StairLanding","Крыльцо с перилами",Steps);
            Add("PlayerShop","TrolleyCorral","Навес для тележек",Trolleys);
            for(int pose=0;pose<3;pose++){int frame=pose;Add("People","TownWalker"+pose,"Пешеход: поза "+pose,m=>Person(m,frame));}
            AddNeighbourhoodModels(models);
            return models;
        }

        private static void Beam(TownMeshBuilder m,Vector3 from,Vector3 to,float width,TownColor color)
        {
            Vector3 delta=to-from;
            m.Box((from+to)*.5f,new Vector3(width,delta.magnitude,width),color,Quaternion.FromToRotation(Vector3.up,delta.normalized));
        }
        private static void Building(TownMeshBuilder m,float width,float depth,int floors,TownColor color,string label,bool corner)
        {
            float height=floors*3.1f;
            m.Box(new Vector3(0,height/2,0),new Vector3(width,height,depth),color);
            m.Box(new Vector3(0,.3f,0),new Vector3(width+.2f,.6f,depth+.2f),TownColor.Stone);
            for(int floor=0;floor<floors;floor++)
            {
                float y=1.7f+floor*3.1f;
                foreach(float face in new[]{-1f,1f})for(float x=-width/2+2;x<width/2;x+=4)
                {
                    // The complete ground-floor entrance bay is solid, not covered with glass.
                    if(floor==0&&face>0&&Mathf.Abs(x)<3)continue;
                    TownArchitecturalParts.Window(m,new Vector3(x,y,face*(depth/2+.08f)),floor==0?2.7f:2.2f,1.8f,false,face<0);
                }
                foreach(float side in new[]{-1f,1f})for(float z=-depth/2+2;z<depth/2;z+=3.5f)
                    TownArchitecturalParts.Window(m,new Vector3(side*(width/2+.08f),y,z),2,1.8f,true,side<0);
                m.Box(new Vector3(0,(floor+1)*3.1f,0),new Vector3(width+.2f,.14f,depth+.2f),TownColor.Pavement);
            }
            TownArchitecturalParts.Door(m,new Vector3(0,1.25f,depth/2+.18f),1.8f,2.4f,true);
            m.Box(new Vector3(0,2.95f,depth/2+1.05f),new Vector3(4.4f,.18f,2.2f),TownColor.Mint);
            if(label.Length>0)TownArchitecturalParts.Sign(m,new Vector3(0,3.45f,depth/2+.22f),4.4f,.65f,label);
            TownArchitecturalParts.FlatRoof(m,width,depth,height);
            if(corner)
            {
                // The canopy continues around the corner and gives the street a recognisable silhouette.
                m.Box(new Vector3(width/2+.8f,2.95f,1),new Vector3(1.8f,.18f,depth-2),TownColor.Mint);
                m.Box(new Vector3(width/2-.25f,height/2,depth/2-.25f),new Vector3(.65f,height,.65f),TownColor.Frame);
            }
        }
        private static void Workshop(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,2.4f,0),new Vector3(20,4.8f,12),TownColor.Brick);
            m.Box(new Vector3(0,.3f,0),new Vector3(20.2f,.6f,12.2f),TownColor.Stone);
            TownArchitecturalParts.FlatRoof(m,20,12,4.8f);
            foreach(float x in new[]{-5f,1f})TownArchitecturalParts.LoadingDoor(m,new Vector3(x,1.8f,6.08f),4,3.5f);
            TownArchitecturalParts.Door(m,new Vector3(7.5f,1.25f,6.15f),1.6f,2.4f,false);
            TownArchitecturalParts.Sign(m,new Vector3(0,4.15f,6.15f),7,.65f,"СЕРВИС");
            for(int i=0;i<3;i++)TownArchitecturalParts.Window(m,new Vector3(-6+i*6,2.5f,-6.08f),3,1.4f,false,true);
        }
        private static void Townhouses(TownMeshBuilder m)
        {
            foreach(float x in new[]{-4.6f,4.6f})
            {
                var color=x<0?TownColor.Brick:TownColor.Cream;
                m.Box(new Vector3(x,4.3f,0),new Vector3(8.8f,8.6f,12),color);
                m.Box(new Vector3(x,.3f,0),new Vector3(9,.6f,12.2f),TownColor.Stone);
                m.Gable(new Vector3(x,8.6f,0),9.1f,12.4f,2.1f,TownColor.RoofDark,color);
                TownArchitecturalParts.Door(m,new Vector3(x,1.25f,6.12f),1.6f,2.4f,false);
                for(int floor=0;floor<3;floor++)foreach(float offset in new[]{-2.5f,2.5f})foreach(float face in new[]{-1f,1f})
                    TownArchitecturalParts.Window(m,new Vector3(x+offset,1.7f+floor*2.8f,face*6.08f),1.7f,1.6f,false,face<0);
                m.Box(new Vector3(x,2.85f,6.7f),new Vector3(2.8f,.15f,1.4f),TownColor.Mint);
                m.Box(new Vector3(x+2,10,0),new Vector3(.7f,2,.7f),TownColor.Brick);
            }
        }
        private static void Bikes(TownMeshBuilder m)
        {
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*.75f;
                Beam(m,new Vector3(x,0,-.45f),new Vector3(x,.8f,-.45f),.055f,TownColor.Metal);
                Beam(m,new Vector3(x,.8f,-.45f),new Vector3(x,.8f,.45f),.055f,TownColor.Metal);
                Beam(m,new Vector3(x,.8f,.45f),new Vector3(x,0,.45f),.055f,TownColor.Metal);
                var rear=new Vector3(x,.35f,-.7f);var front=new Vector3(x,.35f,.7f);
                foreach(var centre in new[]{rear,front})for(int s=0;s<12;s++)
                {
                    float a=s*Mathf.PI/6,b=(s+1)*Mathf.PI/6;
                    Beam(m,centre+new Vector3(0,Mathf.Cos(a)*.34f,Mathf.Sin(a)*.34f),centre+new Vector3(0,Mathf.Cos(b)*.34f,Mathf.Sin(b)*.34f),.045f,TownColor.Rubber);
                }
                Vector3 crank=new Vector3(x,.4f,-.08f),seat=new Vector3(x,.87f,-.3f),handle=new Vector3(x,.9f,.45f);
                var color=i==0?TownColor.Red:i==1?TownColor.Blue:TownColor.Yellow;
                foreach(var pair in new[]{(rear,seat),(seat,crank),(crank,rear),(seat,handle),(handle,crank),(handle,front)})Beam(m,pair.Item1,pair.Item2,.055f,color);
                m.Box(seat+Vector3.up*.05f,new Vector3(.18f,.08f,.25f),TownColor.Rubber);
                Beam(m,handle+Vector3.up*.15f+Vector3.left*.2f,handle+Vector3.up*.15f+Vector3.right*.2f,.045f,TownColor.Frame);
            }
        }
        private static void Shelter(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.025f,0),new Vector3(6,.05f,2.8f),TownColor.Pavement);
            foreach(float x in new[]{-2.8f,2.8f})foreach(float z in new[]{-1.2f,1.2f})m.Box(new Vector3(x,1.35f,z),new Vector3(.12f,2.7f,.12f),TownColor.Frame);
            m.Box(new Vector3(0,2.8f,0),new Vector3(6.2f,.2f,3),TownColor.Mint);
            m.Box(new Vector3(0,1.4f,-1.2f),new Vector3(5.5f,2.4f,.045f),TownColor.Glass);
            m.Box(new Vector3(-2.8f,1.4f,0),new Vector3(.045f,2.4f,2.3f),TownColor.Glass);
            m.Box(new Vector3(0,.52f,-.65f),new Vector3(3.3f,.15f,.5f),TownColor.Wood);
            foreach(float x in new[]{-1.3f,1.3f})m.Box(new Vector3(x,.25f,-.65f),new Vector3(.12f,.5f,.4f),TownColor.Frame);
            m.Box(new Vector3(2,1.65f,-1.15f),new Vector3(.8f,1,.06f),TownColor.Cream);
            for(int i=0;i<5;i++)m.Box(new Vector3(2,1.9f-i*.12f,-1.1f),new Vector3(.6f,.035f,.01f),TownColor.Frame);
        }
        private static void Drain(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.008f,0),new Vector3(.9f,.016f,.5f),TownColor.Frame);
            for(int i=0;i<8;i++)m.Box(new Vector3(-.39f+i*.11f,.02f,0),new Vector3(.055f,.015f,.43f),TownColor.Metal);
        }
        private static void Manhole(TownMeshBuilder m)
        {
            m.Cylinder(new Vector3(0,.015f,0),.43f,.43f,.03f,TownColor.Metal,12);
            m.Cylinder(new Vector3(0,.033f,0),.36f,.36f,.007f,TownColor.Frame,12);
            for(int i=-2;i<=2;i++)m.Ground(i*.12f,0,.035f,.54f,.04f,TownColor.Metal);
        }
        private static void Cabinet(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.08f,0),new Vector3(1,.16f,.7f),TownColor.Stone);
            m.Box(new Vector3(0,.85f,0),new Vector3(.8f,1.4f,.5f),TownColor.Metal);
            m.Box(new Vector3(0,.85f,.26f),new Vector3(.74f,1.25f,.035f),TownColor.Cream);
            m.Box(new Vector3(.25f,.85f,.29f),new Vector3(.04f,.14f,.02f),TownColor.Frame);
            for(int i=0;i<4;i++)m.Box(new Vector3(0,.45f+i*.075f,.29f),new Vector3(.5f,.025f,.02f),TownColor.Frame);
        }
        private static void Signal(TownMeshBuilder m)
        {
            m.Cylinder(new Vector3(0,1.7f,0),.1f,.085f,3.4f,TownColor.Frame);
            m.Box(new Vector3(0,2.9f,.14f),new Vector3(.4f,1.1f,.28f),TownColor.Charcoal);
            for(int i=0;i<3;i++)m.Cylinder(new Vector3(0,3.22f-i*.32f,.3f),.115f,.115f,.03f,i==0?TownColor.Red:TownColor.Charcoal,8,Quaternion.Euler(90,0,0));
        }
        private static void Slide(TownMeshBuilder m)
        {
            foreach(float x in new[]{-.7f,.7f})foreach(float z in new[]{-.6f,.6f})m.Box(new Vector3(x,1,z),new Vector3(.12f,2,.12f),TownColor.Wood);
            m.Box(new Vector3(0,1.85f,0),new Vector3(1.7f,.15f,1.5f),TownColor.WoodLight);
            m.Gable(new Vector3(0,2.75f,0),2,1.9f,.6f,TownColor.Mint,TownColor.Yellow);
            foreach(float x in new[]{-.7f,.7f})Beam(m,new Vector3(x,1.9f,.65f),new Vector3(x,.2f,3.3f),.12f,TownColor.Orange);
            m.Box(new Vector3(0,1.05f,1.97f),new Vector3(1.25f,.08f,3.15f),TownColor.Yellow,Quaternion.Euler(33,0,0));
            foreach(float x in new[]{-.55f,.55f})Beam(m,new Vector3(x,0,-2),new Vector3(x,1.9f,-.65f),.09f,TownColor.Frame);
            for(int i=1;i<6;i++)Beam(m,new Vector3(-.55f,i*.3f,-2+i*.21f),new Vector3(.55f,i*.3f,-2+i*.21f),.07f,TownColor.Frame);
        }
        private static void Swings(TownMeshBuilder m)
        {
            foreach(float x in new[]{-2.5f,2.5f})foreach(float z in new[]{-1.3f,1.3f})Beam(m,new Vector3(x,0,z),new Vector3(x,2.7f,0),.11f,TownColor.Mint);
            Beam(m,new Vector3(-2.6f,2.7f,0),new Vector3(2.6f,2.7f,0),.15f,TownColor.Frame);
            foreach(float x in new[]{-1.2f,1.2f})
            {
                foreach(float side in new[]{-.3f,.3f})Beam(m,new Vector3(x+side,.55f,0),new Vector3(x+side,2.7f,0),.025f,TownColor.Metal);
                m.Box(new Vector3(x,.55f,0),new Vector3(.8f,.12f,.4f),TownColor.Wood);
            }
        }
        private static void Sandpit(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.12f,0),new Vector3(4,.24f,3),TownColor.Wood);
            m.Ground(0,0,3.7f,2.7f,.245f,TownColor.Sand);
            m.Box(new Vector3(-1.6f,.29f,0),new Vector3(.7f,.1f,3),TownColor.WoodLight);
            m.Box(new Vector3(1.6f,.29f,0),new Vector3(.7f,.1f,3),TownColor.WoodLight);
            m.Cylinder(new Vector3(.4f,.4f,.3f),.18f,.22f,.3f,TownColor.Blue);
        }
        private static void Gym(TownMeshBuilder m)
        {
            for(int i=0;i<3;i++)m.Cylinder(new Vector3(-2+i*2,1.2f+i*.2f,0),.07f,.07f,2.4f+i*.4f,TownColor.Frame);
            Beam(m,new Vector3(-2,2.4f,0),new Vector3(0,2.4f,0),.09f,TownColor.Yellow);
            Beam(m,new Vector3(0,2.8f,0),new Vector3(2,2.8f,0),.09f,TownColor.Yellow);
            for(int i=0;i<6;i++)Beam(m,new Vector3(0,.3f+i*.35f,0),new Vector3(2,.3f+i*.35f,0),.06f,TownColor.Metal);
            foreach(float x in new[]{-1f,1f}){m.Cylinder(new Vector3(x,.6f,2),.06f,.06f,1.2f,TownColor.Frame);Beam(m,new Vector3(x,.9f,1.5f),new Vector3(x,.9f,2.5f),.08f,TownColor.Yellow);}
        }
        private static void Court(TownMeshBuilder m)
        {
            m.Ground(0,0,16,14,0,TownColor.GreenStripe);
            foreach(float x in new[]{-7.5f,7.5f})m.Ground(x,0,.12f,13,.01f,TownColor.White);
            foreach(float z in new[]{-6.5f,6.5f})m.Ground(0,z,15,.12f,.01f,TownColor.White);
            foreach(float x in new[]{-2.4f,2.4f})m.Ground(x,3.6f,.12f,5.8f,.01f,TownColor.White);
            m.Ground(0,.7f,4.8f,.12f,.01f,TownColor.White);
            m.Arc(new Vector3(0,.012f,6),5.7f,5.82f,180,360,TownColor.White,20);
            m.Box(new Vector3(0,1.75f,6.3f),new Vector3(.16f,3.5f,.16f),TownColor.Frame);
            m.Box(new Vector3(0,3.35f,5.8f),new Vector3(1.8f,1.1f,.12f),TownColor.Cream);
            m.Arc(new Vector3(0,3.05f,5.35f),.27f,.31f,0,360,TownColor.Orange,12);
            foreach(float x in new[]{-7.9f,7.9f})foreach(float z in new[]{-6.7f,0,6.7f})m.Box(new Vector3(x,1,z),new Vector3(.06f,2,.06f),TownColor.Frame);
            foreach(float x in new[]{-7.9f,7.9f})for(int i=0;i<6;i++)Beam(m,new Vector3(x,.25f+i*.3f,-6.7f),new Vector3(x,.25f+i*.3f,6.7f),.025f,TownColor.Metal);
        }
        private static void Bins(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.025f,0),new Vector3(5,.05f,3.4f),TownColor.Pavement);
            foreach(float x in new[]{-2.4f,2.4f})m.Box(new Vector3(x,.85f,0),new Vector3(.14f,1.7f,3.4f),TownColor.Frame);
            for(int i=0;i<7;i++)m.Box(new Vector3(0,.2f+i*.24f,-1.6f),new Vector3(4.9f,.13f,.12f),TownColor.Wood);
            foreach(float x in new[]{-1.25f,1.25f})
            {
                m.Box(new Vector3(x,.65f,-.35f),new Vector3(1.3f,1.1f,1.2f),TownColor.LeafDark);
                m.Box(new Vector3(x,1.25f,-.35f),new Vector3(1.4f,.16f,1.3f),TownColor.Frame);
                foreach(float offset in new[]{-.5f,.5f})m.Cylinder(new Vector3(x+offset,.12f,-.35f),.12f,.12f,.12f,TownColor.Rubber,8,Quaternion.Euler(0,0,90));
            }
        }
        private static void Pergola(TownMeshBuilder m)
        {
            foreach(float x in new[]{-3f,3f})foreach(float z in new[]{-1.7f,1.7f})m.Box(new Vector3(x,1.4f,z),new Vector3(.18f,2.8f,.18f),TownColor.Wood);
            foreach(float z in new[]{-1.7f,1.7f})m.Box(new Vector3(0,2.85f,z),new Vector3(6.6f,.2f,.16f),TownColor.Wood);
            for(int i=0;i<9;i++)m.Box(new Vector3(-3.2f+i*.8f,3,0),new Vector3(.14f,.16f,4.2f),TownColor.WoodLight);
            for(int i=0;i<4;i++)m.Ellipsoid(new Vector3(-2.5f+i*1.6f,3.05f,-1.4f),new Vector3(.9f,.13f,.5f),TownColor.Leaf,6,3);
            m.Box(new Vector3(0,.5f,-1),new Vector3(4,.12f,.65f),TownColor.WoodLight);
            foreach(float x in new[]{-1.5f,1.5f})m.Box(new Vector3(x,.25f,-1),new Vector3(.12f,.5f,.6f),TownColor.Frame);
        }
        private static void Terrace(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.035f,0),new Vector3(8,.07f,5),TownColor.WoodLight);
            foreach(float x in new[]{-2f,2f})
            {
                m.Cylinder(new Vector3(x,1.4f,0),.04f,.04f,2.8f,TownColor.Frame);
                m.Cylinder(new Vector3(x,2.65f,0),1.65f,.08f,.45f,TownColor.Mint,8);
                m.Cylinder(new Vector3(x,.85f,0),.75f,.75f,.1f,TownColor.Cream,8);
                foreach(float z in new[]{-1.1f,1.1f})
                {
                    m.Box(new Vector3(x,.5f,z),new Vector3(.65f,.1f,.65f),TownColor.Wood);
                    m.Box(new Vector3(x,.8f,z+(z>0?.3f:-.3f)),new Vector3(.65f,.6f,.1f),TownColor.Wood);
                    foreach(float side in new[]{-.25f,.25f})m.Box(new Vector3(x+side,.25f,z),new Vector3(.06f,.5f,.5f),TownColor.Frame);
                }
            }
        }
        private static void Clock(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.15f,0),new Vector3(1.8f,.3f,1.8f),TownColor.Stone);
            m.Cylinder(new Vector3(0,2.2f,0),.19f,.15f,4.2f,TownColor.Frame);
            foreach(float face in new[]{-1f,1f})
            {
                m.Cylinder(new Vector3(0,4.45f,face*.14f),.72f,.72f,.14f,TownColor.Frame,12,Quaternion.Euler(90,0,0));
                m.Cylinder(new Vector3(0,4.45f,face*.23f),.63f,.63f,.03f,TownColor.Cream,12,Quaternion.Euler(90,0,0));
                for(int i=0;i<12;i++){float a=i*Mathf.PI/6;m.Box(new Vector3(Mathf.Sin(a)*.51f,4.45f+Mathf.Cos(a)*.51f,face*.25f),new Vector3(.05f,.08f,.018f),TownColor.Frame,Quaternion.Euler(0,0,-i*30));}
                Beam(m,new Vector3(0,4.45f,face*.27f),new Vector3(.3f,4.65f,face*.27f),.045f,TownColor.Frame);
                Beam(m,new Vector3(0,4.45f,face*.27f),new Vector3(0,4.9f,face*.27f),.035f,TownColor.Frame);
            }
        }
        private static void Arch(TownMeshBuilder m)
        {
            foreach(float x in new[]{-3.5f,3.5f}){m.Box(new Vector3(x,1.5f,0),new Vector3(.4f,3,.4f),TownColor.Frame);m.Box(new Vector3(x,.25f,0),new Vector3(.8f,.5f,.8f),TownColor.Stone);}
            m.Box(new Vector3(0,3.1f,0),new Vector3(7.5f,.3f,.4f),TownColor.Wood);
            m.Box(new Vector3(0,3.4f,0),new Vector3(5,.18f,.6f),TownColor.Mint);
            for(int i=-2;i<=2;i++)m.Ellipsoid(new Vector3(i*1.3f,3.6f,0),new Vector3(.7f,.3f,.4f),TownColor.Leaf,6,3);
        }
        private static void Reeds(TownMeshBuilder m)
        {
            for(int i=0;i<14;i++)
            {
                float x=Mathf.Sin(i*2.3f)*.75f,z=Mathf.Cos(i*1.7f)*.5f,height=.65f+(i%4)*.15f;
                Beam(m,new Vector3(x,0,z),new Vector3(x+.12f,height,z),.025f,TownColor.LeafDark);
                Beam(m,new Vector3(x,height*.35f,z),new Vector3(x-.25f,height*.8f,z+.12f),.035f,TownColor.Leaf);
                m.Cylinder(new Vector3(x+.12f,height,z),.045f,.035f,.22f,TownColor.Wood,5);
            }
        }
        private static void Canopy(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.1f,0),new Vector3(3.6f,.2f,1.7f),TownColor.Mint);
            foreach(float x in new[]{-1.2f,1.2f})Beam(m,new Vector3(x,0,-.7f),new Vector3(x,-.5f,.4f),.055f,TownColor.Frame);
        }
        private static void AirConditioner(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.32f,0),new Vector3(1,.64f,.45f),TownColor.Cream);
            m.Cylinder(new Vector3(-.15f,.34f,.24f),.23f,.23f,.02f,TownColor.Frame,12,Quaternion.Euler(90,0,0));
            for(int i=0;i<5;i++)m.Box(new Vector3(.32f,.17f+i*.075f,.25f),new Vector3(.2f,.025f,.025f),TownColor.Metal);
            foreach(float x in new[]{-.36f,.36f})m.Box(new Vector3(x,-.05f,0),new Vector3(.06f,.1f,.7f),TownColor.Frame);
        }
        private static void Vents(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.3f,0),new Vector3(2,.6f,1.2f),TownColor.Metal);
            foreach(float x in new[]{-.5f,.5f}){m.Cylinder(new Vector3(x,.65f,0),.38f,.38f,.1f,TownColor.Frame,10);m.Cylinder(new Vector3(x,.72f,0),.3f,.3f,.04f,TownColor.Charcoal,10);}
            m.Cylinder(new Vector3(1.15f,.9f,0),.17f,.17f,1.8f,TownColor.Metal);
            m.Cylinder(new Vector3(1.15f,1.82f,0),.25f,.25f,.08f,TownColor.Frame);
        }
        private static void Downpipe(TownMeshBuilder m)
        {
            m.Cylinder(new Vector3(0,3,0),.06f,.06f,6,TownColor.Frame,6);
            for(int i=0;i<4;i++)m.Box(new Vector3(0,.6f+i*1.6f,-.06f),new Vector3(.18f,.055f,.15f),TownColor.Metal);
            Beam(m,new Vector3(0,.1f,0),new Vector3(0,0,.25f),.12f,TownColor.Frame);
        }
        private static void Aerial(TownMeshBuilder m)
        {
            m.Cylinder(new Vector3(0,1,0),.035f,.025f,2,TownColor.Metal,6);
            Beam(m,new Vector3(-1,1.8f,0),new Vector3(1,1.8f,0),.04f,TownColor.Frame);
            for(int i=0;i<6;i++)Beam(m,new Vector3(-.85f+i*.34f,1.8f,-.35f),new Vector3(-.85f+i*.34f,1.8f,.35f),.025f,TownColor.Metal);
        }
        private static void Steps(TownMeshBuilder m)
        {
            for(int i=0;i<3;i++)m.Box(new Vector3(0,.08f*(i+1),-.3f*i),new Vector3(2.5f,.16f*(i+1),1.8f-.6f*i),TownColor.Pavement);
            foreach(float x in new[]{-1.2f,1.2f}){foreach(float z in new[]{-.8f,.8f})m.Box(new Vector3(x,.55f,z),new Vector3(.04f,1.1f,.04f),TownColor.Frame);Beam(m,new Vector3(x,1.1f,-.8f),new Vector3(x,.9f,.8f),.045f,TownColor.Frame);}
        }
        private static void Trolleys(TownMeshBuilder m)
        {
            foreach(float x in new[]{-1.7f,1.7f})foreach(float z in new[]{-.9f,.9f})m.Box(new Vector3(x,1.1f,z),new Vector3(.09f,2.2f,.09f),TownColor.Frame);
            m.Box(new Vector3(0,2.3f,0),new Vector3(3.7f,.15f,2.1f),TownColor.Mint);
            for(int i=0;i<3;i++)
            {
                float x=-.95f+i*.8f;
                for(int bar=0;bar<4;bar++)
                {
                    foreach(float side in new[]{-.27f,.27f})m.Box(new Vector3(x+side,.55f+bar*.12f,0),new Vector3(.025f,.025f,.7f),TownColor.Metal);
                    foreach(float end in new[]{-.35f,.35f})m.Box(new Vector3(x,.55f+bar*.12f,end),new Vector3(.55f,.025f,.025f),TownColor.Metal);
                }
                // Open basket sides; no solid cargo block disguising the trolley silhouette.
                foreach(float side in new[]{-.27f,.27f})for(int bar=0;bar<4;bar++)m.Box(new Vector3(x+side,.78f, -.3f+bar*.2f),new Vector3(.025f,.5f,.025f),TownColor.Frame);
                Beam(m,new Vector3(x,.4f,-.35f),new Vector3(x,.95f,-.6f),.05f,TownColor.Frame);
                m.Box(new Vector3(x,.95f,-.6f),new Vector3(.6f,.06f,.06f),TownColor.Mint);
                m.Box(new Vector3(x,.53f,0),new Vector3(.55f,.025f,.7f),TownColor.Metal);
                m.Box(new Vector3(x,.2f,0),new Vector3(.48f,.035f,.68f),TownColor.Frame);
                foreach(float side in new[]{-.22f,.22f})foreach(float z in new[]{-.3f,.3f})
                    m.Box(new Vector3(x+side,.34f,z),new Vector3(.035f,.35f,.035f),TownColor.Frame);
                foreach(float side in new[]{-.22f,.22f})foreach(float z in new[]{-.3f,.3f})m.Cylinder(new Vector3(x+side,.1f,z),.09f,.09f,.065f,TownColor.Rubber,6,Quaternion.Euler(0,0,90));
            }
        }
        private static void Person(TownMeshBuilder m,int pose,int appearance=0)
        {
            float stride=pose==0?0:pose==1?.27f:-.27f;
            var shirt=appearance==1?TownColor.Cream:appearance==2?TownColor.Orange:appearance==3?TownColor.Pink:TownColor.Blue;
            m.Box(new Vector3(0,1.12f,0),new Vector3(.42f,.55f,.26f),shirt);
            m.Ellipsoid(new Vector3(0,1.62f,0),new Vector3(.16f,.2f,.15f),TownColor.Sand,6,3);
            m.Ellipsoid(new Vector3(0,1.78f,-.035f),new Vector3(.17f,.08f,.15f),appearance==2?TownColor.Yellow:appearance==1?TownColor.Charcoal:TownColor.Wood,6,3);
            if(appearance==2)foreach(float x in new[]{-.14f,.14f})m.Box(new Vector3(x,1.12f,.14f),new Vector3(.06f,.45f,.02f),TownColor.Yellow);
            if(appearance==3)m.Ellipsoid(new Vector3(0,1.65f,-.18f),new Vector3(.09f,.17f,.1f),TownColor.Wood,6,3);
            foreach(float side in new[]{-1f,1f})
            {
                var foot=new Vector3(side*.11f,.11f,side*stride);
                Beam(m,new Vector3(side*.11f,.88f,0),new Vector3(side*.11f,.5f,-side*stride*.4f),.13f,TownColor.Frame);
                Beam(m,new Vector3(side*.11f,.5f,-side*stride*.4f),foot,.12f,TownColor.Frame);
                m.Box(foot,new Vector3(.16f,.12f,.28f),TownColor.Rubber);
                var elbow=new Vector3(side*.28f,1.04f,-side*stride*.5f);
                Beam(m,new Vector3(side*.25f,1.36f,0),elbow,.11f,shirt);
                Beam(m,elbow,new Vector3(side*.28f,.86f,-side*stride),.09f,TownColor.Sand);
            }
        }
    }
}
