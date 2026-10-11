using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal static partial class TownDetailModels
    {
        private static void AddNeighbourhoodModels(List<TownAssetDefinition> models)
        {
            void Add(string category,string id,string label,Action<TownMeshBuilder> draw)
                =>models.Add(new TownAssetDefinition(category,id,label,()=>{var mesh=new TownMeshBuilder();draw(mesh);return mesh;}));
            Add("Buildings","ResidentialCorner","Угловой жилой дом",ResidentialCorner);
            Add("Buildings","NeighbourhoodShops","Небольшие магазины",NeighbourhoodShops);
            Add("Buildings","GaragePair","Два городских гаража",GaragePair);
            Add("Buildings","BrickAnnex","Кирпичная пристройка",BrickAnnex);
            Add("Street","TreePit","Приствольная решётка",TreePit);
            Add("Street","FlowerBorder","Низкая цветочная клумба",FlowerBorder);
            Add("Street","LowRailing","Низкое ограждение",LowRailing);
            Add("Courts","PicnicTable","Стол со скамьями",PicnicTable);
            Add("Facades","RoofGarden","Крышное озеленение",RoofGarden);
            Add("Facades","ChimneyStack","Кирпичная дымовая труба",Chimney);
            Add("Landmarks","StreetNoticeboard","Стенд с планом района",Noticeboard);
            foreach(var person in new[]{("TownResident",1,"Житель"),("TownWorker",2,"Рабочий"),("TownVisitor",3,"Посетитель")})
                for(int pose=0;pose<3;pose++)
                {
                    int frame=pose,appearance=person.Item2;
                    Add("People",person.Item1+pose,person.Item3+": поза "+pose,m=>Person(m,frame,appearance));
                }
        }

        private static void ResidentialCorner(TownMeshBuilder m)
        {
            Building(m,28,12,3,TownColor.Cream,"",true);
            // A raised corner pavilion and alternating bays break the usual long-box silhouette.
            m.Box(new Vector3(-9,10.1f,0),new Vector3(9,1.3f,12),TownColor.Brick);
            m.Gable(new Vector3(-9,10.8f,0),9.6f,12.6f,1.6f,TownColor.RoofDark,TownColor.Cream);
            foreach(float x in new[]{-10f,-6f,6f,10f})
            {
                m.Box(new Vector3(x,4.8f,6.17f),new Vector3(.22f,9.1f,.18f),TownColor.Brick);
                for(int floor=1;floor<3;floor++)
                {
                    float y=1.7f+floor*3.1f;
                    m.Box(new Vector3(x,y-1,6.8f),new Vector3(2.8f,.15f,1.6f),TownColor.Pavement);
                    m.Box(new Vector3(x,y-.65f,7.5f),new Vector3(2.8f,.65f,.08f),TownColor.Frame);
                    foreach(float edge in new[]{-1.35f,1.35f})m.Box(new Vector3(x+edge,y-.65f,6.8f),new Vector3(.08f,.65f,1.5f),TownColor.Frame);
                }
            }
        }
        private static void NeighbourhoodShops(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,2.1f,0),new Vector3(36,4.2f,10),TownColor.Cream);
            m.Box(new Vector3(0,.25f,0),new Vector3(36.2f,.5f,10.2f),TownColor.Stone);
            TownArchitecturalParts.FlatRoof(m,36,10,4.2f);
            int shop=0;
            foreach(float x in new[]{-13.5f,-4.5f,4.5f,13.5f})
            {
                var trim=shop++%2==0?TownColor.Mint:TownColor.Brick;
                TownArchitecturalParts.Door(m,new Vector3(x-2.3f,1.25f,5.15f),1.6f,2.4f,false);
                TownArchitecturalParts.Window(m,new Vector3(x+1.4f,1.8f,5.08f),4,2.1f);
                m.Box(new Vector3(x,3.25f,5.9f),new Vector3(8.4f,.2f,1.8f),trim);
                m.Box(new Vector3(x-4.4f,2.1f,5.13f),new Vector3(.22f,4.1f,.18f),TownColor.Frame);
                // Modest shop plaques, no enormous generic building-type labels.
                TownArchitecturalParts.Sign(m,new Vector3(x,3.7f,5.17f),3.6f,.55f,shop==1?"ЦВЕТЫ":shop==2?"КАФЕ":shop==3?"КНИГИ":"ЧАЙ");
                TownArchitecturalParts.Window(m,new Vector3(x,2.2f,-5.08f),2.8f,1.5f,false,true);
            }
        }
        private static void GaragePair(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,1.6f,0),new Vector3(12,3.2f,7),TownColor.Brick);
            m.Box(new Vector3(0,.15f,0),new Vector3(12.2f,.3f,7.2f),TownColor.Stone);
            TownArchitecturalParts.FlatRoof(m,12,7,3.2f);
            foreach(float x in new[]{-3f,3f})TownArchitecturalParts.LoadingDoor(m,new Vector3(x,1.45f,3.58f),4.8f,2.7f,false,false);
        }
        private static void BrickAnnex(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,2.2f,0),new Vector3(9,4.4f,6),TownColor.Brick);
            m.Gable(new Vector3(0,4.4f,0),9.5f,6.5f,1.2f,TownColor.RoofDark,TownColor.Brick);
            TownArchitecturalParts.Door(m,new Vector3(-2.4f,1.25f,3.13f),1.6f,2.4f);
            TownArchitecturalParts.Window(m,new Vector3(1.8f,2.2f,3.08f),2.7f,1.6f);
            foreach(float x in new[]{-2.5f,2.5f})TownArchitecturalParts.Window(m,new Vector3(x,2.2f,-3.08f),2,1.6f,false,true);
        }
        private static void TreePit(TownMeshBuilder m)
        {
            m.Ground(0,0,2.6f,2.6f,0,TownColor.Soil);
            foreach(float side in new[]{-1.35f,1.35f})
            {
                m.Box(new Vector3(side,.035f,0),new Vector3(.12f,.07f,2.8f),TownColor.Pavement);
                m.Box(new Vector3(0,.035f,side),new Vector3(2.6f,.07f,.12f),TownColor.Pavement);
            }
            for(int i=-5;i<=5;i++)
            {
                float x=i*.23f;
                if(Mathf.Abs(x)>.4f)m.Ground(x,0,.035f,2.5f,.03f,TownColor.Frame);
                else foreach(float z in new[]{-.9f,.9f})m.Ground(x,z,.035f,.7f,.03f,TownColor.Frame);
            }
        }
        private static void FlowerBorder(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.09f,0),new Vector3(4,.18f,1.7f),TownColor.Stone);
            m.Ground(0,0,3.7f,1.4f,.185f,TownColor.Soil);
            for(int i=0;i<7;i++)
            {
                float x=-1.45f+i*.48f,z=(i%2==0?1:-1)*.23f;
                m.Ellipsoid(new Vector3(x,.34f,z),new Vector3(.34f,.17f,.35f),TownColor.Leaf,6,3);
                foreach(float offset in new[]{-.13f,.13f})m.Ellipsoid(new Vector3(x+offset,.49f,z),new Vector3(.09f,.06f,.09f),i%2==0?TownColor.Flowers:TownColor.Pink,5,3);
            }
        }
        private static void LowRailing(TownMeshBuilder m)
        {
            foreach(float x in new[]{-2f,0,2f})m.Box(new Vector3(x,.45f,0),new Vector3(.08f,.9f,.08f),TownColor.Frame);
            foreach(float y in new[]{.3f,.75f})m.Box(new Vector3(0,y,0),new Vector3(4.1f,.045f,.045f),TownColor.Frame);
            for(int i=-7;i<=7;i++)m.Box(new Vector3(i*.27f,.51f,0),new Vector3(.025f,.46f,.025f),TownColor.Frame);
        }
        private static void PicnicTable(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.78f,0),new Vector3(2.4f,.12f,1.1f),TownColor.WoodLight);
            foreach(float x in new[]{-.8f,.8f})foreach(float side in new[]{-1f,1f})
                Beam(m,new Vector3(x,0,side*.65f),new Vector3(x,.72f,side*.3f),.12f,TownColor.Wood);
            foreach(float z in new[]{-.9f,.9f})m.Box(new Vector3(0,.45f,z),new Vector3(2.4f,.12f,.4f),TownColor.WoodLight);
        }
        private static void RoofGarden(TownMeshBuilder m)
        {
            foreach(float x in new[]{-1.5f,1.5f})
            {
                m.Box(new Vector3(x,.2f,0),new Vector3(2.5f,.4f,1.6f),TownColor.Pavement);
                m.Ground(x,0,2.3f,1.4f,.405f,TownColor.Soil);
                for(int i=0;i<4;i++)m.Ellipsoid(new Vector3(x-.85f+i*.55f,.62f,0),new Vector3(.5f,.26f,.55f),TownColor.Leaf,6,3);
            }
        }
        private static void Chimney(TownMeshBuilder m)
        {
            m.Box(new Vector3(0,.9f,0),new Vector3(.7f,1.8f,.9f),TownColor.Brick);
            m.Box(new Vector3(0,1.82f,0),new Vector3(.9f,.16f,1.1f),TownColor.Stone);
            foreach(float x in new[]{-.19f,.19f})m.Cylinder(new Vector3(x,2.02f,0),.12f,.12f,.3f,TownColor.Frame,6);
        }
        private static void Noticeboard(TownMeshBuilder m)
        {
            foreach(float x in new[]{-.8f,.8f})m.Box(new Vector3(x,1.15f,0),new Vector3(.1f,2.3f,.12f),TownColor.Wood);
            m.Box(new Vector3(0,1.6f,0),new Vector3(1.9f,1.3f,.14f),TownColor.Frame);
            foreach(float face in new[]{-1f,1f})
            {
                m.Box(new Vector3(0,1.6f,face*.09f),new Vector3(1.7f,1.1f,.035f),TownColor.Cream);
                for(int i=0;i<3;i++)
                {
                    m.Box(new Vector3(-.5f+i*.5f,1.6f,face*.12f),new Vector3(.05f,.85f,.02f),TownColor.Mint);
                    m.Box(new Vector3(0,1.3f+i*.3f,face*.12f),new Vector3(1.4f,.05f,.02f),TownColor.Mint);
                }
                m.Box(new Vector3(.22f,1.5f,face*.14f),new Vector3(.12f,.12f,.02f),TownColor.Red);
            }
        }
    }
}
