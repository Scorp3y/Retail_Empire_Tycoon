using UnityEngine;
using F = DecorMesh.Finish;

internal sealed partial class DecorArt
{
    private void BuildLighting(int number)
    {
        switch (number)
        {
            case 61:
                Ring(F.Pine,0,.46f,0,(0,.071f),(.027f,.07f));
                Beam(F.Charcoal,V(0,.36f),V(0,.468f),.011f);
                Ring(F.Glow,0,0,0,(0,.071f),(.04f,.128f),(.105f,.174f),(.194f,.192f),(.276f,.17f),(.337f,.125f),(.368f,.059f));
                Ring(F.Brass,0,0,0,(-.009f,.073f),(.017f,.073f));
                for (int i=0;i<3;i++)
                {
                    float y=.104f+i*.069f;
                    float radius=i==1?.195f:.177f;
                    Ring(F.Cream,0,y,0,(0,radius),(.007f,radius-.001f));
                }
                break;
            case 62:
                Box(F.Wood,0,.06f,0,1.14f,.114f,.21f);
                Box(F.Brass,0,.018f,0,1.064f,.043f,.151f);
                Box(F.Glow,0,.007f,0,1.012f,.028f,.111f);
                for (int side=-1;side<=1;side+=2)
                {
                    Beam(F.Charcoal,V(side*.395f,.11f),V(side*.395f,.41f),.009f);
                    Ring(F.Brass,side*.395f,.408f,0,(0,.05f),(.025f,.052f));
                }
                break;
            case 63:
                Disc(F.Brass,0,.213f,0,.126f,.035f);
                Disc(F.Sage,-.034f,.213f,.049f,.178f,.061f);
                Orb(F.Glow,.024f,.213f,.113f,.335f,.335f,.108f);
                break;
            case 64:
                Ring(F.Terra,0,0,0,(0,.18f),(.038f,.186f),(.066f,.147f));
                Beam(F.Terra,V(0,.055f),V(0,.96f),.036f);
                Ring(F.Brass,0,.93f,0,(0,.058f),(.057f,.066f));
                Ring(F.Cream,0,.978f,0,(0,.072f),(.06f,.151f),(.13f,.229f),(.151f,.217f),(.13f,.19f));
                Ring(F.Glow,0,1.074f,0,(0,.157f),(.024f,.191f));
                break;
            case 65:
                Lantern(0,0,0,.11f,.235f,false);
                mesh.Tube(F.Brass,.012f,V(-.072f,.229f),V(-.087f,.303f),V(-.046f,.358f),
                    V(.046f,.358f),V(.087f,.303f),V(.072f,.229f));
                break;
            case 66:
                Ring(F.Wood,0,0,0,(0,.133f),(.047f,.133f),(.058f,.115f));
                Vector3 joint=V(-.080f,.215f,0);
                Vector3 tip=V(.056f,.302f,.018f);
                Beam(F.Pine,V(0,.042f),joint,.025f); Beam(F.Pine,joint,tip,.026f);
                foreach(var pivot in new[]{V(0,.056f),joint,tip}) Disc(F.Brass,pivot.x,pivot.y,pivot.z+.022f,.029f,.013f);
                mesh.Profile(F.Pine,tip+V(.066f,-.014f,.04f),new[]{(-.081f,.082f),(-.061f,.089f),(.057f,.047f),(.065f,.039f)},10,1,V(-25,0,-39));
                mesh.Profile(F.Glow,tip+V(.066f,-.014f,.04f),new[]{(-.083f,.072f),(-.077f,.075f)},10,1,V(-25,0,-39));
                break;
            case 67:
                Box(F.Brass,0,.038f,0,.43f,.053f,.102f);
                Box(F.Glow,0,.011f,0,.39f,.022f,.071f);
                for(int side=-1;side<=1;side+=2) Box(F.Brass,side*.166f,.071f,0,.055f,.034f,.129f);
                break;
            case 68:
                Box(F.Pine,0,.17f,.04f,.12f,.27f,.10f);
                Box(F.Pine,0,.17f,.136f,.092f,.075f,.16f);
                Box(F.Pine,0,.17f,.251f,.35f,.296f,.098f);
                Box(F.Glow,0,.17f,.303f,.286f,.239f,.024f);
                break;
            case 69:
                Star(F.Glow,V(0,.201f,.019f),.194f,.093f,5,.031f);
                Beam(F.Brass,V(0,.388f,.019f),V(0,.53f,.019f),.012f);
                Ring(F.Pine,0,.52f,.019f,(0,.049f),(.020f,.05f));
                break;
            case 70:
                Box(F.Pine,0,.27f,.022f,.15f,.30f,.044f);
                mesh.Tube(F.Pine,.027f,V(0,.30f,.042f),V(0,.39f,.105f),V(0,.46f,.21f),
                    V(0,.44f,.285f),V(0,.34f,.31f));
                Ring(F.Sage,0,.228f,.31f,(0,.20f),(.025f,.20f),(.104f,.118f),(.132f,.051f));
                Orb(F.Glow,0,.164f,.31f,.182f,.169f,.178f);
                break;
            default: throw new System.ArgumentOutOfRangeException(nameof(number));
        }
    }

