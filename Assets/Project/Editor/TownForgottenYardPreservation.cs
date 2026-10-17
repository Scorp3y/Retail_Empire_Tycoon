using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>
/// This update replaces one complete subtree, so saved component records and their asset bytes
/// provide a stricter audit than a quantized triangle comparison, without lazy shader defaults.
/// </summary>
internal sealed class TownForgottenYardPreservation
{
    private readonly Dictionary<string,string> records;
    private readonly Dictionary<string,string> dependencies;
    internal TownForgottenYardPreservation()
    {
        var ignored=OwnedRecordIds();
        records=Records().Where(p=>!ignored.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value);
        dependencies=AssetDatabase.GetDependencies(TownWorldAssembler.PrefabPath,true)
            .Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&p!=TownWorldAssembler.PrefabPath&&p!=TownForgottenYardAuthoring.LayoutPath&&!p.StartsWith(TownForgottenYardAuthoring.Root+"/",StringComparison.Ordinal))
            .ToDictionary(p=>p,TownForgottenYardBatch.Hash);
    }
    internal void RequireUnchanged()
    {
        var after=Records();
        foreach(var p in records)
            TownForgottenYardBatch.Require(after.TryGetValue(p.Key,out var text)&&text==p.Value,"Protected saved town record changed: "+p.Key);
        var owned=OwnedRecordIds();
        foreach(var id in after.Keys)
            TownForgottenYardBatch.Require(records.ContainsKey(id)||owned.Contains(id),"Unexpected town object outside scenery: "+id);
        foreach(var p in dependencies)
            TownForgottenYardBatch.Require(File.Exists(p.Key)&&TownForgottenYardBatch.Hash(p.Key)==p.Value,"Protected town resource changed: "+p.Key);
        File.WriteAllText(TownForgottenYardBatch.Report+"/protection.txt",$"PASS: {records.Count} saved town records outside Future store landscapes are byte-for-byte unchanged; {dependencies.Count} mesh/material/texture/script and other resource files unchanged by SHA256. Only the town root child list and the owned scenery subtree may change.\n");
    }
    private static Dictionary<string,string> Records()=>Regex.Matches(File.ReadAllText(TownWorldAssembler.PrefabPath),@"(?ms)^--- !u!\d+ &(\d+)\r?\n.*?(?=^--- !u!|\z)")
        .Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value.Replace("\r\n","\n"));
    private static HashSet<string> OwnedRecordIds()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownWorldAssembler.PrefabPath);var ids=new HashSet<string>();
        void Add(Object obj)
        {
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string _,out long id))throw new InvalidOperationException("Cannot resolve persistent prefab record: "+obj.name);
            ids.Add(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Add(prefab.transform);var scenery=prefab.GetComponentInChildren<TownExpansionScenery>(true);
        TownForgottenYardBatch.Require(scenery!=null,"Missing owned scenery subtree.");
        foreach(var t in scenery.GetComponentsInChildren<Transform>(true))
        {
            Add(t.gameObject);foreach(var c in t.GetComponents<Component>())Add(c);
        }
        return ids;
    }
}
