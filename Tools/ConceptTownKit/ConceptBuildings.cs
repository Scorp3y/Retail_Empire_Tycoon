using UnityEngine;
using RetailEmpireTycoon.Editor.TownKit;
using static RetailEmpireTycoon.Editor.ConceptTownKit.ConceptParts;

namespace RetailEmpireTycoon.Editor.ConceptTownKit
{
    internal static class ConceptBuildings
    {
        public static ConceptGeometry Apartment()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,16,14);
            var o=P(0,.24f,-1);const float w=10,d=8,h=9.6f;
            WallBody(m,o,w,h,d,TownColor.Cream);
            for(int level=0;level<3;level++)
            {
                float y=1.9f+level*3.1f;
                foreach(float x in new[]{-3.3f,0,3.3f})
                {
                    if(level!=0||x!=0)Window(m,o+P(x,y,d/2+.03f),1.45f,1.8f);
                    Window(m,o+P(w/2+.03f,y,x*.75f),1.35f,1.8f,true);
                    Window(m,o+P(-w/2-.03f,y,x*.75f),1.35f,1.8f,true,true);
                    // Back windows prevent the asset becoming a blank box when viewed from another street.
                    Window(m,o+P(x,y,-d/2-.03f),1.45f,1.8f,false,true);
                    if(level>0&&x!=0)
                    {
                        var balcony=o+P(x,y-1,4.5f);
                        Box(m,balcony,2.2f,.16f,1.3f,TownColor.Pavement);
                        Rail(m,balcony+P(-1,.08f,.58f),balcony+P(1,.08f,.58f),.9f);
                        Rail(m,balcony+P(-1,.08f,-.55f),balcony+P(-1,.08f,.58f),.9f);
                        Rail(m,balcony+P(1,.08f,-.55f),balcony+P(1,.08f,.58f),.9f);
                        Planter(m,balcony+P(0,.12f,.2f),1.15f);
                    }
                }
                Box(m,o+P(0,(level+1)*3.1f,0),w+.08f,.13f,d+.08f,TownColor.White);
            }
            Door(m,o+P(0,1.3f,d/2+.06f),1.6f,2.4f);
            Box(m,o+P(0,2.7f,4.4f),2.4f,.14f,1.3f,TownColor.Mint);
            HipRoof(m,o+P(0,h+.08f,0),w+.7f,d+.7f,2);
            Chimney(m,o+P(-2,10.8f,-2));RoofVent(m,o+P(1.2f,10.9f,-.7f));
            foreach(float x in new[]{-5.12f,5.12f})
                Box(m,o+P(x,h/2,3.8f),.1f,h,.1f,TownColor.Metal);
            foreach(float x in new[]{-6.5f,6.5f})Tree(m,P(x,.24f,-3),4.5f);
            foreach(float x in new[]{-4f,4f})Planter(m,P(x,.24f,5),2.5f);
            Lamp(m,P(6.7f,.24f,5.8f));return g;
        }

        public static ConceptGeometry RetailRow()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,20,12);
            for(int shop=0;shop<3;shop++)
            {
                float x=-6+shop*6;var o=P(x,.24f,-1.2f);
                var facade=shop==0?TownColor.LeafLight:shop==1?TownColor.Orange:TownColor.Cream;
                float h=4.3f+(shop==1?.3f:0);
                WallBody(m,o,5.9f,h,6.8f,facade);
                Box(m,o+P(0,h+.06f,0),6.15f,.22f,7.05f,TownColor.Mint);
                foreach(float side in new[]{-2.9f,2.9f})Box(m,o+P(side,h+.3f,0),.13f,.4f,7,TownColor.Mint);
                Box(m,o+P(0,h+.3f,-3.4f),6,.4f,.14f,TownColor.Mint);
                RoofVent(m,o+P(-1,h+.18f,-1.5f));
                Window(m,o+P(-1.3f,1.7f,3.46f),2.45f,2.4f);
                Door(m,o+P(1.6f,1.3f,3.46f),1.25f,2.45f);
                var awning=shop==0?TownColor.GreenStripe:shop==1?TownColor.Red:TownColor.Blue;
                Awning(m,o+P(0,3.3f,4.08f),5.45f,awning);
                Sign(m,o+P(0,3.95f,3.55f),4.9f,new[]{"ПРОДУКТЫ","ПЕКАРНЯ","ОДЕЖДА"}[shop]);
                Planter(m,P(x-2.5f,.24f,4.6f),.8f);
            }
            Lamp(m,P(-9,.24f,4.8f));Tree(m,P(9,.24f,3.8f),4.1f);return g;
        }

        public static ConceptGeometry Cafe()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,15,14);
            var o=P(.8f,.24f,-1.8f);WallBody(m,o,8.8f,4.1f,7,TownColor.Cream);
            HipRoof(m,o+P(0,4.15f,0),9.5f,7.7f,1.5f);
            Door(m,o+P(1.6f,1.25f,3.55f),1.65f,2.4f);
            Window(m,o+P(-1.8f,1.8f,3.55f),3.4f,2.1f);
            foreach(float z in new[]{-1.8f,1.2f})Window(m,o+P(4.45f,1.8f,z),2.2f,2.1f,true);
            foreach(float z in new[]{-1.8f,1.2f})Window(m,o+P(-4.45f,1.8f,z),2.2f,2.1f,true,true);
            foreach(float x in new[]{-2.5f,2.5f})Window(m,o+P(x,1.8f,-3.55f),1.5f,1.6f,false,true);
            Awning(m,o+P(0,3.3f,4.2f),8.2f,TownColor.Red);
            Sign(m,o+P(0,4.6f,3.65f),3.3f,"КАФЕ");
            Chimney(m,o+P(-2.4f,5.4f,-1.3f));
            foreach(float x in new[]{-3.3f,1f})CafeTable(m,P(x,.24f,4.3f));
            Umbrella(m,P(-3.3f,.24f,4.3f));
            foreach(float x in new[]{-5.7f,5.8f})Planter(m,P(x,.24f,3.8f),1.3f);
            Tree(m,P(-6,.24f,-3.2f),4);Lamp(m,P(6.5f,.24f,5.6f));
            Box(m,P(4.1f,.85f,5.7f),.78f,1.2f,.09f,TownColor.Wood);
            Box(m,P(4.1f,.9f,5.76f),.64f,.9f,.025f,TownColor.Frame);
            for(int i=0;i<4;i++)Box(m,P(4.1f,.6f+i*.16f,5.79f),.44f,.035f,.014f,TownColor.White);
            return g;
        }

        private static void CafeTable(TownMeshBuilder m,Vector3 bottom)
        {
            m.Cylinder(bottom+P(0,.85f,0),.65f,.65f,.1f,TownColor.WoodLight,12);
            m.Cylinder(bottom+P(0,.42f,0),.06f,.06f,.84f,TownColor.Frame);
            foreach(float side in new[]{-1f,1f})
            {
                var p=bottom+P(side*1.1f,0,0);
                Box(m,p+P(0,.48f,0),.58f,.1f,.58f,TownColor.Wood);
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                    Box(m,p+P(x*.22f,.24f,z*.22f),.045f,.48f,.045f,TownColor.Frame);
                Box(m,p+P(side*.28f,.83f,0),.08f,.62f,.58f,TownColor.Wood);
            }
            m.Cylinder(bottom+P(.15f,.97f,0),.09f,.085f,.15f,TownColor.White,8);
        }

        private static void Umbrella(TownMeshBuilder m,Vector3 bottom)
        {
            Beam(m,bottom,bottom+P(0,2.6f,0),.06f,TownColor.Frame);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
                var p=bottom+P(Mathf.Cos(a)*1.45f,2.3f,Mathf.Sin(a)*1.45f);
                var q=bottom+P(Mathf.Cos(b)*1.45f,2.3f,Mathf.Sin(b)*1.45f);
                m.Triangle(p,q,bottom+P(0,2.75f,0),i%2==0?TownColor.Red:TownColor.White);
                m.Triangle(q,p,bottom+P(0,2.75f,0),i%2==0?TownColor.Red:TownColor.White);
            }
        }

        public static ConceptGeometry Warehouse()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,25,21);
            var o=P(-1.5f,.24f,-3);const float w=18,d=11,h=5.8f;
            WallBody(m,o,w,h,d,TownColor.Cream);
            GableRoof(m,o+P(0,h,0),w+.7f,d+.7f,1.5f,TownColor.Cream);
            for(float x=-8;x<=8;x+=1)
                Box(m,o+P(x,h/2,5.53f),.04f,h-.5f,.04f,TownColor.White);
            foreach(float x in new[]{-4.5f,3f})
            {
                TownArchitecturalParts.LoadingDoor(m,o+P(x,2.1f,5.65f),4.1f,4.1f);
                Box(m,o+P(x,.17f,6.2f),4.9f,.34f,1.7f,TownColor.Stone);
                Sign(m,o+P(x,4.85f,5.66f),3.7f,x<0?"ПРИЕМКА":"СКЛАД");
            }
            foreach(float z in new[]{-3f,0,3f})Window(m,o+P(w/2+.04f,3.8f,z),1.8f,1.15f,true);
            foreach(float z in new[]{-3f,0,3f})Window(m,o+P(-w/2-.04f,3.8f,z),1.8f,1.15f,true,true);
            foreach(float x in new[]{-5f,5f})RoofVent(m,o+P(x,6.85f,-2));
            for(int i=0;i<3;i++)
            {
                var p=P(-10+i*1.5f,.24f,6.9f);TownProps.Pallet(m,p);
                TownProps.CardboardBox(m,p+P(0,.25f,0),P(.85f,.9f,.75f));
                TownProps.CardboardBox(m,p+P(0,1.15f,0),P(.7f,.6f,.65f));
            }
            Add(m,TownVehicles.Forklift(),P(4,.24f,7.3f));
            Box(m,P(10,1.7f,-5),2.7f,2.9f,5.5f,TownColor.Blue);
            for(int i=0;i<12;i++)Box(m,P(11.37f,1.7f,-7.5f+i*.45f),.05f,2.75f,.045f,TownColor.Metal);
            Planter(m,P(11,.24f,7.5f),1.3f);return g;
        }
    }
}
