using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AutoWorksDriveCheck
{
    private const string Key = "AutoWorks.LiveDrive";
    private const float Facade = -167;
    private const float ParkZ = -160.2f;
    private static string Report => AutoWorksAuthoring.Report + (first == 12 ? "/CityTurn.txt" : first == 6 ? "/ForwardDrive.txt" : "/Drive.txt");
    private static readonly float[] Centers = {134, 144, 154};
    private static readonly List<string> messages = new();
    private static PickupDrive car;
    private static BoxCollider chassis;
    private static GarageDoor[] doors;
    private static Transform facility;
    private static Rect[] equipment;
    private static List<AutoWorksTurnPlan.Pose> turn;
    private static int first, index, turnIndex, turnGear, shifts;
    private static float rear, wheelbase, speedAtOpening, minimumGap, maxWheelSteer, due, clearedAt;
    private static Vector3 lastTurnAt;
    private static AutoWorksTurnPlan.Pose turnStart;
    private static bool fullyCrossed, beganOpening, touchedObstacle, holdInside, exitForward, laneApproach, bendFromRoad;
    private static DriveContactProbe collisionProbe;
    private static DateTime deadline;
    private static string vehicle;
    private static Phase phase;
    private enum Phase { Prepare, Settle, Approach, Enter, Park, Turn, Exit, Closed }

    static AutoWorksDriveCheck() => EditorApplication.update += Tick;

    public static void Run() => Begin(0);

    public static void ForwardOnly() => Begin(6);

    public static void RoadOnly() => Begin(12);

    private static void Begin(int startTrial)
    {
        TownWorldSafety.RequireIsolatedEditor();
        first = startTrial;
        Directory.CreateDirectory(AutoWorksAuthoring.Report);
        File.WriteAllText(Report, "RUNNING\n");
        EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        SessionState.SetInt(Key + ".First", startTrial);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || Time.frameCount < 16) return;
        try
        {
            if (car == null) SetUp();
            if (index == (first == 12 ? 14 : 12))
            {
                Finish(null);
                return;
            }
            Step();
        }
        catch (Exception error) { Finish(error); }
    }

    private static void SetUp()
    {
        first = index = SessionState.GetInt(Key + ".First", 0);
        Application.logMessageReceived += CaptureMessage;
        var city = Object.FindObjectOfType<CityWorld>();
        car = city.pickup;
        chassis = car.GetComponent<BoxCollider>();
        facility = city.town.transform.Find(TownSpecialPlan.WorkshopRoot + "/AutoWorks");
        doors = facility.GetComponentsInChildren<GarageDoor>().OrderBy(d => d.name).ToArray();
        Require(doors.Length == 3, "Installed doors were not found.");
        car.ReadKeyboard = false;
        car.InputEnabled = true;
        city.enabled = false;
        foreach (var traffic in Object.FindObjectsOfType<TownRoadTraffic>()) traffic.enabled = false;
        foreach (var traffic in Object.FindObjectsOfType<TrafficVehicle>()) traffic.gameObject.SetActive(false);
        Physics.SyncTransforms();
        equipment = facility.GetComponentsInChildren<BoxCollider>()
            .Where(c => c.transform.IsChildOf(facility.Find("Props/Workshop wall equipment")) ||
                c.name == "Steel column collider")
            .Select(c => Rect.MinMaxRect(c.bounds.min.x, c.bounds.min.z, c.bounds.max.x, c.bounds.max.z)).ToArray();
        collisionProbe = car.gameObject.AddComponent<DriveContactProbe>();
        collisionProbe.Facility = facility;
        var lighting = facility.GetComponent<AutoWorksLighting>();
        var lights = facility.GetComponentsInChildren<Light>();
        var emission = facility.GetComponentsInChildren<MeshRenderer>()
            .Where(r => r.sharedMaterial != null && r.sharedMaterial.name == "AW Lamp").ToArray();
        var properties = new MaterialPropertyBlock();
        lighting.SetLights(false);
        Require(lights.Length > 0 && lights.All(light => !light.enabled) && emission.All(renderer =>
        {
            renderer.GetPropertyBlock(properties);
            return properties.GetColor("_EmissionColor") == Color.black;
        }), "Workshop lighting did not turn off.");
        lighting.SetLights(true);
        Require(emission.Length > 0 && lights.All(light => light.enabled) && emission.All(renderer =>
        {
            renderer.GetPropertyBlock(properties);
            return properties.GetColor("_EmissionColor").maxColorComponent > 0;
        }), "Workshop lighting did not turn on.");
        File.AppendAllText(Report, "PASS: the real lights and emissive surfaces switch off and on.\n");
        // Run through the normal WheelCollider controller, at fixed-size steps.
        Time.timeScale = 5;
    }

    private static void Step()
    {
        int bay = index >= 12 ? 1 : index % 3;
        float center = Centers[bay];
        var door = doors[bay];
        if (phase == Phase.Prepare) { Prepare(bay, center); return; }
        var position = car.Body.position;
        var bounds = chassis.bounds;
        maxWheelSteer = Mathf.Max(maxWheelSteer, Mathf.Abs(car.SteeringAngle));
        Require(DateTime.UtcNow < deadline, $"{vehicle} bay {bay+1} {phase}: timeout at {position:F3}, yaw {car.transform.eulerAngles.y:F2}, speed {car.Speed:F2}, path {turnIndex}/{turn?.Count}.");
        Require(position.y < .49f && position.y > -.16f, $"{vehicle} lost the intended drive surface at {position:F3}.");
        if (collisionProbe.UnsafeContact != null) touchedObstacle = true;
        Require(!touchedObstacle, $"{vehicle} bay {bay+1} hit {collisionProbe.UnsafeContact} during {phase} at {position:F3}.");

        bool crossing = bounds.max.z >= Facade + .06f && bounds.min.z <= Facade + .40f &&
            phase is Phase.Enter or Phase.Exit;
        if (crossing)
        {
            Require(door.IsFullyOpen && !door.BlocksPassage, "Entered the opening before its panels were clear.");
            float gap = Mathf.Min(bounds.min.x - (center - AutoWorksModel.Width / 2),
                center + AutoWorksModel.Width / 2 - bounds.max.x);
            minimumGap = Mathf.Min(gap, minimumGap);
            Require(gap > .05f && bounds.max.y < .09f + GarageDoor.ClearHeight,
                $"{vehicle} exceeded the portal envelope at {position:F3}, gap {gap:F2}.");
            fullyCrossed = true;
        }

        switch (phase)
        {
            case Phase.Settle:
                if (Time.fixedTime < due) return;
                Require(door.IsFullyClosed && door.BlocksPassage, "Door should be closed with the car outside its sensors.");
                if (bendFromRoad) PlanRoadBend(center);
                else phase = Phase.Approach;
                break;
            case Phase.Approach:
                RecordOpening(door);
                if (door.IsFullyOpen)
                {
                    phase = Phase.Enter;
                    break;
                }
                if (bounds.max.z > Facade - 1.6f) { car.Stop(); return; }
                Line(center, 0, 1, 1.45f);
                break;
            case Phase.Enter:
                if (position.z < ParkZ) { Line(center, 0, 1, position.z < ParkZ - 1.8f ? 2.1f : .83f); return; }
                car.Stop();
                due = Time.fixedTime + 3.10f;
                phase = Phase.Park;
                break;
            case Phase.Park:
                if (Time.fixedTime < due) return;
                Require(beganOpening && fullyCrossed && car.Speed < .14f && car.GroundedWheels >= 4 &&
                    bounds.min.z > Facade + .3f && bounds.max.z < -153.5f, "Vehicle did not stop wholly inside on its wheels.");
                Require(door.IsFullyOpen && !door.BlocksPassage, "Door tried to close while the vehicle was inside.");
                holdInside = true;
                if (exitForward) PlanTurn(center);
                else phase = Phase.Exit;
                break;
            case Phase.Turn:
                if (bendFromRoad) RecordOpening(door);
                if (!FollowTurn()) return;
                if (bendFromRoad)
                {
                    Require(Vector3.Dot(car.transform.forward, Vector3.forward) > .967f, "Road turn did not align with the central opening.");
                    phase = Phase.Approach;
                    break;
                }
                Require(Vector3.Dot(car.transform.forward, Vector3.back) > .975f, "Turn did not align the vehicle with its exit.");
                phase = Phase.Exit;
                break;
            case Phase.Exit:
                if (bounds.max.z > -175.50f)
                {
                    Line(center, exitForward ? 180 : 0, exitForward ? 1 : -1, 1.60f);
                    return;
                }
                car.Stop();
                Require(door.IsFullyOpen && !door.BlocksPassage, "Door closed immediately instead of delaying.");
                clearedAt = Time.fixedTime;
                due = clearedAt + 5.2f;
                phase = Phase.Closed;
                break;
            case Phase.Closed:
                if (Time.fixedTime < clearedAt + 1.8f)
                    Require(door.IsFullyOpen, "Door started closing before the hold delay.");
                if (Time.fixedTime < due) return;
                Require(door.IsFullyClosed && door.BlocksPassage && holdInside, "Door failed to close after the car cleared the area.");
                Require(maxWheelSteer >= 3f, "No physical steering was exercised.");
                if (exitForward) Require(shifts > 0, "A real in-hall turn was not executed.");
                File.AppendAllText(Report, FormattableString.Invariant(
                    $"PASS {vehicle} / bay {bay+1} / exit {(exitForward ? "forward" : "reverse")} / {(bendFromRoad ? "steered from westbound lane across retained ramp" : laneApproach ? "city road and lowered crossing" : "yard")} / {shifts} direction changes / steering {maxWheelSteer:F1} deg / minimum side clearance {minimumGap:F2} m / opened approaching at {speedAtOpening:F2} m/s / held while occupied and closed on departure.\n"));
                index++;
                phase = Phase.Prepare;
                break;
        }
    }

    private static void Prepare(int bay, float center)
    {
        bendFromRoad = index >= 12;
        bool truck = bendFromRoad ? index == 13 : index % 6 >= 3;
        exitForward = index >= 6 && !bendFromRoad;
        laneApproach = bay == 1;
        var spec = truck ? AssetDatabase.LoadAssetAtPath<PlayerVehicleDefinition>("Assets/Resources/PlayerVehicles/heavy_box_truck.asset") : null;
        car.SetVehicle(spec);
        vehicle = spec == null ? "Delivery pickup" : spec.displayName;
        var wheels = car.wheels.Select(w => (w.front, z: car.transform.InverseTransformPoint(w.contact.transform.position).z)).ToArray();
        rear = wheels.Where(w => !w.front).Average(w => w.z);
        wheelbase = wheels.Where(w => w.front).Average(w => w.z) - rear;
        float startYaw = bay == 2 ? -6.5f : 6.5f;
        float startZ = laneApproach ? -194.2f : -180.2f;
        car.Stop();
        car.Body.position = new Vector3(center + Mathf.Sign(startYaw) * .44f, .25f, startZ);
        car.Body.rotation = Quaternion.Euler(0, startYaw, 0);
        if (bendFromRoad)
        {
            car.Body.position = new Vector3(157.7f, .22f, -191.8f);
            car.Body.rotation = Quaternion.Euler(0, -90, 0);
        }
        Physics.SyncTransforms();
        collisionProbe.UnsafeContact = null;
        beganOpening = fullyCrossed = holdInside = touchedObstacle = false;
        minimumGap = float.PositiveInfinity;
        maxWheelSteer = 0;
        shifts = 0;
        turn = null;
        turnIndex = 0;
        turnGear = 0;
        due = Time.fixedTime + .58f;
        deadline = DateTime.UtcNow.AddSeconds(75);
        phase = Phase.Settle;
    }

    private static void Line(float x, float yaw, int gear, float speed)
    {
        float direction = yaw == 0 ? 1 : -1;
        float headingError = Mathf.DeltaAngle(car.transform.eulerAngles.y, yaw);
        float offset = (x - car.Body.position.x) * direction;
        float steer = Mathf.Clamp((headingError * .035f + offset * .39f * gear) * gear, -.55f, .55f);
        Drive(gear, speed, steer);
    }

    private static void Drive(int gear, float speed, float steer) =>
        car.SetInput(gear * speed / (gear > 0 ? car.topSpeed : car.reverseSpeed), steer, false);

    private static AutoWorksTurnPlan.Pose Actual()
    {
        var p = car.transform.TransformPoint(new Vector3(0, 0, rear));
        return new AutoWorksTurnPlan.Pose(new Vector2(p.x, p.z), car.transform.eulerAngles.y * Mathf.Deg2Rad);
    }

    private static void RecordOpening(GarageDoor door)
    {
        if (door.IsFullyClosed || beganOpening) return;
        beganOpening = true;
        speedAtOpening = car.Speed;
        Require(chassis.bounds.max.z < Facade, "Door failed to react before the vehicle reached the opening.");
        if (index == 0)
            Require(doors.Where(other => other != door).All(other => other.IsFullyClosed), "Another bay opened during an isolated approach.");
    }

    private static void PlanRoadBend(float center)
    {
        turnStart = Actual();
        var parking = facility.Find("Fence");
        var barriers = parking.GetComponentsInChildren<BoxCollider>()
            .Select(c => Rect.MinMaxRect(c.bounds.min.x, c.bounds.min.z, c.bounds.max.x, c.bounds.max.z)).ToArray();
        var goal = new AutoWorksTurnPlan.Pose(new Vector2(center, -178.7f + rear), 0);
        var planner = new AutoWorksTurnPlan(Rect.MinMaxRect(123f, -199.6f, 164.5f, -172f),
            barriers, chassis.center.z - rear, chassis.size.x, chassis.size.z, wheelbase / Mathf.Tan(35f * Mathf.Deg2Rad), .40f);
        turn = planner.Find(turnStart, goal);
        phase = Phase.Turn;
        deadline = DateTime.UtcNow.AddSeconds(95);
        File.AppendAllText(Report, $"{vehicle}: planned {turn.Count} steps from the westbound city lane.\n");
    }
    private static void PlanTurn(float center)
    {
        var start = turnStart = Actual();
        var goal = new AutoWorksTurnPlan.Pose(new Vector2(center, ParkZ - rear), Mathf.PI);
        var planner = new AutoWorksTurnPlan(Rect.MinMaxRect(126.82f, -166.67f, 161.18f, -153.32f),
            equipment, chassis.center.z - rear, chassis.size.x, chassis.size.z, wheelbase / Mathf.Tan(35f * Mathf.Deg2Rad), wheelbase < 4 ? .68f : .48f);
        turn = planner.Find(start, goal);
        lastTurnAt = car.Body.position;
        phase = Phase.Turn;
        deadline = DateTime.UtcNow.AddSeconds(100);
        File.AppendAllText(Report, $"{vehicle} bay {index % 3 + 1}: planned {turn.Count} small path steps for the live steering test.\n");
    }

    private static bool FollowTurn()
    {
        var actual = Actual();
        var target = turn[turnIndex];
        int gear = target.Gear;
        int end = turnIndex;
        while (end + 1 < turn.Count && turn[end + 1].Gear == gear) end++;
        float remaining = Vector2.Distance(actual.Rear, turn[end].Rear);
        if (turnGear != gear)
        {
            if (turnGear != 0) shifts++;
            File.AppendAllText(Report, $"  Shift {shifts}: {actual.Rear} yaw {actual.Yaw * Mathf.Rad2Deg:F1}, gear {gear}." + Environment.NewLine);
            turnGear = gear;
        }
        float projection;
        AutoWorksTurnPlan.Pose before;
        Vector2 segment;
        while (true)
        {
            target = turn[turnIndex];
            before = turnIndex == 0 ? turnStart : turn[turnIndex - 1];
            segment = target.Rear - before.Rear;
            projection = Vector2.Dot(actual.Rear - before.Rear, segment) / segment.sqrMagnitude;
            if (turnIndex >= end || projection < .98f) break;
            turnIndex++;
        }
        // Use the end plane as well as the point. A small sideways error must never make the
        // driver continue past a gear change just because it missed the exact target point.
        if (turnIndex == end && (projection >= .985f || remaining < .14f))
        {
            car.Stop();
            File.AppendAllText(Report, $"  Segment finished {actual.Rear}, target {target.Rear}, error {remaining:F2} m.\n");
            if (end == turn.Count - 1)
            {
                Require(bendFromRoad || Vector3.Distance(lastTurnAt, car.Body.position) < 10, "Turn finished outside the intended bay area.");
                return true;
            }
            turnIndex = end + 1;
            return false;
        }
        var expected = before.Rear + segment * Mathf.Clamp01(projection);
        float yaw = before.Yaw + AutoWorksTurnPlan.Delta(before.Yaw, target.Yaw) * Mathf.Clamp01(projection);
        var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
        float crossTrack = Vector2.Dot(expected - actual.Rear, right);
        float yawError = AutoWorksTurnPlan.Delta(actual.Yaw, yaw);
        float angle = Mathf.Atan(target.Curvature * wheelbase) + gear * yawError * 1.5f + crossTrack * .65f;
        float steering = Mathf.Clamp(angle * Mathf.Rad2Deg / 40f, -.99f, .99f);
        Drive(gear, remaining < .48f && turnIndex >= end - 1 ? .40f : .68f, steering);
        return false;
    }

    private static void CaptureMessage(string message, string stack, LogType type)
    {
        if (type is not (LogType.Error or LogType.Exception or LogType.Warning or LogType.Assert)) return;
        if ((message + stack).Contains("AutoWorks") || (message + stack).Contains("GarageDoor"))
            messages.Add(type + ": " + message);
    }

    private static void Require(bool ok, string message) => AutoWorksAuthoring.Require(ok, message);

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CaptureMessage;
        int completed = index - first;
        string status = error == null && messages.Count == 0
            ? $"PASS: {completed} physical drives; door state and occupancy verified. No Auto Works errors or warnings."
            : "FAILED: " + error + "\n" + string.Join("\n", messages);
        File.AppendAllText(Report, status + "\n");
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null && messages.Count == 0 ? 0 : 1);
    }
}

public sealed class DriveContactProbe : MonoBehaviour
{
    internal Transform Facility;
    internal string UnsafeContact;
    private void OnCollisionEnter(Collision collision)
    {
        var other = collision.collider;
        if (!other.transform.IsChildOf(Facility) || other.transform.IsChildOf(Facility.Find("Yard"))) return;
        UnsafeContact = other.name;
    }
}






