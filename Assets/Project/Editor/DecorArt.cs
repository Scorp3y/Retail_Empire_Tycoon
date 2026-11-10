using System;
using UnityEngine;
using F = DecorMesh.Finish;

internal sealed class DecorArt
{
    private readonly DecorMesh mesh = new();

    internal static DecorMesh Make(int number)
    {
        var art = new DecorArt();
        art.Build(number);
        return art.mesh;
    }

    private static Vector3 V(float x, float y, float z = 0) => new(x,y,z);
    private void Box(F f, float x, float y, float z, float w, float h, float d, float tilt = 0) =>
        mesh.Box(f,V(x,y,z),V(w,h,d),V(0,0,tilt));
    private void Orb(F f, float x, float y, float z, float w, float h, float d, float tilt = 0) =>
        mesh.Ball(f,V(x,y,z),V(w,h,d),V(0,0,tilt));
    private void Ring(F f, float x, float y, float z, params (float height, float radius)[] profile) =>
        mesh.Profile(f,V(x,y,z),profile);
    private void Beam(F f, Vector3 a, Vector3 b, float r) => mesh.Tube(f,r,a,b);
    private void Disc(F f, float x, float y, float z, float r, float depth = .025f) =>
        mesh.Profile(f,V(x,y,z),new[] { (0f,r*.91f),(.005f,r),(depth,r),(depth+.005f,r*.92f) },12,1,V(90,0));

    private void Pot(float x, float y, float z, float r, float h, F finish)
    {
        Ring(finish,x,y,z,(0,r*.68f),(.018f,r*.78f),(h*.83f,r),(h,r),(h,r*.84f),(h*.82f,r*.84f));
        Ring(F.DarkWood,x,y,z,(h*.77f,r*.82f),(h*.825f,r*.82f));
    }

    private void Foliage(float x, float y, float z, float r, float h, int count, bool upright = false)
    {
        for (int i = 0; i < count; i++)
        {
            float a = i * 2.4f;
            var tip = V(x+Mathf.Cos(a)*r*.75f,y+h*(.66f+.12f*(i%3)),z+Mathf.Sin(a)*r*.7f);
            Beam(F.Leaf,V(x,y,z),tip,.012f);
            var size = upright ? V(r*.36f,h*.83f,r*.19f) : V(r*1.15f,h*.45f,r*.74f);
            mesh.Ball(i%3==0?F.LightLeaf:i%3==1?F.Leaf:F.Sage,tip,size,V(0,i*39,upright?(i-2)*9:i%2==0?30:-30));
        }
    }

    private void Flower(float x, float y, float z, float r, F petal)
    {
        Beam(F.Leaf,V(x,y-.16f,z),V(x,y,z),.009f);
        for (int i=0;i<5;i++)
        {
            float a=i*Mathf.PI*.4f;
            mesh.Ball(petal,V(x+Mathf.Cos(a)*r*.67f,y,z+Mathf.Sin(a)*r*.67f),V(r,r*.47f,r),latitudeRings:3);
        }
        Orb(F.Brass,x,y+.018f,z,r*.68f,r*.55f,r*.68f);
    }

    private void Frame(float w, float h, F face)
    {
        Box(F.Wood,0,h/2,0,w,h,.065f);
        Box(face,0,h/2,.037f,w-.065f,h-.065f,.016f);
    }

    private void Chair(float x, float z, float width, F seat)
    {
        for(int i=-1;i<=1;i+=2)
        for(int j=-1;j<=1;j+=2)
            Beam(F.Wood,V(x+i*width*.39f,.01f,z+j*.17f),V(x+i*width*.3f,.34f,z+j*.14f),.03f);
        Box(seat,x,.36f,z,width,.11f,.38f);
        mesh.Box(seat,V(x,.58f,z-.165f),V(width,.36f,.075f),V(-11,0));
    }

