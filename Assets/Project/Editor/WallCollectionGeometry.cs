using System.Collections.Generic;
using RetailEmpireTycoon.BuildSystem;
using UnityEngine;

internal sealed class WallCollectionGeometry
{
    private readonly int style;
    private readonly float height;
    private readonly List<Vector3> points=new();
    private readonly List<Vector2> uv=new(),projection=new();
    private readonly List<int> indices=new();
    private WallCollectionGeometry(int style,float height){this.style=style;this.height=height;}

    internal static (Mesh solid,Mesh glass) Create(int style,Vector3 size)
    {
        float w=size.x,h=size.y,d=size.z;
        int spans=Mathf.RoundToInt(w/ShopProportions.CellSize);
        var solid=new WallCollectionGeometry(style,h);
        var glass=new WallCollectionGeometry(style,h);
        void Slab(float bottom,float top,float depth,int swatch=-1) =>
            solid.Profile(-w/2,w/2,bottom,top,0,depth,swatch);
        void Beam(float x,float y,float width,float rise,float depth,int swatch) =>
            solid.Profile(x-width/2,x+width/2,y-rise/2,y+rise/2,0,depth,swatch);
        void Pane(float bottom,float top,int swatch) =>
            glass.Quad(new Vector3(-w/2,bottom,0),new Vector3(w/2,bottom,0),
                new Vector3(w/2,top,0),new Vector3(-w/2,top,0),Vector3.forward,swatch);

        switch(style)
        {
            case 1:
                solid.BandedBody(w,0,h,d-.014f,d,h*.95f,h,7);
                break;
            case 2: case 3:
                solid.BandedBody(w,0,h,d-.018f,d,h*.345f,h*.368f,style==2?3:4);
                break;
            case 4: case 5: case 20:
                float ribTop=style==20?h-.045f:h;
                Slab(0,ribTop,d-.042f);
                int ribs=2*spans;
                for(int i=0;i<ribs;i++)
                {
                    float pitch=w/ribs;
                    float x=-w*.5f+(i+.5f)*pitch, ribWidth=pitch*(style==4?.80f:.56f);
                    float top=style==20?ribTop-.001f:ribTop;
                    solid.Profile(x-ribWidth/2,x+ribWidth/2,0,top,d/2-.028f,.056f,-1);
                    solid.Profile(x-ribWidth/2,x+ribWidth/2,0,top,-d/2+.028f,.056f,-1);
                }
                if(style==20)Slab(ribTop,h,d,9);
                break;
            case 15:
                solid.StoneShell(w,d);
                break;
            case 16: case 17:
                solid.BoardBody(w,d,style==16?.18f:.32f);
                break;
            case 24: case 25: case 26:
                int frame=style==25?4:1;
                float sill=style==26?h*.36f:.065f;
                if(style==26)Slab(0,sill,d-.018f);
                float frameDepth=spans==2?d:.15f;
                if(style==26) Beam(0,sill,w,.05f,frameDepth,frame);
                else Beam(0,(sill+.025f)*.5f,w,sill+.025f,frameDepth,frame);
                Beam(0,h-.028f,w,.056f,frameDepth,frame);
                if(style==25)
                { Pane(sill,h*.43f,11);Pane(h*.43f,h*.63f,12);Pane(h*.63f,h-.025f,11); }
                else Pane(sill,h-.025f,11);
                break;
            case 27:
                Beam(0,.012f,w,.024f,Mathf.Min(.29f,d),14);
                Beam(0,h-.014f,w,.028f,Mathf.Min(.29f,d),14);
                Pane(.016f,h-.016f,-1);
                break;
            case 28:
                float timberDepth=spans==2?d:.17f;
                Beam(0,.044f,w,.088f,timberDepth,5);
                Beam(0,h-.044f,w,.088f,timberDepth,5);
                int slats=2*spans;
                for(int i=0;i<slats;i++) Beam(((i+.5f)/slats-.5f)*w,h/2,w/slats*.54f,h-.12f,.12f,6);
                break;
            case 29:
                float low=h*.43f, rail=h*.71f;
                solid.BandedBody(w,0,low+.017f,d-.018f,d,low-.025f,low+.017f,3);
                Beam(0,rail,w,.039f,.065f,10);
                Pane(low,rail,11);
                break;
            default:
                Slab(0,h,d);
                break;
        }
        return (solid.Build("Opaque module"),glass.points.Count>0?glass.Build("Glazing"):null);
    }

    internal static bool HasJambs(int style) => style==24||style==25||style==26||style==29;

    internal static Mesh OpeningTrim(string name,float center,float width,float bottom,float top,float depth,int swatch)
    {
        var shape=new WallCollectionGeometry(0,top);
        shape.Profile(center-width*.5f,center+width*.5f,bottom,top,0,depth,swatch);
        return shape.Build(name);
    }

