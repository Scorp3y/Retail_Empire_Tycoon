using System;
using UnityEngine;
using F = DecorMesh.Finish;

internal sealed partial class DecorArt
{
    private void BuildWallDecor(int number)
    {
        switch(number)
        {
            case 81:
                Box(F.Sage,0,.176f,0,.485f,.345f,.055f);
                Box(F.Cream,0,.071f,.033f,.46f,.115f,.015f);
                Disc(F.Cream,0,.228f,.033f,.084f,.009f);
                Disc(F.Sage,0,.228f,.048f,.071f,.004f);
                Box(F.Cream,0,.25f,.057f,.012f,.049f,.008f);
                Box(F.Cream,.014f,.222f,.057f,.039f,.012f,.008f,-28);
                Lettering("STORE HOURS",0,.074f,.043f,.010f,F.Pine);
                break;
            case 82:
                Box(F.Pine,0,.122f,0,.43f,.238f,.064f);
                for(int side=-1;side<=1;side+=2)
                {
                    Beam(F.Pine,V(side*.13f,.227f),V(side*.13f,.411f),.012f);
                    Ring(F.Brass,side*.13f,.405f,0,(0,.031f),(.024f,.034f));
                }
                break;
            case 83:
                Box(F.Wood,0,.336f,0,.66f,.058f,.062f);
                for(int i=-1;i<=1;i++)
                {
                    float x=i*.193f;
                    mesh.Tube(F.Brass,.011f,V(x,.321f),V(x,.295f,.041f),V(x,.254f,.043f));
                    Box(F.Cream,x,.139f,.035f,.162f,.236f,.046f);
                }
                Box(F.Pine,-.20f,.12f,.071f,.078f,.058f,.020f);
                mesh.Tube(F.Pine,.007f,V(-.165f,.146f,.073f),V(-.143f,.142f,.073f),V(-.155f,.107f,.073f),V(-.172f,.106f,.073f));
                Orb(F.Terra,0,.135f,.068f,.114f,.070f,.029f,24);
                for(int i=-1;i<=1;i++)Box(F.Cream,i*.023f,.136f+i*.007f,.084f,.008f,.026f,.006f,34);
                Box(F.Pine,.197f,.126f,.065f,.009f,.102f,.008f,-12);
                Orb(F.Leaf,.174f,.143f,.069f,.045f,.069f,.016f,32);
                Orb(F.Leaf,.218f,.165f,.069f,.043f,.072f,.016f,-44);
                break;
            case 84:
                mesh.Profile(F.Terra,V(0,.241f),new[]{(0f,.188f),(.021f,.204f),(.049f,.200f),(.055f,.188f)},12,1.18f,V(90,0));
                mesh.Tube(F.Brass,.011f,V(-.103f,.091f,.061f),V(-.017f,.195f,.07f),V(.031f,.322f,.07f),V(.046f,.403f,.07f));
                for(int i=0;i<5;i++)
                {
                    float side=i%2==0?1:-1;
                    var point=V(-.042f+i*.021f+side*.043f,.155f+i*.051f,.079f);
                    Orb(F.Brass,point.x,point.y,point.z,.079f,.119f,.029f,-side*39);
                }
                break;
            case 85:
                Frame(.71f,.47f,F.Cream);
                mesh.Tube(F.Blue,.031f,V(-.29f,.055f,.052f),V(-.19f,.137f,.052f),V(-.114f,.17f,.052f),
                    V(-.026f,.216f,.052f),V(.015f,.294f,.052f),V(.107f,.414f,.052f));
                for (int i=0;i<4;i++)
                {
                    Box(F.Brass,-.223f,.216f+i*.051f,.055f,.163f,.014f,.006f,17);
                    Box(F.Brass,.217f,.108f+i*.082f,.055f,.205f,.013f,.006f,-17);
                }
                Box(F.Brass,.195f,.223f,.056f,.018f,.328f,.006f,19);
                Box(F.Brass,-.235f,.278f,.056f,.014f,.246f,.006f,-16);
                Box(F.Sage,.228f,.263f,.061f,.089f,.095f,.011f,-18);
                break;
            case 86:
                Box(F.Pine,0,.245f,.029f,.112f,.237f,.058f);
                mesh.Tube(F.Pine,.028f,V(0,.232f,.051f),V(0,.32f,.133f),V(0,.388f,.221f),V(0,.345f,.269f),V(0,.288f,.24f));
                Ring(F.Brass,0,.122f,.24f,(0,.089f),(.020f,.094f),(.037f,.074f),(.108f,.05f),(.132f,.038f));
                Beam(F.DarkWood,V(0,.143f,.24f),V(0,.051f,.24f),.009f);
                Orb(F.Brass,0,.036f,.24f,.034f,.056f,.035f);
                break;
            case 87:
                Box(F.Wood,0,.285f,.025f,.62f,.562f,.05f);
                for(int row=0;row<5;row++)
                for(int col=0;col<7;col++)
                    mesh.Dot(F.DarkWood,V((col-3)*.077f,.106f+row*.089f,.051f),.012f);
                for(int i=-1;i<=1;i++)Disc(F.Cream,i*.143f,.425f,.052f,.026f,.061f);
                Box(F.Wood,-.111f,.159f,.117f,.31f,.039f,.139f);
                Box(F.Wood,.132f,.277f,.111f,.235f,.039f,.129f);
                break;
            case 88:
                Box(F.Cream,0,.154f,0,.418f,.254f,.172f);
                Box(F.Pine,0,.153f,.094f,.333f,.158f,.020f);
                Box(F.Charcoal,0,.151f,.106f,.299f,.011f,.007f);
                Lettering("10:24",0,.151f,.114f,.016f,F.Cream);
                Box(F.Brass,.066f,.288f,0,.116f,.025f,.061f);
                for(int i=-1;i<=1;i+=2)Box(F.Brass,i*.127f,.029f,0,.060f,.041f,.104f);
                break;
            case 89:
                Box(F.Wood,0,.047f,.058f,.925f,.063f,.138f);
                for(int i=-1;i<=1;i++)
                {
                    float x=i*.292f;
                    F tone=i<0?F.Cream:i==0?F.Sage:F.Terra;
                    Disc(F.DarkWood,x,.184f,.029f,.132f,.027f);
                    Disc(tone,x,.185f,.064f,.125f,.015f);
                    Disc(tone,x,.185f,.086f,.107f,.012f);
                    Blossom(V(x,.183f,.102f),.045f,i<0?F.Leaf:F.Cream);
                    for(int side=-1;side<=1;side+=2) Box(F.Brass,x+side*.075f,.077f,.114f,.027f,.060f,.035f);
                }
                break;
            case 90:
                Beam(F.Wood,V(-.325f,.441f,.028f),V(.325f,.441f,.028f),.027f);
                for(int i=-4;i<=4;i++)
                {
                    float x=i*.058f;
                    Box(F.Cream,x,.267f,.062f,.056f,.319f,.035f);
                    Box(F.Sage,x,.21f+Mathf.Abs(i)*.032f,.082f,.056f,.065f,.016f);
                    Box(F.Cream,x,.100f+Mathf.Abs(i)*.017f,.056f,.033f,.138f,.023f,i*1.8f);
                }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(number));
        }
    }

    private void BuildExterior(int number)
    {
        switch(number)
        {
            case 91:
                for(int x=-1;x<=1;x+=2)
                for(int z=-1;z<=1;z+=2)
                {
                    Box(F.DarkWood,x*.74f,.063f,z*.66f,.172f,.125f,.172f);
                    Box(F.Wood,x*.74f,.94f,z*.66f,.131f,1.84f,.131f);
                    Beam(F.Wood,V(x*.74f,1.48f,z*.66f),V(x*.49f,1.80f,z*.66f),.035f);
                    Beam(F.Wood,V(x*.74f,1.48f,z*.66f),V(x*.74f,1.80f,z*.42f),.034f);
                }
                for(int side=-1;side<=1;side+=2) Box(F.DarkWood,0,1.81f,side*.67f,1.74f,.115f,.105f);
                for(int i=-3;i<=3;i++) Box(F.Wood,i*.231f,1.921f,0,.102f,.097f,1.72f);
                break;
            case 92:
                Ring(F.Cream,0,0,0,(0,.233f),(.035f,.239f),(.071f,.202f),(.132f,.15f),(.49f,.108f),(.57f,.147f));
                Ring(F.Cream,0,.551f,0,(0,.151f),(.041f,.228f),(.108f,.300f),(.154f,.321f),(.169f,.301f),(.169f,.269f),(.102f,.223f));
                Ring(F.Blue,0,.641f,0,(0,.222f),(.031f,.263f));
                break;
            case 93:
                for(int i=0;i<3;i++)
                {
                    float a=i*2.094f;
                    Beam(F.Wood,V(Mathf.Cos(a)*.235f,0,Mathf.Sin(a)*.235f),V(Mathf.Cos(a)*.19f,.169f,Mathf.Sin(a)*.19f),.049f);
                }
                Ring(F.Pine,0,.115f,0,(0,.215f),(.042f,.254f),(.558f,.254f),(.579f,.232f));
                for(int i=0;i<3;i++) Ring(F.Sage,0,.194f+i*.165f,0,(0,.256f),(.025f,.263f),(.04f,.256f));
                Ring(F.Cream,0,.679f,0,(0,.252f),(.033f,.272f),(.074f,.236f),(.089f,.072f),(.118f,.069f));
                mesh.Tube(F.Brass,.024f,V(0,.24f,.244f),V(0,.24f,.329f),V(0,.198f,.35f),V(0,.171f,.35f));
                Beam(F.Brass,V(-.044f,.269f,.307f),V(.044f,.269f,.307f),.011f);
                break;
            case 94:
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*.251f;
                    Ring(F.Cream,x,0,0,(0,.154f),(.055f,.154f),(.092f,.084f),(.143f,.083f),(.173f,.136f),(.286f,.19f),(.341f,.19f),(.35f,.17f));
                    Ring(F.Pine,x,.328f,0,(0,.154f),(.144f,.161f),(.43f,.119f),(.62f,.063f),(.77f,.012f));
                    for(int i=0;i<4;i++)
                    {
                        float a=i*1.571f;
                        mesh.Ball(i%2==0?F.Leaf:F.Sage,V(x+Mathf.Cos(a)*.084f,.64f,Mathf.Sin(a)*.08f),V(.172f,.31f,.168f),latitudeRings:3);
                    }
                }
                break;
            case 95:
                for(int i=-1;i<=1;i++)
                {
                    float x=i*.81f;
                    Box(F.Wood,x,.49f,0,.088f,.97f,.14f);
                    Box(F.DarkWood,x,.050f,0,.125f,.098f,.21f);
                    Box(F.Wood,x,.975f,0,.123f,.052f,.164f);
                }
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*.406f;
                    Box(F.Sage,x,.242f,0,.768f,.38f,.078f);
                    Box(F.Glass,x,.661f,0,.768f,.41f,.024f);
                    for(int i=0;i<3;i++) Box(F.Wood,x,.057f+i*.418f,0,.768f,.052f,.103f);
                    for(int i=-1;i<=1;i++) Box(F.Pine,x+i*.18f,.234f,.043f,.011f,.297f,.010f);
                }
                break;
            case 96:
                Box(F.Wood,-.087f,.356f,0,.71f,.257f,.44f);
                Box(F.DarkWood,-.087f,.486f,0,.63f,.032f,.365f);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(F.Wood,-.086f,.401f,side*.232f,.715f,.059f,.041f);
                    Box(F.Wood,.323f,.281f,side*.152f,.29f,.043f,.045f,15);
                    Box(F.Wood,.237f,.154f,side*.151f,.055f,.295f,.059f);
                    Wheel(-.291f,.187f,side*.249f,.168f);
                }
                for(int i=0;i<9;i++)
                {
                    float x=-.34f+i%5*.113f;
                    Tulip(V(x,.486f,(i%3-1)*.063f),V(x+Mathf.Sin(i*2)*.038f,.643f+i%3*.052f,(i%3-1)*.092f),
                        i%3==0?F.Cream:i%3==1?F.Terra:F.Red);
                }
                break;
            case 97:
                for(int side=-1;side<=1;side+=2)
                {
                    Box(F.Pine,side*.83f,.047f,0,.154f,.090f,.174f);
                    Beam(F.Pine,V(side*.83f,.071f),V(side*.83f,1.016f),.026f);
                    Orb(F.Brass,side*.83f,1.038f,0,.075f,.079f,.075f);
                }
                SaggingRope(1.64f,.999f,.20f,F.DarkWood,.013f);
                for(int i=0;i<7;i++)
                {
                    float t=(i+1)/8f;
                    mesh.Flag(new[]{F.Sage,F.Brass,F.Cream}[i%3],V(1.64f*(t-.5f),Sag(t,.999f,.20f),.002f),.159f,.19f);
                }
                break;
            case 98:
                SaggingRope(.98f,.273f,.133f,F.DarkWood,.010f);
                for(int side=-1;side<=1;side+=2) Disc(F.Brass,side*.48f,.275f,0,.035f,.016f);
                for(int i=0;i<7;i++)
                {
                    float t=(i+.7f)/7.4f;
                    var center=V((t-.5f)*.98f,Sag(t,.273f,.133f)-.041f,.025f);
                    mesh.MapleLeaf(new[]{F.Terra,F.Brass,F.Red,F.Sage}[i%4],center,.129f,(i%3-1)*24);
                }
                break;
            case 99:
                Lantern(-.125f,0,0,.090f,.30f,true);
                Lantern(.104f,0,.061f,.077f,.225f,true);
                for(int i=0;i<2;i++)
                {
                    float x=i==0?-.125f:.104f,y=i==0?.329f:.254f,z=i==0?0:.061f;
                    for(int j=-1;j<=1;j++) Orb(F.Leaf,x+j*.027f,y,z,.069f,.022f,.058f,j*19);
                    mesh.Ball(F.Red,V(x,y+.016f,z+.017f),V(.034f,.033f,.036f),latitudeRings:3);
                    mesh.Tube(F.Pine,.010f,V(x-.035f,y-.004f,z),V(x-.039f,y+.068f,z),V(x+.037f,y+.068f,z),V(x+.036f,y-.004f,z));
                }
                break;
            case 100:
                mesh.Profile(F.Pine,V(0,.237f),new[]{(0f,.217f),(.014f,.241f),(.050f,.238f),(.065f,.212f)},6,1,V(90,0,30));
                mesh.Profile(F.Wood,V(0,.237f,.068f),new[]{(0f,.185f),(.015f,.184f)},6,1,V(90,0,30));
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3;
                    var direction=V(Mathf.Sin(a),Mathf.Cos(a));
                    var center=V(0,.237f,.092f);
                    Beam(F.Cream,center,center+direction*.155f,.017f);
                    for(int side=-1;side<=1;side+=2)
                    {
                        var arm=Quaternion.Euler(0,0,side*50)*direction;
                        Beam(F.Cream,center+direction*.090f,center+direction*.090f+arm*.058f,.011f);
                    }
                }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(number));
        }
    }

    private void Wheel(float x,float y,float z,float r)
    {
        Disc(F.Charcoal,x,y,z-.021f,r,.047f);
        Disc(F.Wood,x,y,z+.029f,r*.79f,.012f);
        for(int i=0;i<7;i++)
        {
            float a=i*Mathf.PI*2/7;
            var tip=V(x+Mathf.Cos(a)*r*.74f,y+Mathf.Sin(a)*r*.74f,z+.047f);
            Beam(F.Brass,V(x,y,z+.047f),tip,.010f);
        }
        Disc(F.Pine,x,y,z+.052f,r*.20f,.011f);
    }

    private static float Sag(float t,float high,float drop)=>high-4*t*(1-t)*drop;

    private void SaggingRope(float width,float height,float drop,F finish,float radius)
    {
        var path=new Vector3[9];
        for(int i=0;i<path.Length;i++)
        {
            float t=i/(path.Length-1f);
            path[i]=V(width*(t-.5f),Sag(t,height,drop));
        }
        mesh.Tube(finish,radius,path);
    }

    private void Star(F finish,Vector3 center,float outer,float inner,int points,float depth)
    {
        var outline=new Vector2[points*2];
        for(int i=0;i<outline.Length;i++)
        {
            float angle=i*Mathf.PI/points,r=i%2==0?outer:inner;
            outline[i]=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*r;
        }
        mesh.Badge(finish,center,outline,depth);
    }

    private void Lettering(string label,float x,float y,float z,float pixel,F finish)
    {
        float start=x+(label.Length*4-2)*pixel*.5f;
        for(int letter=0;letter<label.Length;letter++)
        {
            string glyph=label[letter] switch
            {
                '0'=>"111101101101111", '1'=>"010110010010111", '2'=>"111001111100111",
                '3'=>"111001111001111", '4'=>"101101111001001", ':'=>"000010000010000",
                'S'=>"111100111001111", 'T'=>"111010010010010", 'O'=>"111101101101111",
                'R'=>"110101110101101", 'E'=>"111100110100111", 'H'=>"101101111101101",
                'U'=>"101101101101111", ' '=>"000000000000000",
                _=>throw new ArgumentOutOfRangeException(nameof(label))
            };
            for(int index=0;index<glyph.Length;index++)
                if(glyph[index]=='1')
                    mesh.Stamp(finish,V(start-(letter*4+index%3)*pixel,y+(2-index/3)*pixel,z),pixel*.94f,pixel*.94f);
        }
    }
}
