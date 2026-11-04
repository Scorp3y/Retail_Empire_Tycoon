using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Searches a conservative vehicle envelope. The result is only a steering target for the live drive test.</summary>
internal sealed class AutoWorksTurnPlan
{
    internal readonly struct Pose
    {
        internal readonly Vector2 Rear;
        internal readonly float Yaw;
        internal readonly int Gear;
        internal readonly float Curvature;
        internal Vector2 Forward => new(Mathf.Sin(Yaw), Mathf.Cos(Yaw));

        internal Pose(Vector2 rear, float yaw, int gear = 0, float curvature = 0)
        { Rear = rear; Yaw = yaw; Gear = gear; Curvature = curvature; }
    }

    private readonly struct Node
    {
        internal readonly Pose Pose;
        internal readonly float Cost;
        internal readonly int Previous;
        internal Node(Pose pose, float cost, int previous) { Pose = pose; Cost = cost; Previous = previous; }
    }

    private const float Step = .41f;
    private static readonly int[] Gears = {1, -1};
    private static readonly float[] SteeringSamples = {-1f, -.57f, 0f, .57f, 1f};
    private readonly Rect space;
    private readonly Rect[] obstacles;
    private readonly float rearToCenter, halfWidth, halfLength, radius;

    internal AutoWorksTurnPlan(Rect space, Rect[] obstacles, float rearToCenter, float width, float length, float radius, float margin)
    {
        this.space = space;
        this.obstacles = obstacles;
        this.rearToCenter = rearToCenter;
        halfWidth = width / 2 + margin;
        halfLength = length / 2 + margin;
        this.radius = radius;
    }

    internal List<Pose> Find(Pose start, Pose goal)
    {
        var found = new List<Node> {new(start, 0, -1)};
        var frontier = new SortedSet<(float Score, int Index)> { (0, 0) };
        var best = new Dictionary<(int, int, int, int), float>();
        while (frontier.Count > 0 && found.Count < 320000)
        {
            var pick = frontier.Min;
            frontier.Remove(pick);
            var node = found[pick.Index];
            var current = node.Pose;
            if (Vector2.Distance(current.Rear, goal.Rear) < .48f && Mathf.Abs(Delta(current.Yaw, goal.Yaw)) < .11f)
            {
                var result = new List<Pose>();
                for (int index = pick.Index; index > 0; index = found[index].Previous) result.Add(found[index].Pose);
                result.Reverse();
                return result;
            }
            foreach (int gear in Gears)
                foreach (float steering in SteeringSamples)
                {
                    float curvature = steering / radius;
                    var next = Advance(current, curvature, gear, Step);
                    if (!Clear(next) || !Clear(Advance(current, curvature, gear, Step * .5f))) continue;
                    float cost = node.Cost + Step + .038f * Mathf.Abs(steering) +
                        (current.Gear != 0 && gear != current.Gear ? 2.85f : 0f);
                    var key = (Mathf.RoundToInt(next.Rear.x / .27f), Mathf.RoundToInt(next.Rear.y / .27f),
                        Mathf.RoundToInt(next.Yaw / .09817477f), next.Gear);
                    if (best.TryGetValue(key, out float previous) && previous <= cost) continue;
                    best[key] = cost;
                    float heuristic = Mathf.Max(Vector2.Distance(next.Rear, goal.Rear),
                        Mathf.Abs(Delta(next.Yaw, goal.Yaw)) * radius);
                    frontier.Add((cost + heuristic * 1.62f, found.Count));
                    found.Add(new Node(next, cost, pick.Index));
                }
        }
        throw new InvalidOperationException($"No padded turn route from {start.Rear} to {goal.Rear} (radius {radius:F2} m, {found.Count} candidates).");
    }

    private static Pose Advance(Pose from, float curvature, int gear, float step)
    {
        float length = step * gear;
        float yaw = from.Yaw + length * curvature;
        var move = Mathf.Abs(curvature) < .0001f ? from.Forward * length :
            new Vector2((Mathf.Cos(from.Yaw) - Mathf.Cos(yaw)) / curvature,
                (Mathf.Sin(yaw) - Mathf.Sin(from.Yaw)) / curvature);
        return new Pose(from.Rear + move, Mathf.Repeat(yaw + Mathf.PI, Mathf.PI * 2) - Mathf.PI, gear, curvature);
    }

    private bool Clear(Pose pose)
    {
        var forward = pose.Forward;
        var right = new Vector2(forward.y, -forward.x);
        var center = pose.Rear + forward * rearToCenter;
        var extents = new Vector2(Mathf.Abs(forward.x) * halfLength + Mathf.Abs(right.x) * halfWidth,
            Mathf.Abs(forward.y) * halfLength + Mathf.Abs(right.y) * halfWidth);
        if (!space.Contains(center - extents) || !space.Contains(center + extents)) return false;
        foreach (var obstacle in obstacles)
        {
            var d = obstacle.center - center;
            var e = obstacle.size * .5f;
            if (Mathf.Abs(d.x) >= e.x + extents.x || Mathf.Abs(d.y) >= e.y + extents.y) continue;
            if (Mathf.Abs(Vector2.Dot(d, forward)) > halfLength + e.x * Mathf.Abs(forward.x) + e.y * Mathf.Abs(forward.y)) continue;
            if (Mathf.Abs(Vector2.Dot(d, right)) > halfWidth + e.x * Mathf.Abs(right.x) + e.y * Mathf.Abs(right.y)) continue;
            return false;
        }
        return true;
    }

    internal static float Delta(float from, float to) => Mathf.DeltaAngle(from * Mathf.Rad2Deg, to * Mathf.Rad2Deg) * Mathf.Deg2Rad;
}


