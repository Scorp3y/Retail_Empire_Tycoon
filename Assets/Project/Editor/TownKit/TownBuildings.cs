using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal static class TownBuildings
    {
        public static TownMeshBuilder House(TownColor facade,int floors=1,bool townhouse=false)
        {
            var m=new TownMeshBuilder();float width=townhouse?5.4f:7, depth=6.5f, height=floors*2.9f;
            m.Box(new Vector3(0,.15f,0),new Vector3(width+.3f,.3f,depth+.3f),TownColor.Stone);
            m.Box(new Vector3(0,height/2+.3f,0),new Vector3(width,height,depth),facade);
            m.Gable(new Vector3(0,height+.32f,0),width+.55f,depth+.6f,1.8f,TownColor.Mint,facade);
            m.Box(new Vector3(-width*.25f,height+1.7f,-1.2f),new Vector3(.55f,1.6f,.7f),TownColor.Brick);
            for(int floor=0;floor<floors;floor++)
            {
                float y=1.8f+floor*2.9f;
                foreach(float x in new[]{-width*.3f,width*.3f}) TownArchitecturalParts.Window(m,new Vector3(x,y,depth/2+.04f),1.25f,1.35f);
                foreach(float z in new[]{-1.7f,1.7f}) TownArchitecturalParts.Window(m,new Vector3(width/2+.04f,y,z),1.25f,1.35f,true);
                if(floor>0)TownArchitecturalParts.Window(m,new Vector3(0,y,depth/2+.04f),1.1f,1.35f);
            }
            TownArchitecturalParts.Door(m,new Vector3(0,1.4f,depth/2+.09f),1.1f,2.2f);
            TownArchitecturalParts.Window(m,new Vector3(0,height+1,(depth+.6f)/2+.04f),.65f,.65f);
            return m;
        }

        public static TownMeshBuilder Apartment()
        {
            var m=new TownMeshBuilder();const float w=10,d=8,h=11.6f;
            m.Box(new Vector3(0,h/2+.2f,0),new Vector3(w,h,d),TownColor.Cream);
            m.Box(new Vector3(0,.18f,0),new Vector3(w+.3f,.36f,d+.3f),TownColor.Stone);
            for(int level=0;level<4;level++)
            {
                float y=1.8f+level*2.8f;
                foreach(float x in new[]{-3.25f,0,3.25f})
                {
                    if(level==0&&x==0)continue;
                    TownArchitecturalParts.Window(m,new Vector3(x,y,d/2+.02f),1.65f,1.55f);
                    if(level>0&&x!=0)
                    {
                        m.Box(new Vector3(x,y-.9f,d/2+.56f),new Vector3(2.1f,.16f,1.2f),TownColor.Pavement);
                        m.Box(new Vector3(x,y-.48f,d/2+1.1f),new Vector3(2.1f,.78f,.1f),TownColor.Mint);
                    }
                }
                foreach(float z in new[]{-2.4f,0,2.4f})TownArchitecturalParts.Window(m,new Vector3(w/2+.02f,y,z),1.4f,1.55f,true);
            }
            TownArchitecturalParts.Door(m,new Vector3(0,1.4f,d/2+.08f),1.8f,2.4f,true);
            TownArchitecturalParts.FlatRoof(m,w,d,h+.2f);return m;
        }

        public static TownMeshBuilder Garage()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,1.6f,0),new Vector3(5.4f,3.2f,6.5f),TownColor.Cream);
            m.Gable(new Vector3(0,3.2f,0),5.9f,7,1.2f,TownColor.Mint,TownColor.Cream);
            TownArchitecturalParts.LoadingDoor(m,new Vector3(0,1.4f,3.32f),3.4f,2.7f);return m;
        }

        public static TownMeshBuilder Wholesale()
        {
            var m=new TownMeshBuilder();const float w=20,d=13,h=5.4f;
            m.Box(new Vector3(0,h/2,0),new Vector3(w,h,d),TownColor.Cream);
            m.Box(new Vector3(0,.38f,0),new Vector3(w+.1f,.76f,d+.1f),TownColor.Pavement);
            TownArchitecturalParts.FlatRoof(m,w,d,h);
            foreach(float x in new[]{-5.8f,0,5.8f}) TownArchitecturalParts.LoadingDoor(m,new Vector3(x,1.8f,d/2+.05f),3.5f,3.5f);
            TownArchitecturalParts.Sign(m,new Vector3(0,4.6f,d/2+.13f),11,.85f,"ПРОДУКТЫ");
            foreach(float z in new[]{-4f,0,4f}) TownArchitecturalParts.Window(m,new Vector3(w/2+.03f,3.5f,z),2.4f,.9f,true);
            return m;
        }

        public static TownMeshBuilder Retail(string sign,float width,float depth,bool equipment=false,bool dealership=false,bool construction=false,TownColor facade=TownColor.Cream)
        {
            var m=new TownMeshBuilder();const float h=4.2f;
            m.Box(new Vector3(0,h/2,0),new Vector3(width,h,depth),facade);
            m.Box(new Vector3(0,.3f,0),new Vector3(width+.1f,.6f,depth+.1f),TownColor.Pavement);
            TownArchitecturalParts.FlatRoof(m,width,depth,h);
            TownArchitecturalParts.Sign(m,new Vector3(0,3.65f,depth/2+.18f),width*.88f,.72f,sign);
            TownArchitecturalParts.Door(m,new Vector3(0,1.3f,depth/2+.08f),1.6f,2.5f,true);
            foreach(float x in new[]{-width*.3f,width*.3f})TownArchitecturalParts.Window(m,new Vector3(x,1.8f,depth/2+.07f),width*.27f,2.2f);
            foreach(float z in new[]{-depth*.26f,depth*.26f})TownArchitecturalParts.Window(m,new Vector3(width/2+.05f,1.8f,z),depth*.32f,2.2f,true);
            if(!dealership)TownArchitecturalParts.LoadingDoor(m,new Vector3(-width*.3f,1.5f,-depth/2-.09f),2.8f,2.9f,true);
            if(equipment)
            {
                // Exterior sample stand, separately readable from the building facade.
                TownProps.Shelf(m,new Vector3(width*.5f+2.2f,0,1),2.2f,2.1f,.75f,true);
                TownProps.Refrigerator(m,new Vector3(width*.5f+2.2f,0,-1.1f));
            }
            if(construction)
            {
                m.Box(new Vector3(width*.5f+2.2f,1.2f,0),new Vector3(2.3f,2.4f,.22f),TownColor.Cream);
                m.Box(new Vector3(width*.5f+2.2f,.4f,.15f),new Vector3(2.3f,.8f,.08f),TownColor.GreenStripe);
                TownProps.Pallet(m,new Vector3(width*.5f+2.2f,0,2.2f));
            }
            return m;
        }

        public static TownMeshBuilder Cafe(string sign,TownColor facade,bool flowerShop=false,bool outdoorTables=true)
        {
            var m=Retail(sign,8,6,facade:facade); // Shared dimensions keep doors and windows consistent.
            m.Box(new Vector3(0,3.05f,3.65f),new Vector3(8.2f,.15f,1.3f),TownColor.Mint);
            for(int i=0;i<12;i++)m.Box(new Vector3(-3.85f+i*.7f,3.13f,3.65f),new Vector3(.32f,.04f,1.3f),TownColor.White);
            m.Box(new Vector3(0,3.85f,0),new Vector3(8.02f,.32f,6.02f),facade);
            if(flowerShop)
                foreach(float x in new[]{-2.8f,2.8f})TownProps.Planter(m,new Vector3(x,0,4.5f),true);
            else if(outdoorTables)
                foreach(float x in new[]{-2.7f,2.7f})
                {
                    m.Cylinder(new Vector3(x,.8f,4.8f),.58f,.58f,.1f,TownColor.WoodLight);
                    m.Cylinder(new Vector3(x,.4f,4.8f),.07f,.07f,.8f,TownColor.Frame);
                    foreach(float z in new[]{4.1f,5.5f})
                    {
                        m.Box(new Vector3(x,.47f,z),new Vector3(.55f,.1f,.5f),TownColor.Wood);
                        for(int s=-1;s<=1;s+=2)m.Box(new Vector3(x+s*.2f,.23f,z),new Vector3(.07f,.46f,.4f),TownColor.Frame);
                    }
                }
            if(sign=="АПТЕКА")
            {
                m.Box(new Vector3(4.18f,3.2f,2.5f),new Vector3(.15f,1.25f,1.25f),TownColor.White);
                m.Box(new Vector3(4.27f,3.2f,2.5f),new Vector3(.06f,.85f,.26f),TownColor.GreenStripe);
                m.Box(new Vector3(4.27f,3.2f,2.5f),new Vector3(.06f,.26f,.85f),TownColor.GreenStripe);
            }
            return m;
        }

        public static TownMeshBuilder FarmersMarket()
        {
            var m=new TownMeshBuilder();const float w=12,d=6,h=3.2f;
            foreach(float x in new[]{-w/2,w/2})foreach(float z in new[]{-d/2,d/2})m.Box(new Vector3(x,h/2,z),new Vector3(.18f,h,.18f),TownColor.Wood);
            for(int strip=0;strip<12;strip++)
            {
                float x=-w/2+strip;var color=strip%2==0?TownColor.White:TownColor.Mint;
                m.Quad(new Vector3(x,h,3.3f),new Vector3(x+1,h,3.3f),new Vector3(x+1,h+.8f,0),new Vector3(x,h+.8f,0),color);
                m.Quad(new Vector3(x,h+.8f,0),new Vector3(x+1,h+.8f,0),new Vector3(x+1,h,-3.3f),new Vector3(x,h,-3.3f),color);
            }
            TownArchitecturalParts.Sign(m,new Vector3(0,2.85f,3.2f),5.8f,.7f,"РЫНОК");
            foreach(float x in new[]{-4f,0,4f})
            {
                m.Box(new Vector3(x,.7f,.8f),new Vector3(3.1f,1.4f,1.6f),TownColor.Wood);
                TownProps.Crate(m,new Vector3(x-.8f,1.4f,.8f),false);
                TownProps.Crate(m,new Vector3(x+.8f,1.4f,.8f),true);
                TownProps.Crate(m,new Vector3(x,0,-1.7f),false);
            }
            return m;
        }

        public static TownMeshBuilder PlayerShop()
        {
            var m=new TownMeshBuilder();const float w=15,d=12,h=3.3f;
            m.Ground(0,0,w,d,.02f,TownColor.Cream);m.Ground(-5.75f,0,3.5f,d,.025f,TownColor.Pavement);
            void Wall(Vector3 center,Vector3 size)
            {
                m.Box(center+Vector3.up*h*.5f,new Vector3(size.x,h,size.z),TownColor.Metal);
                m.Box(center+Vector3.up*.7f,new Vector3(size.x+.01f,1.4f,size.z+.01f),TownColor.GreenStripe);
            }
            Wall(new Vector3(0,0,-d/2),new Vector3(w,.1f,.2f));
            Wall(new Vector3(w/2,0,0),new Vector3(.2f,.1f,d));
            Wall(new Vector3(-w/2,0,-2),new Vector3(.2f,.1f,8));
            Wall(new Vector3(-w/2,0,5.5f),new Vector3(.2f,.1f,1));
            Wall(new Vector3(-4,0,-2.5f),new Vector3(.18f,.1f,7));
            Wall(new Vector3(-4,0,4.5f),new Vector3(.18f,.1f,3));
            Wall(new Vector3(-3.4f,0,d/2),new Vector3(8.2f,.1f,.2f));
            Wall(new Vector3(5.2f,0,d/2),new Vector3(4.6f,.1f,.2f));
            TownArchitecturalParts.Door(m,new Vector3(1.8f,1.35f,d/2+.07f),2.2f,2.65f,true);
            TownArchitecturalParts.Sign(m,new Vector3(1.8f,2.95f,d/2+.15f),5,.5f,"МАГАЗИН");
            foreach(float z in new[]{-3.5f,0,3.5f})TownProps.WarehouseRack(m,new Vector3(-5.8f,0,z));
            foreach(float x in new[]{-1f,2.5f,6})TownProps.Shelf(m,new Vector3(x,0,-1),2.4f,1.8f,.8f,true);
            TownProps.Checkout(m,new Vector3(5,0,4));return m;
        }
    }
}