    internal static Mesh StripeCorner(float armLength,float height,float depth)
    {
        var shape=new WallCollectionGeometry(0,height);
        const float bevel=.008f;
        float half=depth*.5f;
        var levels=new[]{(0f,half-bevel),(bevel,half),(height-bevel,half),(height,half-bevel)};
        Vector3[] Outline(float y,float d) => new Vector3[]{new(-d,y,-d),new(armLength,y,-d),
            new(armLength,y,d),new(d,y,d),new(d,y,armLength),new(-d,y,armLength)};
        for(int i=1;i<levels.Length;i++)
        {
            var lower=Outline(levels[i-1].Item1,levels[i-1].Item2);
            var upper=Outline(levels[i].Item1,levels[i].Item2);
            for(int edge=0;edge<lower.Length;edge++)
            {
                int next=(edge+1)%lower.Length;
                var direction=lower[next]-lower[edge];
                shape.Quad(lower[edge],lower[next],upper[next],upper[edge],new(direction.z,0,-direction.x),-1);
            }
        }
        Cap(0,Vector3.down);
        Cap(height,Vector3.up);
        return shape.Build("Thin corner");

        void Cap(float y,Vector3 normal)
        {
            float d=half-bevel;
            shape.Quad(new(-d,y,-d),new(armLength,y,-d),new(armLength,y,d),new(-d,y,d),normal,-1);
            shape.Quad(new(-d,y,d),new(d,y,d),new(d,y,armLength),new(-d,y,armLength),normal,-1);
        }
    }

    internal static Mesh CreateJamb(int style,Vector3 size)
    {
        float top=style==29?size.y*.71f:size.y;
        float bottom=style==29?size.y*.43f:style==26?size.y*.36f:.065f;
        var jamb=new WallCollectionGeometry(style,size.y);
        float depth=size.x>ShopProportions.CellSize*1.5f?size.z:.13f;
        jamb.Profile(-.012f,.012f,bottom,top,0,style==29?.038f:depth,style==29?10:style==25?4:1);
        return jamb.Build("Frame jamb");
    }

    // End planes stay on the grid. The small chamfer follows the long edges,
    // leaving a full-height contact face between neighbouring solid modules.
    private void Profile(float left,float right,float bottom,float top,float middle,float depth,int swatch)
    {
        float z0=middle-depth/2,z1=middle+depth/2;
        float bevel=Mathf.Min(.008f,Mathf.Min(depth*.18f,(top-bottom)*.2f));
        var section=new[]{
            new Vector2(bottom,z0+bevel),new Vector2(bottom,z1-bevel),
            new Vector2(bottom+bevel,z1),new Vector2(top-bevel,z1),
            new Vector2(top,z1-bevel),new Vector2(top,z0+bevel),
            new Vector2(top-bevel,z0),new Vector2(bottom+bevel,z0)
        };
        Vector3 At(float x,int i)=>new(x,section[i].x,section[i].y);
        for(int i=0;i<section.Length;i++)
        {
            int n=(i+1)%section.Length;
            var a=At(left,i);var b=At(left,n);
            var normal=new Vector3(0,a.z-b.z,b.y-a.y);
            Quad(a,At(right,i),At(right,n),b,normal,swatch);
        }
        for(int i=1;i<section.Length-1;i++)
        {
            Triangle(At(left,0),At(left,i),At(left,i+1),Vector3.left,swatch);
            Triangle(At(right,0),At(right,i),At(right,i+1),Vector3.right,swatch);
        }
    }

    // A continuous cross-section keeps the top and trims closed at the exact grid edge.
    private void BandedBody(float width,float bottom,float top,float bodyDepth,float trimDepth,
        float trimStart,float trimEnd,int trimSwatch)
    {
        const float bevel=.004f;
        float body=bodyDepth*.5f,trim=trimDepth*.5f;
        var levels=new List<Vector2>{new(bottom,body-bevel),new(bottom+bevel,body),
            new(trimStart,body),new(trimStart+bevel,trim),new(trimEnd-bevel,trim)};
        if(trimEnd<top)
        {
            levels.Add(new(trimEnd,body));
            levels.Add(new(top-bevel,body));
            levels.Add(new(top,body-bevel));
        }
        else levels.Add(new(top,trim-bevel));
        ExtrudeBody(width,levels,y=>y>=trimStart && y<=trimEnd?trimSwatch:-1);
    }

