using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Freezes licensed font glyphs into static model lettering, with no runtime text components.</summary>
internal sealed class TownStaticTypography
{
    private const int FontSize=128;
    private readonly string root;
    private readonly Dictionary<char,CharacterInfo> characters=new();
    private readonly Material ink;
    public TownStaticTypography(string root,string text)
    {
        this.root=root;
        var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Plugins/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf");
        if(font==null)throw new InvalidOperationException("Licensed model font missing.");
        font.RequestCharactersInTexture(text,FontSize,FontStyle.Normal);
        foreach(char c in text)
        {if(!font.GetCharacterInfo(c,out var glyph,FontSize,FontStyle.Normal))throw new InvalidOperationException("Missing model glyph "+c);characters[c]=glyph;}
        var source=font.material.mainTexture;var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        var atlas=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false){name="FloristFrozenFont",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        try{Graphics.Blit(source,target);RenderTexture.active=target;atlas.ReadPixels(new Rect(0,0,source.width,source.height),0,0);atlas.Apply();}
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        string path=root+"/Materials/LetteringAtlas.asset";TownBakeryPlacement.SaveAsset(atlas,path);
        var shader=Shader.Find("Universal Render Pipeline/Bakery Lettering");if(shader==null)throw new InvalidOperationException("Static lettering shader missing.");
        var material=new Material(shader){name="FloristIvoryLettering",enableInstancing=true};
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));material.SetColor("_BaseColor",new Color(.98f,.94f,.85f));
        path=root+"/Materials/Lettering.mat";TownBakeryPlacement.SaveAsset(material,path);ink=AssetDatabase.LoadAssetAtPath<Material>(path);
    }
    public void Add(Transform parent,string id,string text,Vector3 position,float width,float height,float yaw=0)
    {
        var glyphs=new List<CharacterInfo>();float advance=0,minY=float.MaxValue,maxY=float.MinValue;
        foreach(char c in text)
        {
            if(!characters.TryGetValue(c,out var glyph))throw new InvalidOperationException("Unfrozen glyph "+c);
            glyphs.Add(glyph);advance+=glyph.advance;
            if(glyph.glyphWidth>0){minY=Mathf.Min(minY,glyph.minY);maxY=Mathf.Max(maxY,glyph.maxY);}
        }
        float scale=Mathf.Min(width/advance,height/(maxY-minY)),start=advance*scale/2,centerY=(minY+maxY)/2;
        var points=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
        foreach(var glyph in glyphs)
        {
            if(glyph.glyphWidth>0)
            {
                float left=start-glyph.maxX*scale,right=start-glyph.minX*scale,bottom=(glyph.minY-centerY)*scale,top=(glyph.maxY-centerY)*scale;int index=points.Count;
                points.Add(new Vector3(left,bottom,0));points.Add(new Vector3(right,bottom,0));points.Add(new Vector3(right,top,0));points.Add(new Vector3(left,top,0));
                uv.Add(glyph.uvBottomRight);uv.Add(glyph.uvBottomLeft);uv.Add(glyph.uvTopLeft);uv.Add(glyph.uvTopRight);
                for(int i=0;i<4;i++)normals.Add(Vector3.forward);indices.AddRange(new[]{index,index+1,index+2,index,index+2,index+3});
            }
            start-=glyph.advance*scale;
        }
        var mesh=new Mesh{name=id};mesh.SetVertices(points);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        string path=root+"/Meshes/"+id+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
        var sign=TownBakeryPlacement.Child(parent,id,position);sign.localRotation=Quaternion.Euler(0,yaw,0);sign.gameObject.isStatic=true;
        sign.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var renderer=sign.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=ink;renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
}
