#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace RetailEmpireTycoon.City
{
    /// <summary>Opt-in development-player measurement. Never starts in normal gameplay.</summary>
    public sealed class TownPerformanceProbe : MonoBehaviour
    {
        private const int Width = 1920, Height = 1080;
        private string output;
        private Camera viewCamera;
        private RenderTexture image;
        private readonly List<Counter> counters = new List<Counter>();
        private readonly FrameTiming[] timings = new FrameTiming[4];
        private readonly List<ViewResult> results = new List<ViewResult>();
        private bool finished;
        private int errors;
        private long cameraRenderEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-town-profile-out");
            if (index < 0) return;
            if (index + 1 >= args.Length) { Application.Quit(2); return; }
            // Evidence may only be written beneath this disposable player build.
            string path = Path.GetFullPath(args[index + 1]);
            string build = Path.GetFullPath(Path.Combine(Application.dataPath, "..")) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(build, StringComparison.OrdinalIgnoreCase)) { Application.Quit(2); return; }
            var root = new GameObject("Disposable town performance probe");
            DontDestroyOnLoad(root);
            var probe = root.AddComponent<TownPerformanceProbe>();
            probe.output = path;
        }

        private void Start()
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "complete.txt"), "RUNNING\n");
            Application.logMessageReceived += OnLog;
            StartCoroutine(Guard(Run()));
        }

        private IEnumerator Run()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.timeScale = 1;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A real graphics device is required; -nographics is forbidden.");
            for (int i = 0; i < 30; i++) yield return null;
            if (FindObjectOfType<SaveManager>(true) != null)
                throw new InvalidOperationException("SaveManager must be absent from the disposable fixture.");
            var trip = FindObjectOfType<CityTrip>();
            if (trip == null) throw new InvalidOperationException("The actual shop trip is missing.");
            trip.Begin();
            float deadline = Time.realtimeSinceStartup + 90;
            while ((!trip.InCity || trip.Transitioning) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!trip.InCity || trip.Transitioning) throw new TimeoutException("Actual shop-to-city trip failed.");
            var city = FindObjectOfType<CityWorld>();
            if (city == null || city.town == null) throw new InvalidOperationException("Live town is missing.");
            viewCamera = Camera.main;
            if (viewCamera == null) throw new InvalidOperationException("Live player camera is missing.");
            var pickup = FindObjectOfType<PickupDrive>();
            var chase = viewCamera.GetComponent<PickupCamera>();
            if (pickup == null || chase == null) throw new InvalidOperationException("Actual pickup/camera is missing.");
            pickup.ReadKeyboard = false; chase.ReadMouse = false; pickup.Stop();
            image = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            image.Create(); viewCamera.targetTexture = image;
            viewCamera.aspect = (float)Width / Height;
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            OpenCounters();
            WriteEnvironment();

            // First measure the shipped vehicle camera, including its physics query and City HUD.
            yield return Measure("VehicleCamera", pickup, true);
            chase.enabled = false;
            yield return FixedView("ResidentialStreet", new Vector3(-168, 8, -204), new Vector3(-168, 4, -164), 0, pickup);
            yield return FixedView("ResidentialCourt", new Vector3(-168, 95, -168), new Vector3(-168, 0, -168), 29, pickup);
            yield return FixedView("Workshop", new Vector3(147, 49, -219), new Vector3(120, 1, -168), 0, pickup);
            yield return FixedView("Riverside", new Vector3(292, 90, -118), new Vector3(219, 3, -18), 0, pickup);
            yield return FixedView("Office", new Vector3(284, 47, 118), new Vector3(219, 2, 168), 0, pickup);
            yield return FixedView("CityOverview", new Vector3(580, 930, -770), new Vector3(175, 0, 135), 470, pickup);

            viewCamera.targetTexture = null;
            chase.enabled = true;
            trip.Return(); deadline = Time.realtimeSinceStartup + 90;
            while ((trip.InCity || trip.Transitioning) && Time.realtimeSinceStartup < deadline) yield return null;
            if (trip.InCity || trip.Transitioning) throw new TimeoutException("Actual city-to-shop trip failed.");
            if (errors != 0) throw new InvalidOperationException("Player logged " + errors + " errors during measurement.");
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonUtility.ToJson(new Report { views = results.ToArray() }, true));
            Finish(0, "PASS actual shop departure/return; seven normal-render-loop offscreen views. Not an interactive presentation benchmark.\n");
        }

        private IEnumerator FixedView(string name, Vector3 position, Vector3 focus, float orthographicSize, PickupDrive pickup)
        {
            viewCamera.transform.position = position;
            viewCamera.transform.LookAt(focus);
            viewCamera.orthographic = orthographicSize > 0;
            viewCamera.orthographicSize = orthographicSize;
            viewCamera.fieldOfView = 65; viewCamera.nearClipPlane = .1f; viewCamera.farClipPlane = 1600;
            yield return Measure(name, pickup, false);
        }

        private IEnumerator Measure(string name, PickupDrive pickup, bool vehicleCamera)
        {
            var samples = new Sample[12000];
            for (int i = 0; i < samples.Length; i++) samples[i].values = new long[counters.Count];
            float until = Time.realtimeSinceStartup + 3;
            while (Time.realtimeSinceStartup < until) yield return null;
            int count = 0;
            int renderedUpdateFrames = 0;
            long previousRenderEvents = cameraRenderEvents, initialRenderEvents = cameraRenderEvents;
            double previous = Time.realtimeSinceStartupAsDouble;
            double end = previous + 8;
            ulong lastTiming = 0;
            Vector3 start = pickup.transform.position;
            while (Time.realtimeSinceStartupAsDouble < end && count < samples.Length)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                if (cameraRenderEvents != previousRenderEvents) renderedUpdateFrames++;
                previousRenderEvents = cameraRenderEvents;
                var sample = new Sample { wallMs = (now - previous) * 1000, gpuMs = -1, cpuMs = -1, mainMs = -1, renderMs = -1, presentMs = -1 };
                previous = now;
                uint available = FrameTimingManager.GetLatestTimings(4, timings);
                // GPU timings arrive late. Do not repeatedly count an old timing as a new frame.
                for (int i = 0; i < available; i++)
                {
                    var timing = timings[i];
                    if (timing.frameStartTimestamp <= lastTiming || timing.gpuFrameTime <= 0) continue;
                    lastTiming = timing.frameStartTimestamp;
                    sample.gpuMs = timing.gpuFrameTime; sample.cpuMs = timing.cpuFrameTime;
                    sample.mainMs = timing.cpuMainThreadFrameTime; sample.renderMs = timing.cpuRenderThreadFrameTime;
                    sample.presentMs = timing.cpuMainThreadPresentWaitTime;
                    break;
                }
                sample.values = samples[count].values;
                for (int i = 0; i < counters.Count; i++) sample.values[i] = counters[i].Read();
                samples[count++] = sample;
            }
            if (count < 30) throw new InvalidOperationException("Insufficient native samples for " + name);
            var result = Summarize(name, samples, count);
            result.renderEvents = cameraRenderEvents - initialRenderEvents;
            result.renderedUpdateFrames = renderedUpdateFrames;
            result.renderCoverage = (double)renderedUpdateFrames / count;
            result.position = viewCamera.transform.position; result.rotation = viewCamera.transform.eulerAngles;
            result.orthographic = viewCamera.orthographic; result.fieldOfView = viewCamera.fieldOfView; result.orthographicSize = viewCamera.orthographicSize;
            result.vehicleCamera = vehicleCamera; result.pickupDisplacement = Vector3.Distance(start, pickup.transform.position);
            results.Add(result);
            WriteSamples(name, samples, count);
            var previousTarget = RenderTexture.active;
            try
            {
                RenderTexture.active = image;
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                try { texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply(); File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); }
                finally { Destroy(texture); }
            }
            finally { RenderTexture.active = previousTarget; }
            File.WriteAllText(Path.Combine(output, "progress.txt"), name + " " + count + " frames\n");
        }

        private void OpenCounters()
        {
            var available = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(available);
            var catalog = new StringBuilder();
            foreach (var handle in available)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                catalog.AppendLine(description.Category + " | " + description.Name + " | " + description.UnitType);
            }
            File.WriteAllText(Path.Combine(output, "counter-catalog.txt"), catalog.ToString());
            foreach (string name in new[] { "Draw Calls Count", "Batches Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "System Used Memory", "Total Reserved Memory", "GC Reserved Memory", "GC Allocated In Frame", "Main Thread", "CPU Total Frame Time", "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "GPU Frame Time" })
            {
                ProfilerRecorderHandle found = default;
                foreach (var handle in available) if (ProfilerRecorderHandle.GetDescription(handle).Name == name) { found = handle; break; }
                if (found.Valid) counters.Add(new Counter(name, ProfilerRecorder.StartNew(ProfilerRecorderHandle.GetDescription(found).Category, name, 1)));
                else counters.Add(new Counter(name, default));
            }
        }

        private void WriteEnvironment()
        {
            File.WriteAllText(Path.Combine(output, "environment.json"), JsonUtility.ToJson(new EnvironmentReport
            {
                unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                api = SystemInfo.graphicsDeviceType.ToString(), driver = SystemInfo.graphicsDeviceVersion,
                memoryMb = SystemInfo.systemMemorySize, gpuMemoryMb = SystemInfo.graphicsMemorySize,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], pipeline = GraphicsSettings.currentRenderPipeline == null ? "none" : GraphicsSettings.currentRenderPipeline.name,
                screenWidth = Screen.width, screenHeight = Screen.height, targetWidth = Width, targetHeight = Height,
                batchMode = Application.isBatchMode, editor = Application.isEditor, development = Debug.isDebugBuild, vsync = QualitySettings.vSyncCount,
                frameCap = Application.targetFrameRate, mode = "Normal render loop to 1920x1080 RenderTexture; City HUD remains on backbuffer. No interactive/present FPS claim."
            }, true));
        }

        private ViewResult Summarize(string name, Sample[] samples, int count)
        {
            var wall = new double[count]; double total = 0, gpu = 0, cpu = 0, main = 0, render = 0, present = 0; int validTimings = 0;
            var sums = new double[counters.Count]; var valid = new int[counters.Count];
            for (int i = 0; i < count; i++)
            {
                var s = samples[i]; total += s.wallMs; wall[i] = s.wallMs;
                if (s.gpuMs > 0) { validTimings++; gpu += s.gpuMs; cpu += s.cpuMs; main += s.mainMs; render += s.renderMs; present += s.presentMs; }
                for (int j = 0; j < counters.Count; j++) if (s.values[j] >= 0) { sums[j] += s.values[j]; valid[j]++; }
            }
            Array.Sort(wall);
            var stats = new CounterResult[counters.Count];
            for (int j = 0; j < stats.Length; j++) stats[j] = new CounterResult { name = counters[j].Name, validSamples = valid[j], mean = valid[j] > 0 ? sums[j] / valid[j] : -1 };
            return new ViewResult { name = name, frames = count, meanFrameMs = total / count, loopFps = count * 1000 / total,
                p50Ms = Percentile(wall, .5), p95Ms = Percentile(wall, .95), p99Ms = Percentile(wall, .99),
                timingSamples = validTimings, gpuMs = validTimings > 0 ? gpu / validTimings : -1, cpuMs = validTimings > 0 ? cpu / validTimings : -1,
                mainMs = validTimings > 0 ? main / validTimings : -1, renderMs = validTimings > 0 ? render / validTimings : -1,
                presentWaitMs = validTimings > 0 ? present / validTimings : -1, counters = stats };
        }

        private static double Percentile(double[] sorted, double fraction) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(fraction * sorted.Length) - 1)];
        private void WriteSamples(string name, Sample[] samples, int count)
        {
            var csv = new StringBuilder("frame,wallMs,gpuMs,cpuMs,mainMs,renderMs,presentWaitMs");
            foreach (var counter in counters) csv.Append(',').Append(counter.Name);
            csv.AppendLine();
            for (int i = 0; i < count; i++)
            {
                var s = samples[i]; csv.Append(i);
                foreach (double value in new[] { s.wallMs, s.gpuMs, s.cpuMs, s.mainMs, s.renderMs, s.presentMs }) csv.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
                foreach (long value in s.values) csv.Append(',').Append(value);
                csv.AppendLine();
            }
            File.WriteAllText(Path.Combine(output, name + ".csv"), csv.ToString());
        }

        private IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                bool moved = false; object current = null; Exception failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); Finish(1, "FAILED\n" + failure); yield break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested); else yield return current;
            }
        }
        private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private void OnCameraRendered(ScriptableRenderContext context, Camera renderedCamera)
        { if (renderedCamera == viewCamera) cameraRenderEvents++; }
        private void Finish(int code, string message)
        {
            if (finished) return; finished = true;
            foreach (var counter in counters) counter.Dispose();
            RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            if (image != null) { image.Release(); Destroy(image); }
            Application.logMessageReceived -= OnLog;
            File.WriteAllText(Path.Combine(output, "complete.txt"), message);
            Application.Quit(code);
        }
        private sealed class Counter : IDisposable
        {
            public readonly string Name;
            private ProfilerRecorder recorder;
            public Counter(string name, ProfilerRecorder recorder) { Name = name; this.recorder = recorder; }
            public long Read()
            {
                if (!recorder.Valid || recorder.Count == 0) return -1;
                long value = recorder.LastValue;
                return Name.EndsWith("Frame Time", StringComparison.Ordinal) && value <= 0 ? -1 : value;
            }
            public void Dispose() { if (recorder.Valid) recorder.Dispose(); }
        }
        private struct Sample { public double wallMs, gpuMs, cpuMs, mainMs, renderMs, presentMs; public long[] values; }
        [Serializable] private sealed class Report { public ViewResult[] views; }
        [Serializable] private sealed class CounterResult { public string name; public int validSamples; public double mean; }
        [Serializable] private sealed class ViewResult
        {
            public string name; public int frames, timingSamples; public double meanFrameMs, loopFps, p50Ms, p95Ms, p99Ms, gpuMs, cpuMs, mainMs, renderMs, presentWaitMs;
            public long renderEvents; public int renderedUpdateFrames; public double renderCoverage;
            public Vector3 position, rotation; public bool orthographic, vehicleCamera; public float orthographicSize, fieldOfView, pickupDisplacement; public CounterResult[] counters;
        }
        [Serializable] private sealed class EnvironmentReport
        {
            public string unity, cpu, gpu, api, driver, quality, pipeline, mode;
            public int memoryMb, gpuMemoryMb, screenWidth, screenHeight, targetWidth, targetHeight, vsync, frameCap;
            public bool batchMode, editor, development;
        }
    }
}
#endif
