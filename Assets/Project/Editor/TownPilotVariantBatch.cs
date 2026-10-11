using System;
using UnityEditor;
using UnityEngine;

/// <summary>Builds the four-body comparison in the isolated editor, never in the working project.</summary>
public static class TownPilotVariantBatch
{
    public static void RunA()
    {
        try
        {
            TownWorldBatch.GenerateAndValidate(TownhousePilotVariant.FourBuildings,true);
            TownPilotReview.CaptureVariantA();
        }
        catch(Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