    private void BoardBody(float width,float depth,float course)
    {
        float face=depth*.5f;
        var levels=new List<Vector2>{new(0,face-.008f)};
        for(float y=0;y<height-.018f;y+=course)
        {
            levels.Add(new(y+.006f,face));
            levels.Add(new(Mathf.Min(y+.043f,height-.008f),face-.005f));
            if(y+course<height-.018f) levels.Add(new(y+course,face-.0165f));
        }
        levels.Add(new(height,face-.010f));
        ExtrudeBody(width,levels);
    }

    private void ExtrudeBody(float width,List<Vector2> levels,System.Func<float,int> swatchAt=null)
    {
        float left=-width*.5f,right=width*.5f;
        for(int i=1;i<levels.Count;i++)
        {
            var a=levels[i-1];var b=levels[i];
            int swatch=swatchAt?.Invoke((a.x+b.x)*.5f)??-1;
            for(int side=-1;side<=1;side+=2)
                Quad(new(left,a.x,a.y*side),new(right,a.x,a.y*side),new(right,b.x,b.y*side),
                    new(left,b.x,b.y*side),new(0,a.y-b.y,(b.x-a.x)*side),swatch);
            for(int side=-1;side<=1;side+=2)
            {
                float x=right*side;
                Quad(new(x,a.x,-a.y),new(x,a.x,a.y),new(x,b.x,b.y),new(x,b.x,-b.y),Vector3.right*side,swatch);
            }
        }
        Cap(levels[0],Vector3.down);
        Cap(levels[levels.Count-1],Vector3.up);

        void Cap(Vector2 end,Vector3 normal) => Quad(new(left,end.x,-end.y),new(right,end.x,-end.y),
            new(right,end.x,end.y),new(left,end.x,end.y),normal,swatchAt?.Invoke(end.x)??-1);
    }

    private void StoneShell(float width,float depth)
    {
        const int columns=4,rows=14;
        Vector3 Point(int x,int y,int side)
        {
            float u=x/(float)columns,v=y/(float)rows;
            float wave=Mathf.Sin(v*7*Mathf.PI);
            // Use exactly the same profile at both ends, including the floating-point endpoint of sin(pi).
            float arch=x==0 || x==columns?0:Mathf.Sin(u*Mathf.PI);
            float relief=(.35f+.65f*arch)*wave*wave;
            return new((u-.5f)*width,v*height,side*(depth*.5f-.019f+.019f*relief));
        }
        for(int side=-1;side<=1;side+=2)
        for(int y=0;y<rows;y++)
        for(int x=0;x<columns;x++)
            Quad(Point(x,y,side),Point(x+1,y,side),Point(x+1,y+1,side),Point(x,y+1,side),Vector3.forward*side,-1);
        for(int y=0;y<rows;y++)
        {
            Quad(Point(0,y,-1),Point(0,y,1),Point(0,y+1,1),Point(0,y+1,-1),Vector3.left,-1);
            Quad(Point(columns,y,-1),Point(columns,y,1),Point(columns,y+1,1),Point(columns,y+1,-1),Vector3.right,-1);
        }
        for(int x=0;x<columns;x++)
        {
            Quad(Point(x,0,-1),Point(x+1,0,-1),Point(x+1,0,1),Point(x,0,1),Vector3.down,-1);
            Quad(Point(x,rows,-1),Point(x+1,rows,-1),Point(x+1,rows,1),Point(x,rows,1),Vector3.up,-1);
        }
    }

    private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,int swatch)
    {
        int first=points.Count;
        bool forward=Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>0;
        Vertex(a,normal,swatch);Vertex(b,normal,swatch);Vertex(c,normal,swatch);Vertex(d,normal,swatch);
        indices.Add(first);indices.Add(first+(forward?1:2));indices.Add(first+(forward?2:1));
        indices.Add(first);indices.Add(first+(forward?2:3));indices.Add(first+(forward?3:2));
    }
    private void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 normal,int swatch)
    {
        int first=points.Count;
        if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0)(b,c)=(c,b);
        Vertex(a,normal,swatch);Vertex(b,normal,swatch);Vertex(c,normal,swatch);
        indices.Add(first);indices.Add(first+1);indices.Add(first+2);
    }
    private void Vertex(Vector3 point,Vector3 normal,int swatch)
    {
        points.Add(point);
        bool end=Mathf.Abs(normal.x)>.1f;
        projection.Add(swatch<0?new Vector2(end?point.z:point.x,end?2:1):Vector2.zero);
        uv.Add(swatch<0?WallCollectionFinishes.Pattern(style,point.y,height):WallCollectionFinishes.Swatch(swatch));
    }
    private Mesh Build(string name)
    {
        var mesh=new Mesh{name=name};mesh.SetVertices(points);mesh.SetUVs(0,uv);mesh.SetUVs(1,projection);
        mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
}