    private void Build(int n)
    {
        switch(n)
        {
            case 1:
                Ring(F.Terra,0,0,0,(0,.16f),(.025f,.20f),(.048f,.20f));
                Pot(0,.04f,0,.19f,.22f,F.Terra); Foliage(0,.25f,0,.25f,.41f,6); break;
            case 2:
                Pot(0,0,0,.17f,.30f,F.Cream); Foliage(0,.28f,0,.15f,.55f,7,true); break;
            case 3:
                Pot(0,0,0,.28f,.32f,F.Brass); Beam(F.DarkWood,V(0,.25f),V(.04f,1.17f),.045f);
                Foliage(0,.7f,0,.38f,.42f,9); break;
            case 4:
                Pot(0,.35f,0,.22f,.14f,F.Wood);
                for (int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3; float x=Mathf.Cos(a)*.18f,z=Mathf.Sin(a)*.18f;
                    Beam(F.Wood,V(x,.45f,z),V(0,.92f),.016f);
                    for(int j=0;j<5;j++) Orb(j%2==0?F.Leaf:F.LightLeaf,x*(1+j*.08f),.5f-j*.09f,z*(1+j*.08f),.15f,.15f,.095f,25);
                }
                break;
            case 5:
                Pot(0,0,0,.105f,.115f,F.Pine); Foliage(0,.10f,0,.13f,.13f,7); break;
            case 6:
            case 41:
                Box(n==6?F.Wood:F.Cream,0,.12f,0,.9f,.24f,.37f);
                Box(F.DarkWood,0,.243f,0,.81f,.03f,.29f);
                for(int i=-1;i<=1;i++) Foliage(i*.23f,.25f,0,.18f,.27f,4);
                if(n==41) for(int i=-1;i<=1;i++) Flower(i*.19f,.51f,i%2*.06f,.057f,i==0?F.Cream:F.Terra);
                break;
            case 7:
                Ring(F.Cream,0,0,0,(0,.063f),(.03f,.09f),(.12f,.094f),(.18f,.044f),(.23f,.05f));
                for(int i=-2;i<=2;i++)
                {
                    var end=V(i*.041f,.31f+(2-Mathf.Abs(i))*.042f,i*.018f);
                    Beam(F.Wood,V(0,.19f),end,.009f);
                    Orb(i%2==0?F.Brass:F.Terra,end.x,end.y,end.z,.042f,.077f,.045f,i*15);
                }
                break;
            case 8:
                Box(F.Wood,0,.21f,0,.46f,.42f,.045f);
                Box(F.Cream,0,.18f,.09f,.37f,.19f,.16f); Foliage(0,.27f,.11f,.18f,.16f,5);
                Orb(F.Leaf,.12f,.13f,.18f,.13f,.25f,.08f,31); break;
            case 9:
                for(int i=-1;i<=1;i+=2) Beam(F.Wood,V(i*.28f,0,.23f),V(i*.28f,1.04f,-.13f),.035f);
                for(int i=0;i<3;i++)
                {
                    float y=.22f+i*.30f,z=.08f-i*.09f;
                    Box(F.Wood,0,y,z,.61f,.06f,.31f);
                    Pot(0,y+.03f,z,.09f,.11f,i%2==0?F.Terra:F.Cream); Foliage(0,y+.12f,z,.10f,.16f,4);
                }
                break;
            case 10:
                Ring(F.DarkWood,0,0,0,(0,.16f),(.022f,.20f),(.068f,.20f));
                for(int i=-1;i<=1;i++) Orb(i==0?F.Sage:F.Leaf,i*.084f,.10f,0,.20f,.14f,.21f);
                Orb(F.Cream,.066f,.16f,.05f,.095f,.08f,.09f);
                mesh.Profile(F.Glass,V(0,.06f),new[]{(0f,.194f),(.15f,.177f),(.245f,.105f),(.26f,.056f)},8,1,default,false);
                break;
            case 11:
                for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Beam(F.Wood,V(Mathf.Cos(a)*.23f,0,Mathf.Sin(a)*.23f),V(0,.90f),.041f);}
                Ring(F.Pine,0,.89f,0,(0,.08f),(.11f,.08f));
                Ring(F.Cream,0,.98f,0,(0,.23f),(.035f,.25f),(.27f,.16f),(.30f,.146f));
                Ring(F.Glow,0,.975f,0,(0,.195f),(.017f,.195f)); break;
            case 12:
            case 15:
                Beam(F.Pine,V(0,.22f),V(0,.49f),.021f);
                Ring(F.Charcoal,0,.47f,0,(0,.07f),(.035f,.08f));
                Orb(F.Glow,0,.13f,0,.22f,.17f,.22f);
                if(n==12) Ring(F.Sage,0,.09f,0,(0,.24f),(.04f,.24f),(.15f,.18f),(.21f,.06f));
                else
                {
                    Ring(F.Brass,0,.10f,0,(0,.23f),(.025f,.24f),(.045f,.23f),(.045f,.20f),(.005f,.20f), (0,.23f));
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4; Vector3 On(float r,float y)=>V(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
                        mesh.Tube(F.Wood,.022f,On(.045f,.31f),On(.15f,.28f),On(.22f,.20f),On(.226f,.13f));
                    }
                }
                break;
            case 13:
                Box(F.Charcoal,0,.25f,0,.94f,.052f,.13f);
                for(int i=-1;i<=1;i++)
                {
                    Box(F.Charcoal,i*.28f,.17f,0,.033f,.14f,.035f);
                    mesh.Profile(F.Charcoal,V(i*.28f,.11f),new[]{(-.08f,.074f),(-.05f,.078f),(.06f,.071f)},10,1,V(-31,0,i*17));
                    mesh.Profile(F.Glow,V(i*.28f,.11f),new[]{(-.083f,.062f),(-.078f,.065f)},10,1,V(-31,0,i*17));
                }
                break;
            case 14:
                Disc(F.Brass,0,.20f,0,.10f);
                Beam(F.Brass,V(0,.21f,0),V(0,.21f,.16f),.034f);
                Orb(F.Glow,0,.10f,.16f,.17f,.18f,.17f); break;
            case 16:
                Box(F.Brass,0,.035f,0,.27f,.07f,.17f);
                mesh.Tube(F.Brass,.028f,V(0,.06f),V(0,.26f),V(0,.31f,.063f));
                Box(F.Pine,0,.30f,.08f,.40f,.115f,.18f);
                Box(F.Glow,0,.24f,.08f,.31f,.024f,.116f); break;
            case 17:
                var path=new Vector3[9];
                for(int i=0;i<path.Length;i++){float t=i/8f;path[i]=V((t-.5f),.10f+.29f*Mathf.Pow(2*t-1,2));}
                mesh.Tube(F.Charcoal,.013f,path);
                for(int i=1;i<8;i++){var p=path[i];Orb(F.Glow,p.x,p.y-.055f,p.z,.067f,.085f,.068f);}
                break;
            case 18:
                Box(F.Brass,0,.14f,0,.12f,.12f,.05f);
                Beam(F.Brass,V(0,.14f),V(0,.12f,.16f),.024f);
                Box(F.Brass,0,.12f,.17f,.40f,.068f,.092f);
                Box(F.Glow,0,.082f,.17f,.36f,.021f,.069f); break;
            case 19:
            case 20:
                float h=n==19?1.94f:.44f, r=n==19?.10f:.095f;
                Ring(F.Pine,0,0,0,(0,r),(.045f,r),(.085f,r*.78f),(h-.10f,r*.43f));
                if(n==19)r*=1.47f;
                Ring(F.Glow,0,h-.12f,0,(0,r*.68f),(.065f,r*.73f),(.12f,r*.91f));
                Ring(F.Pine,0,h,0,(0,r*1.10f),(.036f,r*.88f),(.065f,r*.43f)); break;
            case 21:
            case 22:
                bool back=n==22;
                for(int i=-1;i<=1;i+=2)
                {
                    Box(F.Pine,i*.48f,.21f,0,.12f,.42f,.39f);
                    if(back) Beam(F.Sage,V(i*.48f,.30f,-.11f),V(i*.48f,.73f,-.17f),.044f);
                }
                for(int i=-1;i<=1;i++) Box(F.Wood,0,.42f,i*.11f,1.04f,.072f,.096f);
                if(back)for(int i=0;i<2;i++) Box(F.Wood,0,.55f+i*.13f,-.17f,1.01f,.10f,.05f);
                break;
            case 24:
                Chair(-.61f,.04f,.34f,F.Wood); Chair(.61f,-.04f,.34f,F.Wood);
                Ring(F.Sage,0,0,0,(0,.24f),(.06f,.17f),(.12f,.057f),(.64f,.057f),(.67f,.29f),(.73f,.31f),(.75f,.29f)); break;
            case 25: Chair(0,0,.60f,F.Terra); break;
            case 26:
                Box(F.Pine,0,.22f,0,.41f,.44f,.35f);
                mesh.Box(F.Sage,V(0,.47f,.038f),V(.39f,.10f,.31f),V(23,0));
                Box(F.Charcoal,0,.425f,.166f,.29f,.07f,.05f); break;
            case 27:
                Ring(F.Charcoal,0,0,0,(0,.15f),(.04f,.17f),(.36f,.17f),(.37f,.14f));
                for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9;mesh.Box(F.Wood,V(Mathf.Sin(a)*.16f,.195f,Mathf.Cos(a)*.16f),V(.086f,.31f,.038f),V(0,i*40));}
                for(int i=-1;i<=1;i+=2) Beam(F.Pine,V(i*.105f,.31f),V(i*.105f,.46f),.025f);
                Ring(F.Pine,0,.45f,0,(0,.19f),(.032f,.19f),(.075f,.13f)); break;
            case 28:
                Box(F.Cream,0,.28f,0,.97f,.55f,.43f);
                for(int i=-1;i<=1;i++)
                {
                    F color=i<0?F.Sage:i==0?F.Brass:F.Terra;
                    Box(color,i*.32f,.58f,0,.31f,.082f,.43f);
                    Box(F.Charcoal,i*.32f,.625f,0,.13f,.012f,.12f);
                    Disc(color,i*.32f,.32f,.219f,.052f);
                }
                break;
            case 29:
                Box(F.Cream,0,.014f,0,1.04f,.027f,.49f);
                Box(F.Pine,0,.028f,0,.96f,.012f,.41f);
                for(int i=-4;i<=4;i++) Box(F.Sage,0,.036f,i*.042f,.91f,.009f,.019f);
                break;
            case 31:
                Disc(F.Pine,0,.25f,0,.245f,.054f);
                Disc(F.Cream,0,.25f,.06f,.211f);
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6;
                    Box(F.Charcoal,Mathf.Sin(a)*.176f,.25f+Mathf.Cos(a)*.176f,.09f,.012f,.034f,.006f,-i*30);
                }
                Box(F.Pine,-.04f,.275f,.093f,.017f,.10f,.01f,54); Box(F.Pine,.058f,.291f,.094f,.014f,.145f,.01f,-55);
                Disc(F.Brass,0,.25f,.102f,.025f,.012f); break;
            case 32:
                Frame(.48f,.45f,F.Cream);
                Orb(F.LightLeaf,-.087f,.205f,.051f,.135f,.17f,.022f,-14);
                Orb(F.Terra,.075f,.185f,.051f,.16f,.139f,.023f);
                Box(F.DarkWood,-.08f,.30f,.057f,.013f,.063f,.016f,-15); Box(F.DarkWood,.071f,.273f,.057f,.012f,.058f,.016f,9);
                Orb(F.Leaf,.105f,.30f,.055f,.075f,.027f,.011f,30); break;
            case 33:
                Frame(.74f,.43f,F.Blue);
                for(int i=-2;i<=2;i++)
                {
                    float x=i*.118f,y=.15f+(.028f*(i*i%3));
                    Box(F.Cream,x,y,.051f,.10f,.14f,.013f);
                    Box(F.Terra,x,y+.095f,.055f,.080f,.080f,.014f,45);
                    Box(F.Pine,x,y-.02f,.061f,.025f,.064f,.010f);
                }
                Box(F.Sage,0,.075f,.066f,.62f,.04f,.014f); break;
            case 34:
                Disc(F.Pine,0,.44f,0,.20f,.046f); Disc(F.Pine,0,.20f,0,.20f,.046f);
                Box(F.Pine,0,.32f,.026f,.399f,.24f,.057f);
                Disc(F.Blue,0,.44f,.059f,.169f,.008f); Disc(F.Blue,0,.20f,.059f,.169f,.008f);
                Box(F.Blue,0,.32f,.061f,.336f,.24f,.015f);
                Box(F.Glass,.033f,.335f,.074f,.056f,.31f,.009f,-27); break;
            case 35:
            case 36:
                Frame(.70f,.44f,n==35?F.Pine:F.Wood);
                if(n==35)
                {
                    Disc(F.Cream,-.14f,.22f,.052f,.068f,.007f); Disc(F.Pine,-.14f,.22f,.064f,.053f,.004f);
                    Box(F.Cream,-.14f,.295f,.072f,.012f,.048f,.007f,-24);
                    Orb(F.Cream,.117f,.208f,.058f,.24f,.095f,.014f,13);
                    for(int i=-1;i<=1;i++)Box(F.Pine,.116f+i*.048f,.218f+i*.012f,.069f,.014f,.053f,.008f,25);
                }
                else for(int i=-1;i<=1;i++)
                {
                    Box(F.Cream,i*.158f,.22f-i*i*.028f,.054f,.111f,.139f,.015f,i*10);
                    Disc(i==0?F.Terra:F.Sage,i*.158f,.274f-i*i*.026f,.065f,.014f,.012f);
                }
                break;
            case 37:
                Box(F.Sage,0,.155f,0,.47f,.31f,.052f);
                for(int i=-1;i<=1;i+=2)
                {
                    Box(F.Cream,.08f,.155f+i*.065f,.033f,.16f,.018f,.012f);
                    Box(F.Cream,.157f,.17f+i*.065f,.034f,.049f,.014f,.012f,-42);
                    Box(F.Cream,.157f,.14f+i*.065f,.034f,.049f,.014f,.012f,42);
                    Disc(F.Cream,-.128f,.155f+i*.065f,.032f,.034f,.012f);
                }
                break;
            case 38:
                Box(F.Pine,0,.13f,0,.68f,.25f,.070f);
                for(int i=-1;i<=1;i+=2) Beam(F.Charcoal,V(i*.21f,.25f),V(i*.21f,.45f),.014f);
                Box(F.Wood,0,.44f,0,.68f,.051f,.092f);
                FreshLettering(); break;
            case 39:
                for(int i=-1;i<=1;i+=2) for(int j=0;j<2;j++) Box(j==0?F.Sage:F.Cream,i*.152f,.152f+j*.30f,0,.292f,.291f,.052f);
                for(int i=-1;i<=1;i+=2) Orb(F.Leaf,i*.101f,.37f,.038f,.13f,.28f,.019f,-i*46);
                Box(F.Pine,0,.25f,.039f,.022f,.34f,.016f); break;
            case 40:
                Box(F.Terra,0,.25f,0,.44f,.40f,.029f);
                Box(F.Terra,0,.087f,0,.123f,.123f,.029f,45);
                Beam(F.Wood,V(-.27f,.45f),V(.27f,.45f),.024f);
                for(int i=0;i<5;i++){float a=i*Mathf.PI*.4f;Disc(F.Cream,Mathf.Cos(a)*.060f,.245f+Mathf.Sin(a)*.06f,.021f,.046f,.006f);}
                Disc(F.Brass,0,.245f,.034f,.034f,.006f); break;
            case 42:
                Ring(F.Wood,0,0,0,(0,.16f),(.06f,.19f),(.20f,.22f),(.33f,.19f),(.37f,.18f));
                Ring(F.Charcoal,0,0,0,(.07f,.202f),(.105f,.207f));
                Ring(F.Charcoal,0,0,0,(.25f,.219f),(.281f,.215f));
                Foliage(0,.35f,0,.21f,.19f,5);
                for(int i=-1;i<=1;i++)Flower(i*.104f,.54f,i*.037f,.053f,F.Brass); break;
            case 43:
                for(int i=-1;i<=1;i+=2) mesh.Box(F.Pine,V(0,.29f,i*.117f),V(.41f,.62f,.045f),V(-i*22,0));
                mesh.Box(F.Charcoal,V(0,.32f,.144f),V(.33f,.46f,.025f),V(-22,0));
                Disc(F.Cream,0,.31f,.19f,.071f,.018f); Disc(F.Charcoal,0,.31f,.21f,.053f,.006f);
                Box(F.Cream,0,.21f,.225f,.20f,.017f,.012f); break;
            case 44:
                for(int i=-2;i<=2;i++)
                {
                    var tint=i%2==0?F.Sage:F.Cream;
                    mesh.Box(tint,V(i*.184f,.22f,.27f),V(.181f,.047f,.57f),V(22,0));
                    Box(tint,i*.184f,.087f,.523f,.179f,.12f,.04f);
                }
                for(int i=-1;i<=1;i+=2)mesh.Tube(F.Charcoal,.032f,V(i*.44f,.06f,0),V(i*.44f,.09f,.49f),V(i*.44f,.29f,.05f));
                break;
            case 45:
                Box(F.Pine,0,.031f,0,1.24f,.059f,.35f);
                for(int i=-1;i<=1;i++)
                {
                    float x=i*.36f;
                    mesh.Tube(F.Sage,.036f,V(x-.12f,.04f),V(x-.12f,.42f),V(x-.09f,.51f),V(x,.55f),
                        V(x+.09f,.51f),V(x+.12f,.42f),V(x+.12f,.04f));
                }
                break;
            case 46:
                Ring(F.Cream,0,0,0,(0,.69f),(.06f,.74f),(.25f,.79f),(.29f,.76f),(.29f,.66f),(.11f,.62f));
                Ring(F.Blue,0,0,0,(.18f,.65f),(.19f,.68f));
                Ring(F.Cream,0,0,0,(.19f,.23f),(.23f,.26f),(.30f,.20f),(.51f,.12f),(.55f,.23f),(.68f,.38f),(.72f,.36f),(.72f,.30f));
                Ring(F.Blue,0,0,0,(.695f,.31f),(.71f,.33f));
                Orb(F.Cream,0,.77f,0,.14f,.21f,.14f);
                for(int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3; Vector3 At(float r,float y)=>V(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
                    mesh.Tube(F.Glass,.025f,At(.33f,.70f),At(.45f,.55f),At(.56f,.21f));
                }
                break;
            case 47:
                Box(F.Wood,0,.105f,0,.70f,.21f,.43f);
                for(int i=-1;i<=1;i++) Orb(i==0?F.Terra:F.Brass,i*.17f,.28f+(i==0?.07f:0),i*.05f,.27f,.26f,.24f);
                for(int i=-1;i<=1;i++) Box(F.Leaf,i*.17f,.41f+(i==0?.07f:0),i*.05f,.039f,.07f,.04f,12);
                break;
            case 48:
                for(int i=0;i<10;i++)
                {
                    float a=i*Mathf.PI*.2f;
                    Orb(i%2==0?F.Pine:F.Leaf,Mathf.Cos(a)*.184f,.239f+Mathf.Sin(a)*.184f,0,.157f,.092f,.070f,i*36+65);
                }
                Orb(F.Red,-.06f,.08f,.05f,.13f,.074f,.046f,25);
                Orb(F.Red,.06f,.08f,.05f,.13f,.074f,.046f,-25);
                Disc(F.Terra,0,.08f,.077f,.035f,.02f); break;
            case 49:
                for(int i=-1;i<=1;i+=2)
                {
                    Box(F.DarkWood,i*.65f,.063f,0,.23f,.124f,.27f);
                    Box(F.Wood,i*.65f,.70f,0,.15f,1.35f,.17f);
                    mesh.Tube(F.Wood,.08f,V(i*.65f,1.36f),V(i*.51f,1.62f),V(i*.28f,1.77f),V(0,1.82f));
                    for(int j=0;j<3;j++)Flower(i*(.54f-j*.11f),1.53f+j*.09f,.11f,.065f,j==1?F.Terra:F.Cream);
                }
                break;
            case 50:
                Ring(F.Pine,0,0,0,(0,.23f),(.035f,.26f),(.10f,.22f));
                Beam(F.Wood,V(0,.09f),V(0,1.77f),.042f);
                mesh.Canopy(F.Terra,F.Cream,1.77f,1.51f,.68f);
                Ring(F.Brass,0,1.70f,0,(0,.09f),(.065f,.09f),(.09f,.057f)); break;
            default: throw new ArgumentOutOfRangeException(nameof(n));
        }
    }

    private void FreshLettering()
    {
        // Five block letters built into the sign mesh, with no texture or runtime font.
        string[] glyphs={"111100110100100","110101110101101","111100110100111","111100111001111","101101111101101"};
        for(int letter=0;letter<glyphs.Length;letter++)
            for(int index=0;index<glyphs[letter].Length;index++)
                if(glyphs[letter][index]=='1')
                    mesh.Stamp(F.Cream,V(.217f-letter*.099f-(index%3)*.022f,.178f-(index/3)*.026f,.041f),.023f,.027f);
    }
}

