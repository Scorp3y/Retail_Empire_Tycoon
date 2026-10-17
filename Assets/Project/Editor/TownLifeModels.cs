using System;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

/// <summary>Native low-poly articulated models, matching the existing town palette.</summary>
internal static class TownLifeModels
{
    internal static TownMeshBuilder Train(bool cab)
    {
        var m=new TownMeshBuilder();
        m.ExtrudeX(Vector3.zero,2.65f,new[]{new Vector2(.85f,-5.45f),new Vector2(3.0f,-5.45f),new Vector2(3.35f,-4.9f),new Vector2(3.35f,4.6f),new Vector2(2.9f,5.4f),new Vector2(.85f,5.4f)},TownColor.Cream);
        m.Box(new(0,.85f,0),new(2.7f,.32f,10.9f),TownColor.Frame);
        foreach(float side in new[]{-1f,1f})
        {
            m.Box(new(side*1.334f,1.35f,0),new(.035f,.35f,10.6f),TownColor.GreenStripe);
            m.Box(new(side*1.355f,1.1f,0),new(.025f,.09f,10.65f),TownColor.Red);
            for(int i=0;i<8;i++){float z=-4.45f+i*1.18f;m.Box(new(side*1.34f,2.35f,z),new(.035f,.91f,.91f),TownColor.Frame);m.Box(new(side*1.36f,2.37f,z),new(.035f,.74f,.75f),TownColor.Glass);}
            foreach(float z in new[]{-4.8f,4.65f}){m.Box(new(side*1.38f,1.95f,z),new(.04f,1.9f,.65f),TownColor.Mint);m.Box(new(side*1.41f,2.45f,z),new(.02f,.65f,.4f),TownColor.Glass);m.Box(new(side*1.42f,1.62f,z),new(.035f,.06f,.25f),TownColor.Metal);}
        }
        foreach(float z in new[]{-3.4f,3.4f})
        {
            m.Box(new(0,.58f,z),new(2.1f,.35f,2.3f),TownColor.Frame);
            foreach(float axle in new[]{-.7f,.7f})foreach(float side in new[]{-1f,1f})
            {m.Cylinder(new(side*1.1f,.36f,z+axle),.36f,.36f,.19f,TownColor.Rubber,14,Quaternion.Euler(0,0,90));m.Cylinder(new(side*1.21f,.36f,z+axle),.23f,.23f,.025f,TownColor.Metal,12,Quaternion.Euler(0,0,90));}
        }
        foreach(float z in new[]{-2f,1f})m.Box(new(0,.6f,z),new(1.7f,.45f,1.6f),TownColor.Charcoal);
        foreach(float z in new[]{-3f,0f,3f}){m.Box(new(0,3.4f,z),new(1.45f,.2f,1.1f),TownColor.Metal);for(int j=0;j<5;j++)m.Box(new(-.5f+j*.25f,3.51f,z),new(.08f,.03f,.8f),TownColor.Frame);}
        foreach(float z in new[]{-5.6f,5.6f}){m.Box(new(0,.88f,z),new(.38f,.24f,.6f),TownColor.Metal);m.Box(new(0,2,z),new(1.15f,1.8f,.25f),TownColor.Charcoal);}
        if(cab)
        {
            m.ExtrudeX(new(0,0,0),2.6f,new[]{new Vector2(.9f,4.7f),new Vector2(2.9f,4.7f),new Vector2(2.65f,5.7f),new Vector2(1.15f,5.85f)},TownColor.GreenStripe);
            foreach(float x in new[]{-.58f,.58f}){m.Box(new(x,2.37f,5.75f),new(.95f,.68f,.04f),TownColor.Frame);m.Box(new(x,2.4f,5.78f),new(.82f,.53f,.04f),TownColor.Glass);m.Box(new(x,1.45f,5.91f),new(.35f,.22f,.07f),TownColor.Light);m.Box(new(x,.98f,5.91f),new(.32f,.1f,.07f),TownColor.Red);}
            m.Box(new(0,.86f,5.87f),new(2.5f,.18f,.2f),TownColor.Metal);
        }
        return m;
    }
    internal static void Animal(Transform parent,bool chicken,bool rooster,int index,Action<Transform,string,TownMeshBuilder> part)
    {
        var actor=parent.gameObject.AddComponent<FarmAnimal>();actor.chicken=chicken;actor.seed=index;actor.pasture=new Rect(472,255,26,29);
        var body=new TownMeshBuilder();
        if(!chicken)
        {
            body.Ellipsoid(new(0,1.28f,0),new(.52f,.58f,1.05f),TownColor.White,12,7);
            foreach(float side in new[]{-1f,1f})foreach(float z in new[]{-.65f,.35f})body.Ellipsoid(new(side*.46f,1.4f,z),new(.08f,.3f,.35f),index%3==0?TownColor.Wood:TownColor.Charcoal,8,4);
            body.Ellipsoid(new(0,.87f,-.35f),new(.25f,.15f,.3f),TownColor.Pink,10,4);
        }
        else{body.Ellipsoid(new(0,.44f,0),new(.23f,.28f,.34f),rooster?TownColor.Wood:TownColor.White,10,5);foreach(float side in new[]{-1f,1f})body.Ellipsoid(new(side*.2f,.45f,-.04f),new(.07f,.21f,.22f),rooster?TownColor.Orange:TownColor.Cream,8,4);}
        part(parent,"Body",body);
        actor.head=new GameObject("Head joint").transform;actor.head.SetParent(parent,false);actor.head.localPosition=chicken?new(0,.61f,.25f):new(0,1.6f,.85f);
        var head=new TownMeshBuilder();
        head.Ellipsoid(chicken?new(0,.08f,.08f):new(0,-.04f,.25f),chicken?new(.13f,.17f,.14f):new(.26f,.34f,.38f),TownColor.White,10,6);
        if(chicken)
        {
            head.ExtrudeX(new(0,.05f,.19f),.09f,new[]{new Vector2(-.04f,0),new Vector2(.04f,0),new Vector2(0,.17f)},TownColor.Yellow);
            for(int i=0;i<3;i++)head.Ellipsoid(new(0,.25f,.03f+i*.06f),new(.04f,.07f,.05f),TownColor.Red,8,4);
            head.Ellipsoid(new(0,-.06f,.18f),new(.04f,.07f,.05f),TownColor.Red,8,4);
        }
        else
        {
            head.Ellipsoid(new(0,-.23f,.51f),new(.26f,.16f,.2f),TownColor.Pink,10,5);
            foreach(float side in new[]{-1f,1f}){head.Ellipsoid(new(side*.33f,.08f,.05f),new(.18f,.075f,.12f),TownColor.Cream,8,4);head.Cylinder(new(side*.19f,.31f,.03f),.07f,.015f,.24f,TownColor.Sand,8,Quaternion.Euler(0,0,side*-22));head.Ellipsoid(new(side*.13f,-.2f,.68f),new(.035f,.025f,.025f),TownColor.Charcoal,8,4);}
        }
        foreach(float side in new[]{-1f,1f})head.Ellipsoid(chicken?new(side*.12f,.13f,.14f):new(side*.245f,.01f,.38f),chicken?new(.025f,.035f,.035f):new(.035f,.055f,.055f),TownColor.Charcoal,8,4);
        part(actor.head,"Head anatomy",head);actor.tail=new GameObject("Tail joint").transform;actor.tail.SetParent(parent,false);actor.tail.localPosition=chicken?new(0,.5f,-.27f):new(0,1.55f,-.98f);
        var tail=new TownMeshBuilder();if(chicken){for(int i=-1;i<=1;i++)tail.Ellipsoid(new(i*.07f,.08f,-.12f),new(.07f,rooster?.25f:.14f,.15f),rooster?TownColor.GreenStripe:TownColor.Cream,8,4);}else{tail.Cylinder(new(0,-.3f,-.05f),.035f,.04f,.6f,TownColor.White);tail.Ellipsoid(new(0,-.62f,-.05f),new(.07f,.13f,.07f),TownColor.Charcoal);}
        part(actor.tail,"Tail",tail);actor.legs=new Transform[chicken?2:4];
        for(int i=0;i<actor.legs.Length;i++)
        {
            var leg=new GameObject("Leg joint "+i).transform;leg.SetParent(parent,false);leg.localPosition=chicken?new(i==0?-.1f:.1f,.3f,0):new(i%2==0?-.32f:.32f,.93f,i<2?.63f:-.65f);actor.legs[i]=leg;
            var limb=new TownMeshBuilder();if(chicken){limb.Cylinder(new(0,-.13f,0),.025f,.025f,.26f,TownColor.Yellow,7);for(int j=-1;j<=1;j++)limb.Box(new(j*.035f,-.28f,.06f),new(.025f,.025f,.17f),TownColor.Yellow,Quaternion.Euler(0,j*18,0));}else{limb.Ellipsoid(new(0,-.17f,0),new(.13f,.27f,.14f),TownColor.White,8,5);limb.Cylinder(new(0,-.6f,0),.065f,.095f,.6f,TownColor.Cream);limb.Box(new(0,-.86f,.035f),new(.19f,.14f,.24f),TownColor.Charcoal);}
            part(leg,"Limb",limb);
        }
    }
}