    private void BuildFurnishings(int number)
    {
        switch (number)
        {
            case 71:
                Legs(.35f,.34f,.15f,.046f,F.DarkWood);
                Ring(F.Terra,0,.106f,0,(0,.269f),(.052f,.317f),(.249f,.317f),(.302f,.281f),(.32f,.189f));
                for(int x=-1;x<=1;x+=2)
                for(int z=-1;z<=1;z+=2)
                    mesh.Ball(F.DarkWood,V(x*.082f,.432f,z*.080f),V(.040f,.015f,.040f),latitudeRings:3);
                break;
            case 72:
                Legs(1.24f,.46f,.145f,.044f,F.Wood);
                Box(F.Pine,0,.226f,0,1.37f,.17f,.64f);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(F.Sage,side*.296f,.381f,.064f,.57f,.156f,.45f);
                    mesh.Box(F.Sage,V(side*.291f,.564f,-.208f),V(.581f,.392f,.164f),V(-12,0));
                    Box(F.Pine,side*.642f,.414f,.023f,.155f,.32f,.557f);
                    mesh.Ball(F.Cream,V(side*.418f,.538f,-.041f),V(.246f,.25f,.103f),V(-17,0,side*25),4);
                }
                break;
            case 73:
                Legs(.31f,.30f,.55f,.033f,F.Wood);
                Ring(F.DarkWood,0,.239f,0,(0,.161f),(.025f,.162f),(.025f,.139f),(0,.139f),(0,.161f));
                Box(F.Cream,0,.571f,0,.417f,.107f,.334f);
                mesh.Box(F.Cream,V(0,.626f,-.125f),V(.389f,.102f,.105f),V(-24,0));
                break;
            case 74:
                for(int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3;
                    Beam(F.Wood,V(Mathf.Cos(a)*.34f,0,Mathf.Sin(a)*.17f),V(Mathf.Cos(a)*.28f,.31f,Mathf.Sin(a)*.14f),.055f);
                }
                mesh.CurvedTable(F.Wood,V(0,.338f),.479f,.273f,.07f);
                break;
            case 75:
                Legs(.47f,.43f,.43f,.045f,F.Wood);
                Ring(F.Cream,0,.43f,0,(0,.319f),(.017f,.35f),(.069f,.35f),(.088f,.33f));
                Box(F.Wood,0,.539f,0,.32f,.042f,.25f);
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*.137f;
                    mesh.Tube(side<0?F.Terra:F.Pine,.013f,V(x-.064f,.564f,-.043f),V(x-.052f,.709f,-.02f),V(x+.04f,.735f,.022f),V(x+.065f,.566f,.071f));
                    Orb(F.Brass,x-.054f,.62f,-.025f,.062f,.059f,.057f);
                    Orb(F.Sage,x+.044f,.68f,.041f,.053f,.055f,.051f);
                    Stool(side*.44f,.234f);
                }
                break;
            case 76:
                Box(F.Pine,0,.503f,0,.415f,.98f,.34f);
                Box(F.Cream,0,.547f,.178f,.326f,.363f,.034f);
                Box(F.Charcoal,0,.562f,.198f,.256f,.276f,.018f);
                Box(F.Cream,0,.43f,.223f,.279f,.049f,.131f);
                for(int i=-2;i<=2;i++)Box(F.Charcoal,i*.042f,.459f,.232f,.014f,.009f,.066f);
                mesh.Tube(F.Brass,.022f,V(0,.70f,.211f),V(0,.677f,.255f),V(0,.635f,.255f));
                Orb(F.Cream,0,.843f,.180f,.086f,.101f,.029f);
                Orb(F.Sage,0,.847f,.196f,.050f,.066f,.016f);
                break;
            case 77:
                Ring(F.Pine,0,0,0,(0,.174f),(.029f,.183f),(.054f,.161f));
                Beam(F.Pine,V(0,.042f),V(0,1.23f),.031f);
                for (int i=0;i<4;i++)
                {
                    float a=i*Mathf.PI*.5f,y=i%2==0?.90f:1.11f;
                    Vector3 On(float r,float h)=>V(Mathf.Cos(a)*r,h,Mathf.Sin(a)*r);
                    mesh.Tube(F.Wood,.026f,On(0,y),On(.105f,y+.013f),On(.168f,y+.082f),On(.18f,y+.115f));
                    mesh.Ball(F.Wood,On(.18f,y+.115f),V(.052f,.053f,.053f),latitudeRings:3);
                }
                break;
            case 78:
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*.48f;
                    Ring(F.Brass,x,0,0,(0,.109f),(.026f,.112f),(.048f,.089f),(.07f,.037f),(.57f,.034f),(.609f,.054f));
                    Orb(F.Brass,x,.657f,0,.107f,.107f,.106f);
                }
                SaggingRope(.90f,.57f,.14f,F.Terra,.026f);
                break;
            case 79:
                for(int side=-1;side<=1;side+=2)
                {
                    Beam(F.Wood,V(side*.277f,0,.15f),V(side*.277f,.77f,-.087f),.039f);
                    Beam(F.DarkWood,V(side*.277f,0,-.188f),V(side*.277f,.76f,-.09f),.035f);
                }
                mesh.Box(F.Wood,V(0,.415f,.029f),V(.565f,.716f,.042f),V(-17,0));
                for (int row=0;row<2;row++)
                {
                    float y=.212f+row*.301f,z=.102f-row*.091f;
                    Box(F.Wood,0,y,z+.055f,.574f,.064f,.118f);
                    for(int side=-1;side<=1;side+=2)
                    {
                        float x=side*.132f;
                        mesh.Box(side<0?F.Cream:F.Sage,V(x,y+.12f,z),V(.187f,.237f,.019f),V(-17,0,side*4));
                        Disc(row==0?F.Terra:F.Blue,x,y+.155f,z+.024f,.051f,.005f);
                    }
                }
                break;
            case 80:
                for (int i=-1;i<=1;i++)
                {
                    float turn=i==0?-24:24,x=i*.427f;
                    var center=V(x,.626f,i==0?.085f:0);
                    var rotation=Quaternion.Euler(0,turn,0);
                    mesh.Box(F.Wood,center,V(.482f,1.25f,.069f),V(0,turn));
                    mesh.Box(F.Sage,center+rotation*Vector3.forward*.043f,V(.381f,1.11f,.023f),V(0,turn));
                    mesh.Box(F.Sage,center-rotation*Vector3.forward*.043f,V(.381f,1.11f,.023f),V(0,turn));
                }
                for(int side=-1;side<=1;side+=2)
                for(int i=0;i<2;i++) Box(F.Brass,side*.211f,.31f+i*.61f,.087f,.048f,.104f,.028f);
                break;
            default: throw new System.ArgumentOutOfRangeException(nameof(number));
        }
    }

    private void Legs(float width,float depth,float height,float radius,F finish)
    {
        for(int x=-1;x<=1;x+=2)
        for(int z=-1;z<=1;z+=2)
            Beam(finish,V(x*width*.5f,0,z*depth*.5f),V(x*width*.43f,height,z*depth*.43f),radius);
    }

    private void Stool(float x,float z)
    {
        for(int i=0;i<3;i++)
        {
            float a=i*Mathf.PI*2/3;
            Beam(F.Pine,V(x+Mathf.Cos(a)*.107f,0,z+Mathf.Sin(a)*.105f),V(x+Mathf.Cos(a)*.076f,.19f,z+Mathf.Sin(a)*.076f),.032f);
        }
        Ring(F.Sage,x,.19f,z,(0,.127f),(.025f,.148f),(.069f,.145f),(.076f,.127f));
    }

    private void Lantern(float x,float y,float z,float radius,float height,bool square)
    {
        if(square)
        {
            Box(F.Pine,x,y+.026f,z,radius*2.25f,.052f,radius*2.25f);
            Box(F.Pine,x,y+height,z,radius*2.28f,.056f,radius*2.28f);
            for(int side=-1;side<=1;side+=2)
            for(int back=-1;back<=1;back+=2)
                Beam(F.Pine,V(x+side*radius,y+.04f,z+back*radius),V(x+side*radius,y+height-.02f,z+back*radius),.017f);
            Box(F.Glass,x,y+height*.5f,z,radius*1.94f,height*.73f,radius*1.94f);
            Ring(F.Cream,x,y+.052f,z,(0,radius*.38f),(height*.35f,radius*.38f));
            Orb(F.Glow,x,y+height*.51f,z,radius*.38f,height*.21f,radius*.35f);
        }
        else
        {
            Ring(F.Brass,x,y,z,(0,radius*.89f),(.025f,radius),(.045f,radius*.87f));
            Ring(F.Brass,x,y+height,z,(-.042f,radius*.78f),(-.01f,radius),(.023f,radius*.86f));
            Ring(F.Glow,x,y+.036f,z,(0,radius*.54f),(.025f,radius*.62f),(height-.076f,radius*.62f),(height-.053f,radius*.49f));
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;
                Vector3 On(float r,float h)=>V(x+Mathf.Cos(a)*radius*r,y+h,z+Mathf.Sin(a)*radius*r);
                mesh.Tube(F.Brass,.008f,On(.82f,.032f),On(1.035f,.067f),On(1.035f,height-.058f),On(.82f,height-.02f));
            }
        }
    }
}

