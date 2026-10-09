using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal static class TownArchitecturalParts
    {
        public static void Window(TownMeshBuilder mesh,Vector3 center,float width,float height,bool side=false)
        {
            var rotation=side?Quaternion.Euler(0,90,0):Quaternion.identity;
            mesh.Box(center,new Vector3(width+.14f,height+.14f,.1f),TownColor.White,rotation);
            mesh.Box(center+rotation*Vector3.forward*.065f,new Vector3(width,height,.025f),TownColor.Glass,rotation);
            mesh.Box(center+rotation*Vector3.forward*.085f,new Vector3(.045f,height,.03f),TownColor.Frame,rotation);
            mesh.Box(center+rotation*Vector3.forward*.085f,new Vector3(width,.045f,.03f),TownColor.Frame,rotation);
            mesh.Box(center+rotation*new Vector3(0,-height*.5f-.07f,.1f),new Vector3(width+.26f,.11f,.24f),TownColor.Pavement,rotation);
        }

        public static void Door(TownMeshBuilder mesh,Vector3 center,float width,float height,bool doubleDoor=false)
        {
            mesh.Box(center,new Vector3(width+.18f,height+.12f,.12f),TownColor.White);
            mesh.Box(center+Vector3.forward*.085f,new Vector3(width,height,.08f),TownColor.Frame);
            int count=doubleDoor?2:1;
            for(int i=0;i<count;i++)
            {
                float x=center.x+(i-(count-1)*.5f)*width/count;
                mesh.Box(new Vector3(x,center.y+.24f,center.z+.14f),new Vector3(width/count-.15f,height*.55f,.025f),TownColor.Glass);
                mesh.Box(new Vector3(x+width/count*.28f,center.y-.15f,center.z+.17f),new Vector3(.05f,.3f,.06f),TownColor.Metal);
            }
            mesh.Box(center+new Vector3(0,-height*.5f+.075f,.38f),new Vector3(width+.6f,.15f,.9f),TownColor.Pavement);
        }

        public static void FlatRoof(TownMeshBuilder mesh,float width,float depth,float height)
        {
            mesh.Box(new Vector3(0,height,0),new Vector3(width+.38f,.2f,depth+.38f),TownColor.Mint);
            mesh.Box(new Vector3(0,height+.24f,-depth*.5f),new Vector3(width,.34f,.16f),TownColor.Cream);
            foreach(float x in new[]{-width*.5f,width*.5f})
                mesh.Box(new Vector3(x,height+.24f,0),new Vector3(.16f,.34f,depth),TownColor.Cream);
            mesh.Box(new Vector3(-width*.22f,height+.42f,-depth*.16f),new Vector3(.9f,.62f,.9f),TownColor.Metal);
            for(int i=0;i<4;i++)mesh.Box(new Vector3(-width*.22f,height+.74f,-depth*.16f+(i-1.5f)*.16f),new Vector3(.8f,.035f,.035f),TownColor.Frame);
        }

        public static void LoadingDoor(TownMeshBuilder mesh,Vector3 center,float width,float height,bool reverse=false,bool bollards=true)
        {
            float direction=reverse?-1:1;
            mesh.Box(center,new Vector3(width+.4f,height+.3f,.25f),TownColor.Frame);
            mesh.Box(center+Vector3.forward*.15f*direction,new Vector3(width,height,.08f),TownColor.Metal);
            for(float y=-height/2+.18f;y<height/2;y+=.28f)
                mesh.Box(center+new Vector3(0,y,.205f*direction),new Vector3(width,.028f,.035f),TownColor.Pavement);
            if(!bollards)return;
            foreach(float x in new[]{-width*.5f-.3f,width*.5f+.3f})
            {
                mesh.Cylinder(center+new Vector3(x,-height*.5f+.45f,.65f*direction),.12f,.12f,.9f,TownColor.Yellow);
                mesh.Cylinder(center+new Vector3(x,-height*.5f+.55f,.65f*direction),.125f,.125f,.12f,TownColor.Frame);
            }
        }

        public static void Sign(TownMeshBuilder mesh,Vector3 center,float width,float height,string text)
        {
            mesh.Box(center,new Vector3(width,height,.14f),TownColor.Frame);
            Text(mesh,center+Vector3.forward*.074f,width*.92f,height*.76f,text,TownColor.White);
        }

        // Geometric lettering remains editable in OBJ and does not require a runtime font component.
        public static void Text(TownMeshBuilder mesh,Vector3 center,float width,float height,string text,TownColor color)
        {
            float pixel=Mathf.Min(height/7,width/Mathf.Max(1,text.Length*6-1));
            // Viewed from +Z, positive world X is the left side of the sign.
            float start=center.x+(text.Length*6-1)*pixel*.5f;
            for(int character=0;character<text.Length;character++)
            {
                string glyph=Glyph(text[character]);
                for(int row=0;row<7;row++) for(int col=0;col<5;col++)
                {
                    if(glyph[row*5+col]!='1')continue;
                    float x=start-(character*6+col)*pixel, y=center.y+(3-row)*pixel;
                    mesh.Quad(new Vector3(x-pixel*.47f,y-pixel*.47f,center.z),new Vector3(x+pixel*.47f,y-pixel*.47f,center.z),
                        new Vector3(x+pixel*.47f,y+pixel*.47f,center.z),new Vector3(x-pixel*.47f,y+pixel*.47f,center.z),color);
                }
            }
        }

        private static string Glyph(char value)
        {
            const string blank="00000/00000/00000/00000/00000/00000/00000";
            var glyphs=new Dictionary<char,string>
            {
                ['А']="01110/10001/10001/11111/10001/10001/10001", ['Б']="11111/10000/10000/11110/10001/10001/11110",
                ['В']="11110/10001/10001/11110/10001/10001/11110", ['Г']="11111/10000/10000/10000/10000/10000/10000",
                ['Д']="00110/01010/01010/01010/11111/10001/10001", ['Е']="11111/10000/10000/11110/10000/10000/11111",
                ['Ж']="10101/10101/01110/00100/01110/10101/10101", ['З']="11110/00001/00001/01110/00001/00001/11110",
                ['И']="10001/10011/10101/10101/11001/10001/10001", ['Й']="01010/00100/10001/10011/10101/11001/10001",
                ['К']="10001/10010/10100/11000/10100/10010/10001", ['Л']="00111/01001/01001/01001/01001/01001/10001",
                ['М']="10001/11011/10101/10101/10001/10001/10001", ['Н']="10001/10001/10001/11111/10001/10001/10001",
                ['О']="01110/10001/10001/10001/10001/10001/01110", ['П']="11111/10001/10001/10001/10001/10001/10001",
                ['Р']="11110/10001/10001/11110/10000/10000/10000", ['С']="01111/10000/10000/10000/10000/10000/01111",
                ['Т']="11111/00100/00100/00100/00100/00100/00100", ['У']="10001/10001/01010/00100/00100/01000/10000",
                ['Ф']="00100/01110/10101/10101/01110/00100/00100", ['Ц']="10010/10010/10010/10010/10010/11111/00001",
                ['Ч']="10001/10001/10001/01111/00001/00001/00001", ['Ш']="10101/10101/10101/10101/10101/10101/11111",
                ['Ы']="10001/10001/10001/11101/10011/10011/11101", ['Ь']="10000/10000/10000/11110/10001/10001/11110",
                ['Я']="01111/10001/10001/01111/00101/01001/10001", ['P']="11110/10001/10001/11110/10000/10000/10000",
                ['S']="01111/10000/10000/01110/00001/00001/11110", ['T']="11111/00100/00100/00100/00100/00100/00100",
                ['O']="01110/10001/10001/10001/10001/10001/01110"
            };
            return (glyphs.TryGetValue(value,out var glyph)?glyph:blank).Replace("/","");
        }
    }
}
