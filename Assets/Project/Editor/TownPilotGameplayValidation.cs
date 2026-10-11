using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.City;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks the real pilot's collision shapes, public passages and native-sized driving pickup.</summary>
internal static class TownPilotGameplayValidation
{
    private const string ReportFolder = "Library/TownWorldQA";
    private static readonly Rect PilotPlot = new Rect(-210, -186, 84, 36);
    private static readonly float[] WalkingOffsets = { -.83f, -.55f, -.28f, 0, .28f, .55f, .83f };
    private const float EasternPathX = -125.5f;

    /// <summary>
    /// Runs only in the isolated editor's Play Mode. Static probes test walking and parking clearance;
    /// the southern street is then traversed using PickupDrive's real FixedUpdate physics.
    /// Restores the test vehicle/camera state on both success and failure, without saving the scene.
    /// </summary>
    internal static IEnumerator Check(CityWorld city)
    {
        TownWorldSafety.RequireIsolatedEditor();
        Require(Application.isPlaying && city != null && city.pickup != null && city.pickup.Body != null,
            "The pilot gameplay check requires the live City scene and its initialized pickup.");
        Require(city.town != null && city.chaseCamera != null && city.chaseCamera.target == city.pickup.transform,
            "The pilot lost its authored town or real driving-camera references.");
        Require(Mathf.Abs(Time.timeScale - 1) < .001f, "Pilot driving acceptance requires normal, unpaused game time.");

        var pickup = city.pickup;
        var chase = city.chaseCamera;
        var body = pickup.Body;
        var savedPosition = body.position;
        var savedRotation = body.rotation;
        var savedVelocity = body.velocity;
        var savedAngularVelocity = body.angularVelocity;
        bool savedKeyboard = pickup.ReadKeyboard, savedInputEnabled = pickup.InputEnabled, savedMouse = chase.ReadMouse;
        var savedCameraPosition = chase.transform.position;
        var savedCameraRotation = chase.transform.rotation;
        float savedYaw = chase.Yaw, savedPitch = chase.Pitch, savedDistance = chase.distance;
        var log = new StringBuilder();
        bool passed = false;
        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(ReportFolder + "/pilot-gameplay.txt", "RUNNING\n");
        try
        {
            pickup.ReadKeyboard = false;
            pickup.InputEnabled = true;
            chase.ReadMouse = false;
            pickup.Stop();
            Physics.SyncTransforms();

            log.AppendLine("Checking five solid building bodies in the pilot plot.");
            CheckBuildingBodies(city);
            log.AppendLine("PASS five live, non-overlapping solid building bodies.");

            log.AppendLine("Checking the full 2.2 metre central/west/east pedestrian passages.");
            CheckWalkingRoute(city, new Vector3(-208, 0, -168), new Vector3(EasternPathX, 0, -168), "central garden path");
            CheckWalkingRoute(city, new Vector3(-208, 0, -186.5f), new Vector3(-208, 0, -149.5f), "western sidewalk");
            CheckWalkingRoute(city, new Vector3(EasternPathX, 0, -186.5f), new Vector3(EasternPathX, 0, -149.5f), "eastern sidewalk");
            log.AppendLine("PASS continuous walking clearance, with 0.5 m longitudinal sampling and overlapping cross-width probes.");

            var visualSize = PickupNativeSize(pickup);
            Require(Mathf.Abs(Mathf.Max(visualSize.x, visualSize.z) - 4.4f) < .05f,
                "Pilot clearance must be tested with the existing 4.4 metre pickup, not a reduced vehicle.");
            var vehicleEnvelope = new Vector3(Mathf.Max(1.25f, visualSize.x / 2 + .1f), .65f,
                Mathf.Max(2.3f, visualSize.z / 2 + .1f));
            log.AppendLine("Checking the parking entry and six-metre manoeuvring aisle, not the occupied bays.");
            CheckVehicleRoute(city, new Vector3(-133, 0, -193), new Vector3(-133, 0, -183), vehicleEnvelope, "parking entry");
            CheckVehicleRoute(city, new Vector3(-136, 0, -183), new Vector3(-129, 0, -183), vehicleEnvelope, "reverse manoeuvring aisle");
            for (int angle = 0; angle < 360; angle += 45)
                RequireClear(city, new Vector3(-133, 1.1f, -183), vehicleEnvelope, Quaternion.Euler(0, angle, 0), "parking turn envelope");
            log.AppendLine("PASS native pickup entry/aisle/turn envelopes; occupied resident parking remains intact.");

            log.AppendLine("Driving the real pickup east along the existing southern street.");
            pickup.Stop();
            body.position = new Vector3(-201, .15f, -192);
            body.rotation = Quaternion.Euler(0, 90, 0);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            pickup.SetInput(.8f, 0, false);
            float deadline = Time.realtimeSinceStartup + 8;
            float progressTime = Time.realtimeSinceStartup;
            float progressX = body.position.x;
            while (body.position.x < -150 && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                Require(Mathf.Abs(body.position.z + 192) < 1.5f,
                    "Pilot street driving deviated from the road: " + body.position);
                Require(Mathf.Abs(Mathf.DeltaAngle(body.rotation.eulerAngles.y, 90)) < 5,
                    "The pickup unexpectedly rotated during straight pilot street driving.");
                if (body.position.x >= progressX + .2f)
                {
                    progressX = body.position.x;
                    progressTime = Time.realtimeSinceStartup;
                }
                Require(Time.realtimeSinceStartup - progressTime < 1.5f,
                    "The enlarged pickup became stuck beside the pilot at " + body.position + ".");
            }
            Require(body.position.x >= -150 && Mathf.Abs(body.position.z + 192) < 1.5f,
                "The enlarged pickup did not complete the 51 metre pilot street within eight seconds.");
            pickup.Stop();
            log.AppendLine("PASS real straight driving: at least 51 m, no stuck progress, no unintended rotation, lateral deviation below 1.5 m.");

            chase.ResetBehindVehicle();
            // LateUpdate must have time to settle the real spring camera after the test teleport.
            for (int frame = 0; frame < 24; frame++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            CaptureDrivingCamera(chase.GetComponent<Camera>());
            log.AppendLine("PASS actual PickupCamera view captured at 1280 x 800 (post-pilot only; not a paired Before image).");
            passed = true;
        }
        finally
        {
            pickup.Stop();
            body.position = savedPosition;
            body.rotation = savedRotation;
            body.velocity = savedVelocity;
            body.angularVelocity = savedAngularVelocity;
            pickup.InputEnabled = savedInputEnabled;
            pickup.ReadKeyboard = savedKeyboard;
            chase.Orbit(Mathf.DeltaAngle(chase.Yaw, savedYaw), savedPitch - chase.Pitch);
            chase.distance = savedDistance;
            chase.ReadMouse = savedMouse;
            chase.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            Physics.SyncTransforms();
            File.WriteAllText(ReportFolder + "/pilot-gameplay.txt", (passed ? "PASS\n" : "FAILED\n") + log);
        }
    }

    private static void CheckBuildingBodies(CityWorld city)
    {
        var bodies = city.town.GetComponentsInChildren<BoxCollider>(true)
            .Where(collider => collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger
                && collider.bounds.size.x >= 3 && collider.bounds.size.z >= 3 && collider.bounds.size.y >= 2.5f
                && PilotPlot.Contains(new Vector2(collider.bounds.center.x, collider.bounds.center.z))).ToArray();
        Require(bodies.Length == 5, "The accepted pilot must contain five live building bodies; found " + bodies.Length + ".");
        foreach (var collider in bodies)
        {
            Require(InsidePlot(collider.bounds.min) && InsidePlot(collider.bounds.max),
                "Pilot building collision volume leaves its original plot: " + collider.name);
            Require(Physics.OverlapBox(collider.bounds.center, Vector3.one * .05f, Quaternion.identity, ~0,
                    QueryTriggerInteraction.Ignore).Contains(collider), "Pilot building is absent from active physics: " + collider.name);
        }
        for (int i = 0; i < bodies.Length; i++)
            for (int j = i + 1; j < bodies.Length; j++)
                Require(!bodies[i].bounds.Intersects(bodies[j].bounds), "Pilot building collision bodies overlap: " + bodies[i].name + " / " + bodies[j].name);
    }

    private static bool InsidePlot(Vector3 position) => position.x >= PilotPlot.xMin && position.x <= PilotPlot.xMax
        && position.z >= PilotPlot.yMin && position.z <= PilotPlot.yMax;

    private static void CheckWalkingRoute(CityWorld city, Vector3 from, Vector3 to, string purpose)
    {
        var side = Vector3.Cross(Vector3.up, (to - from).normalized);
        int samples = Mathf.CeilToInt(Vector3.Distance(from, to) / .5f);
        for (int step = 0; step <= samples; step++)
            foreach (float offset in WalkingOffsets)
                RequireClear(city, Vector3.Lerp(from, to, (float)step / samples) + side * offset + Vector3.up,
                    new Vector3(.27f, .7f, .27f), Quaternion.identity, purpose);
    }

    private static void CheckVehicleRoute(CityWorld city, Vector3 from, Vector3 to, Vector3 envelope, string purpose)
    {
        int samples = Mathf.CeilToInt(Vector3.Distance(from, to) / .5f);
        var rotation = Quaternion.LookRotation(to - from);
        for (int step = 0; step <= samples; step++)
            RequireClear(city, Vector3.Lerp(from, to, (float)step / samples) + Vector3.up * 1.1f, envelope, rotation, purpose);
    }

    private static void RequireClear(CityWorld city, Vector3 position, Vector3 halfExtents, Quaternion rotation, string purpose)
    {
        var obstacles = Physics.OverlapBox(position, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore)
            .Where(collider => collider.gameObject.scene == city.gameObject.scene
                && !collider.transform.IsChildOf(city.pickup.transform)).ToArray();
        Require(obstacles.Length == 0, "Pilot " + purpose + " is obstructed at " + position + " by "
            + string.Join(", ", obstacles.Take(6).Select(collider => collider.name + " [" + collider.GetType().Name + "]")));
    }

    private static Vector3 PickupNativeSize(PickupDrive pickup)
    {
        var filters = pickup.GetComponentsInChildren<MeshFilter>().Where(filter => filter.sharedMesh != null).ToArray();
        Require(filters.Length != 0, "The real driving pickup lost its visible model.");
        var bounds = new Bounds();
        bool initialized = false;
        foreach (var filter in filters)
        {
            var source = filter.sharedMesh.bounds;
            var pose = pickup.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                var position = pose.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? source.min.x : source.max.x,
                    (corner & 2) == 0 ? source.min.y : source.max.y, (corner & 4) == 0 ? source.min.z : source.max.z));
                if (!initialized) { bounds = new Bounds(position, Vector3.zero); initialized = true; }
                else bounds.Encapsulate(position);
            }
        }
        var scale = pickup.transform.lossyScale;
        return Vector3.Scale(bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
    }

    private static void CaptureDrivingCamera(Camera camera)
    {
        Require(camera != null && camera.isActiveAndEnabled, "The native driving camera is inactive.");
        var texture = new RenderTexture(1280, 800, 24);
        var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        var savedTarget = camera.targetTexture;
        var savedActive = RenderTexture.active;
        try
        {
            Require(texture.Create(), "Cannot create the pilot driving review render texture.");
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            image.Apply();
            File.WriteAllBytes(ReportFolder + "/PilotDrivingCamera.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = savedTarget;
            RenderTexture.active = savedActive;
            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
