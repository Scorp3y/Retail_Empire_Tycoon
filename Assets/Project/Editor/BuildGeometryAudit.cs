using System.IO;
using System.Text;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEngine;

public static class BuildGeometryAudit
{
    [MenuItem("Retail Empire/Building/Audit model geometry")]
    public static void Run()
    {
        var report = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:BuildItemData"))
        {
            var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item.prefab == null) continue;
            report.AppendLine(item.name + " footprint=" + item.footprint + " rootScale=" + item.prefab.transform.localScale);
            foreach (var filter in item.prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var matrix = Matrix4x4.Scale(item.prefab.transform.localScale) * item.prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var bounds = filter.sharedMesh.bounds;
                report.AppendLine("  " + filter.name + " mesh=" + filter.sharedMesh.name + " center=" + matrix.MultiplyPoint3x4(bounds.center).ToString("F4") + " size=" + matrix.MultiplyVector(bounds.size).ToString("F4"));
            }
        }
        foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            var bounds = renderer.bounds;
            if (bounds.size.y < 0.25f && bounds.size.x > 1f && bounds.size.z > 1f)
                report.AppendLine("FLOOR " + renderer.name + " min=" + bounds.min.ToString("F4") + " max=" + bounds.max.ToString("F4"));
            if (bounds.min.x < 2f && bounds.max.x > -2f && bounds.min.z < 2f && bounds.max.z > -2f && bounds.size.x > 1f)
                report.AppendLine("SHOP SURFACE " + renderer.name + " min=" + bounds.min.ToString("F4") + " max=" + bounds.max.ToString("F4"));
            if (renderer.name == "Model")
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    report.AppendLine("MODEL LOW LEVELS " + string.Join(", ", filter.sharedMesh.vertices.Select(v => filter.transform.TransformPoint(v).y).Where(y => y < 0.3f).Select(y => y.ToString("F4")).Distinct()));
                    Vector3[] vertices = filter.sharedMesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
                    int[] triangles = filter.sharedMesh.triangles;
                    var levels = new System.Collections.Generic.Dictionary<string, float>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        float height = (a.y + b.y + c.y) / 3f;
                        if (height > 0.3f || normal.normalized.y < 0.99f) continue;
                        string key = height.ToString("F4");
                        levels.TryGetValue(key, out float area);
                        levels[key] = area + normal.magnitude * 0.5f;
                    }
                    report.AppendLine("FLOOR FACE AREAS " + string.Join(", ", levels.Select(p => p.Key + "=" + p.Value.ToString("F4"))));
                }
                var hits = Physics.RaycastAll(new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z), Vector3.down, bounds.size.y + 2f);
                foreach (var hit in hits) report.AppendLine("CENTER HIT " + hit.collider.name + " y=" + hit.point.y);
            }
        }
        foreach (var terrain in Object.FindObjectsOfType<Terrain>())
            report.AppendLine("Terrain height at origin=" + (terrain.SampleHeight(Vector3.zero) + terrain.transform.position.y));
        Directory.CreateDirectory("Library/CameraBuildQA");
        File.WriteAllText("Library/CameraBuildQA/geometry.txt", report.ToString());
        Debug.Log("Build geometry audit written.");
    }
}
