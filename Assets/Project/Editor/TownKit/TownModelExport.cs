using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal static class TownModelExport
    {
        private static string Number(float value)=>value.ToString("0.######",CultureInfo.InvariantCulture);

        public static void Obj(Mesh mesh,string path)
        {
            var text=new StringBuilder("# Retail Empire Tycoon TownKit; metres; ground-centred pivot\nmtllib ../TownKit.mtl\no "+mesh.name+"\n");
            foreach(var vertex in mesh.vertices)text.AppendLine($"v {Number(-vertex.x)} {Number(vertex.y)} {Number(vertex.z)}");
            foreach(var uv in mesh.uv)text.AppendLine($"vt {Number(uv.x)} {Number(uv.y)}");
            foreach(var normal in mesh.normals)text.AppendLine($"vn {Number(-normal.x)} {Number(normal.y)} {Number(normal.z)}");
            text.AppendLine("usemtl TownPalette");
            var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                int a=indices[i]+1,b=indices[i+2]+1,c=indices[i+1]+1;
                text.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
            }
            File.WriteAllText(path,text.ToString(),new UTF8Encoding(false));
        }

        public static void Material(string path)
        {
            File.WriteAllText(path,"newmtl TownPalette\nKa 1 1 1\nKd 1 1 1\nKs 0 0 0\nd 1\nillum 1\nmap_Kd ../Materials/TownPalette.png\n");
        }
    }
}
