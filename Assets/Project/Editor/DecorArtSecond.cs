using System;
using UnityEngine;
using F = DecorMesh.Finish;

internal sealed partial class DecorArt
{
    private void BuildSecond(int number)
    {
        if (number <= 60) BuildGreenery(number);
        else if (number <= 70) BuildLighting(number);
        else if (number <= 80) BuildFurnishings(number);
        else if (number <= 90) BuildWallDecor(number);
        else BuildExterior(number);
    }

    private void BuildGreenery(int number)
    {
        switch (number)
        {
            case 51:
                Box(F.Charcoal,0,.052f,0,.44f,.10f,.29f);
                Box(F.Sage,0,.101f,0,.39f,.019f,.24f);
                mesh.Tube(F.DarkWood,.029f,V(-.07f,.10f),V(-.035f,.23f),V(.08f,.34f),V(.04f,.45f));
                BonsaiCrown(V(-.12f,.30f,.035f),V(-.016f,.24f),.23f);
                BonsaiCrown(V(.13f,.38f,-.035f),V(.025f,.30f),.22f);
                BonsaiCrown(V(.034f,.49f),V(.05f,.39f),.28f);
                Orb(F.Cream,.14f,.119f,.07f,.09f,.041f,.071f); break;
            case 52:
                Pot(0,0,0,.255f,.29f,F.Cream);
                mesh.Tube(F.DarkWood,.039f,V(0,.23f),V(-.07f,.65f),V(.015f,.96f),V(.09f,1.32f));
                for (int i=0;i<7;i++)
                {
                    float a=i*2.4f,y=.75f+.081f*i;
                    var tip=V(Mathf.Cos(a)*.30f,y+.22f,Mathf.Sin(a)*.24f);
                    Beam(F.Wood,V(0,y),tip,.018f);
                    OliveSprig(tip,a,.132f);
                }
                break;
            case 53:
                for (int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3;
                    Beam(F.Pine,V(Mathf.Cos(a)*.24f,0,Mathf.Sin(a)*.24f),V(Mathf.Cos(a)*.15f,.57f,Mathf.Sin(a)*.15f),.026f);
                }
                Ring(F.Brass,0,.53f,0,(0,.18f),(.035f,.18f)); Pot(0,.55f,0,.21f,.12f,F.Pine);
                for (int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI*2/7;
                    Vector3 On(float radius,float y)=>V(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius);
                    mesh.Tube(F.Leaf,.012f,On(0,.65f),On(.17f,.88f),On(.38f,.81f));
                    for (int j=0;j<4;j++)
                    for (int side=-1;side<=1;side+=2)
                    {
                        var p=On(.10f+j*.071f,.81f+Mathf.Sin(j*.95f)*.06f);
                        p+=V(-Mathf.Sin(a),0,Mathf.Cos(a))*side*.055f;
                        mesh.Ball(j%2==0?F.LightLeaf:F.Leaf,p,V(.146f-j*.014f,.042f,.088f),
                            V(0,-a*Mathf.Rad2Deg+side*48,side*11),3);
                    }
                }
                break;
            case 54:
                Box(F.Wood,0,.024f,0,.49f,.044f,.205f);
                Pot(-.158f,.044f,0,.073f,.097f,F.Cream); Pot(0,.044f,0,.077f,.075f,F.Terra); Pot(.156f,.044f,0,.071f,.093f,F.Sage);
                Ring(F.Leaf,-.158f,.126f,0,(0,.035f),(.019f,.050f),(.141f,.045f),(.161f,.026f));
                Orb(F.LightLeaf,0,.18f,0,.116f,.147f,.113f);
                Blossom(V(0,.255f),.025f,F.Terra);
                Orb(F.Leaf,.157f,.180f,0,.095f,.146f,.067f,12);
                Orb(F.Leaf,.20f,.254f,0,.068f,.095f,.046f,-24);
                Orb(F.LightLeaf,.119f,.252f,0,.061f,.086f,.041f,24);
                break;
            case 55:
                Pot(0,0,0,.16f,.18f,F.Terra);
                Foliage(0,.17f,0,.17f,.17f,5);
                for (int side=-1;side<=1;side+=2)
                {
                    float h=.48f+side*.042f;
                    mesh.Tube(F.Pine,.011f,V(side*.026f,.17f),V(side*.075f,h-.12f),V(side*.14f,h),V(side*.20f,h-.052f,.02f));
                    Beam(F.Brass,V(side*.082f,.16f,-.03f),V(side*.095f,h-.045f,-.03f),.008f);
                    Blossom(V(side*.142f,h,.025f),.048f,F.Cream);
                    Blossom(V(side*.207f,h-.056f,.037f),.039f,F.Cream);
                }
                break;
            case 56:
                Frame(.69f,.61f,F.Pine);
                for (int row=0;row<4;row++)
                for (int col=0;col<5;col++)
                {
                    float x=(col-2)*.112f+Mathf.Sin(row*2+col)*.018f;
                    float y=.13f+row*.116f+Mathf.Sin(col*1.8f)*.018f;
                    var color=(row+col*2)%5<2?F.LightLeaf:(row+col)%2==0?F.Leaf:F.Sage;
                    mesh.Ball(color,V(x,y,.073f),V(.155f,.151f,.106f),V(0,0,row*22),3);
                }
                break;
            case 57:
                Box(F.Wood,0,.026f,0,.48f,.047f,.166f);
                Box(F.Wood,0,.164f,0,.48f,.042f,.143f);
                for (int i=-1;i<=1;i+=2)
                    Beam(F.Wood,V(i*.219f,.027f),V(i*.192f,.18f),.023f);
                for (int i=-1;i<=1;i++)
                {
                    float x=i*.137f;
                    Orb(F.Glass,x,.091f,0,.104f,.117f,.10f);
                    Ring(F.Glass,x,.143f,0,(0,.026f),(.042f,.026f),(.047f,.022f));
                    mesh.Ball(F.Blue,V(x,.063f),V(.081f,.044f,.079f),latitudeRings:3);
                    mesh.Tube(F.Leaf,.008f,V(x,.056f),V(x,.16f),V(x+i*.03f,.27f+(.04f*(1-i*i))));
                    for (int leaf=0;leaf<3;leaf++)
                        mesh.Ball(leaf%2==0?F.LightLeaf:F.Leaf,V(x+Mathf.Sin(leaf*2)*.035f,.21f+leaf*.045f,.019f),
                            V(.075f,.034f,.037f),V(0,leaf*40,i*20),3);
                }
                break;
            case 58:
                Pot(0,0,0,.178f,.25f,F.Charcoal);
                Ring(F.Cream,0,.06f,0,(0,.149f),(.018f,.153f));
                Ring(F.Sage,0,.202f,0,(0,.176f),(.022f,.180f));
                for (int i=-1;i<=1;i+=2)
                    mesh.Tube(F.Brass,.011f,V(i*.17f,.20f,-.037f),V(i*.205f,.16f),V(i*.17f,.20f,.037f));
                for (int i=0;i<7;i++)
                {
                    float a=i*2.4f;
                    Tulip(V(0,.19f),V(Mathf.Cos(a)*.16f,.38f+i%3*.056f,Mathf.Sin(a)*.13f),i%2==0?F.Cream:F.Terra);
                }
                break;
            case 59:
                Ring(F.Cream,0,0,0,(0,.084f),(.019f,.108f),(.12f,.103f),(.29f,.068f),(.32f,.079f),(.324f,.056f),(.302f,.054f));
                mesh.Tube(F.Cream,.016f,V(.056f,.271f),V(.13f,.247f),V(.131f,.184f),V(.09f,.135f));
                for (int i=-1;i<=1;i+=2)
                {
                    var tip=V(i*.15f,.58f+i*.044f);
                    Beam(F.DarkWood,V(0,.25f),tip,.009f);
                    OliveSprig(tip,i<0?2.65f:.22f,.082f);
                }
                break;
            case 60:
                Box(F.Pine,0,.093f,0,1.22f,.18f,.39f);
                Box(F.DarkWood,0,.184f,0,1.14f,.03f,.30f);
                for (int side=-1;side<=1;side+=2)
                {
                    Box(F.Wood,side*.55f,.62f,0,.075f,1.18f,.08f);
                    Box(F.Wood,side*.55f,.04f,0,.20f,.075f,.38f);
                }
                Box(F.Wood,0,1.15f,0,1.15f,.066f,.078f);
                for (int i=0;i<5;i++)
                {
                    Box(F.Wood,(i-2)*.204f,.664f,0,.044f,.99f,.032f);
                    Box(F.Wood,0,.27f+i*.195f,0,1.06f,.040f,.04f);
                }
                for (int i=0;i<16;i++)
                {
                    float x=-.47f+(i%6)*.177f;
                    float y=.27f+(i%4)*.205f+Mathf.Sin(i*1.7f)*.05f;
                    var p=V(x,y,i%2==0?.064f:-.062f);
                    mesh.Ball(i%3==0?F.LightLeaf:F.Leaf,p,V(.17f,.13f,.064f),V(0,0,(i%3-1)*38),3);
                }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(number));
        }
    }

    private void BonsaiCrown(Vector3 point, Vector3 joint, float width)
    {
        Beam(F.DarkWood,joint,point-Vector3.up*.033f,.019f);
        mesh.Ball(F.Leaf,point,V(width,.096f,width*.72f),latitudeRings:4);
        mesh.Ball(F.LightLeaf,point+V(.028f,.038f,-.013f),V(width*.68f,.063f,width*.56f),latitudeRings:4);
    }

    private void OliveSprig(Vector3 tip,float angle,float length)
    {
        var direction=V(Mathf.Cos(angle),.38f,Mathf.Sin(angle)).normalized;
        for (int i=0;i<4;i++)
        {
            float side=i%2==0?1:-1;
            var basePoint=tip-direction*length*(3-i)*.68f;
            var p=basePoint+V(-Mathf.Sin(angle),.23f,Mathf.Cos(angle))*length*.42f*side;
            mesh.Ball(i%2==0?F.Leaf:F.Sage,p,V(length*1.55f,length*.30f,length*.61f),V(0,-angle*Mathf.Rad2Deg+side*31,24),3);
        }
        var fruit=tip-direction*length*.9f-Vector3.up*length*.32f;
        mesh.Ball(F.LightLeaf,fruit,V(length*.53f,length*.68f,length*.51f),latitudeRings:3);
    }

    private void Blossom(Vector3 center,float radius,F petal)
    {
        for (int i=0;i<5;i++)
        {
            float a=i*Mathf.PI*.4f;
            var p=center+V(Mathf.Cos(a),Mathf.Sin(a))*radius*.73f;
            mesh.Ball(petal,p,V(radius,radius,.025f),V(0,0,i*72),3);
        }
        mesh.Ball(F.Brass,center+Vector3.forward*.013f,V(radius*.66f,radius*.67f,.026f),latitudeRings:3);
    }

    private void Tulip(Vector3 start,Vector3 tip,F petals)
    {
        Beam(F.Leaf,start,tip,.010f);
        var middle=Vector3.Lerp(start,tip,.6f)+Vector3.right*.036f;
        mesh.Ball(F.Leaf,middle,V(.060f,.135f,.031f),V(0,0,-34),3);
        Ring(petals,tip.x,tip.y,tip.z,(-.030f,.022f),(-.003f,.050f),(.055f,.043f),(.071f,.025f));
        mesh.Ball(petals,tip+V(.024f,.029f,0),V(.059f,.086f,.061f),V(0,0,-16),3);
    }
}

