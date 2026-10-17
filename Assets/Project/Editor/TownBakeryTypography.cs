using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Freezes the existing licensed Roboto font into static sign meshes; no runtime font or UI component.</summary>
internal sealed class TownBakeryTypography
{
    private const int FontSize=128;
    private readonly Dictionary<char,CharacterInfo> characters=new();
    private readonly Material white;
    private readonly Material black;
    public TownBakeryTypography()
    {
        var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Plugins/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf");
        if(font==null)throw new InvalidOperationException("The project's existing licensed Roboto Bold font is missing.");
        const string requested="BakeryBREAD";
        font.RequestCharactersInTexture(requested,FontSize,FontStyle.Normal);
        foreach(char c in requested)
        {
            if(!font.GetCharacterInfo(c,out var glyph,FontSize,FontStyle.Normal))throw new InvalidOperationException("The source font cannot render character "+c);
            characters[c]=glyph;
        }
        var source=font.material.mainTexture;
        var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false){name="BakeryFrozenFont",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        try{Graphics.Blit(source,target);RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);texture.Apply();}
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        string path=TownBakeryPlacement.Root+"/Materials/BakeryFrozenFont.asset";TownBakeryPlacement.SaveAsset(texture,path);
        var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        white=Ink(atlas,"BakeryIvoryLettering",new Color(.98f,.94f,.85f));
        black=Ink(atlas,"BakeryDarkLettering",new Color(.09f,.10f,.085f));
    }
    private static Material Ink(Texture2D atlas,string name,Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Bakery Lettering");if(shader==null)throw new InvalidOperationException("Bakery lettering shader did not import.");
        var ink=new Material(shader){name=name,enableInstancing=true};ink.SetTexture("_BaseMap",atlas);ink.SetColor("_BaseColor",color);
        string path=TownBakeryPlacement.Root+"/Materials/"+name+".mat";TownBakeryPlacement.SaveAsset(ink,path);return AssetDatabase.LoadAssetAtPath<Material>(path);
    }
    public void Attach(GameObject prototype)
    {
        if(prototype.name=="BakeryHouse")
        {
            Sign(prototype.transform,"BakeryName","Bakery",new Vector3(0,3.34f,6.135f),5.05f,.61f,0,false);
        }
        else if(prototype.name=="MenuBoard")Sign(prototype.transform,"MenuTitle","BREAD",new Vector3(0,.95f,.265f),.65f,.11f,0,false);
    }
    private void Sign(Transform parent,string id,string value,Vector3 position,float width,float height,float yaw,bool dark)
    {
        var glyphs=new List<CharacterInfo>();float advance=0;float minY=float.MaxValue,maxY=float.MinValue;
        foreach(char c in value)
        {
            // Asset imports may clear Unity's dynamic Font cache. Geometry must use the same
            // frozen UV snapshot as the saved atlas, never query that mutable cache again.
            if(!characters.TryGetValue(c,out var glyph))throw new InvalidOperationException("Missing frozen glyph "+c);
            glyphs.Add(glyph);advance+=glyph.advance;minY=Mathf.Min(minY,glyph.minY);maxY=Mathf.Max(maxY,glyph.maxY);
        }
        float scale=Mathf.Min(width/advance,height/(maxY-minY));float start=advance*scale/2,centerY=(minY+maxY)/2;
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();var normals=new List<Vector3>();
        foreach(var glyph in glyphs)
        {
            float left=start-glyph.maxX*scale,right=start-glyph.minX*scale;
            float bottom=(glyph.minY-centerY)*scale,top=(glyph.maxY-centerY)*scale;int index=vertices.Count;
            vertices.Add(new Vector3(left,bottom,0));vertices.Add(new Vector3(right,bottom,0));vertices.Add(new Vector3(right,top,0));vertices.Add(new Vector3(left,top,0));
            uv.Add(glyph.uvBottomRight);uv.Add(glyph.uvBottomLeft);uv.Add(glyph.uvTopLeft);uv.Add(glyph.uvTopRight);
            for(int n=0;n<4;n++)normals.Add(Vector3.forward);
            triangles.AddRange(new[]{index,index+1,index+2,index,index+2,index+3});start-=glyph.advance*scale;
        }
        var mesh=new Mesh{name=id};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        string path=TownBakeryPlacement.Root+"/Meshes/"+id+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
        var sign=TownBakeryPlacement.Child(parent,id,position);sign.localRotation=Quaternion.Euler(0,yaw,0);sign.gameObject.isStatic=true;
        sign.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);sign.gameObject.AddComponent<MeshRenderer>().sharedMaterial=dark?black:white;
        sign.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
