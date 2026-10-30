using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StaffAccessDiagnostics
{
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(StaffUpdateBatch.Report);Directory.CreateDirectory("Docs/StaffUpdate");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();System.GC.Collect();
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);var driver=Object.FindObjectOfType<DriverDelivery>(true);
        Physics.SyncTransforms();var trace=new System.Text.StringBuilder();
        foreach(var point in driver.town.suppliers.Where(p=>p.kind==CheckpointKind.Depot))
        {
            var native=driver.ToNative(point.transform.position);trace.AppendLine(point.supplier+" native="+native+" radius="+point.radius);
            if(point.supplier!=SupplierKind.Bakery && point.supplier!=SupplierKind.Farmers)continue;
            foreach(var collider in Physics.OverlapBox(point.transform.position+Vector3.up,Vector3.one*7,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                trace.AppendLine(collider.name+" "+collider.GetType().Name+" "+collider.bounds);
            TownFloristBatch.Render(scene,"Docs/StaffUpdate/"+point.supplier+"Access.png",point.transform.position+new Vector3(0,19,-21),point.transform.position);
            if(point.supplier==SupplierKind.Farmers)
            {
                var world=driver.traffic.transform.TransformPoint(new Vector3(550,.02f,275));
                foreach(var hit in Physics.BoxCastAll(world+Vector3.up*.45f,new Vector3(1.9f*.42f,.35f,.08f)*driver.traffic.transform.lossyScale.x,Vector3.back,Quaternion.Euler(0,180,0),1.2f,~0,QueryTriggerInteraction.Ignore))trace.AppendLine("EXIT BLOCKER "+hit.collider.name+" at "+driver.ToNative(hit.point)+" normal "+hit.normal);
            }
        }
        File.WriteAllText(StaffUpdateBatch.Report+"/access-diagnostic.txt",trace.ToString());EditorApplication.Exit(0);
    }
}
