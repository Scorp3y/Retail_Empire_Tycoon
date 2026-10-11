using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal enum ArchitecturePreviewView { Front, Rear, Roof }

    /// <summary>One native-metre camera rig for all models, not individual bounds-normalized thumbnails.</summary>
    internal static class TownArchitecturePreviews
    {
        private const int Width = 1280;
        private const int Height = 800;
        private const int CaptionHeight = 32;
        private const int ModelLayer = 30;
        private const int GridLayer = 29;
        private static readonly Color32 Background = new Color32(244, 242, 233, 255);

        public static Texture2D Render(GameObject prefab, ArchitecturePreviewView view)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D image = null;
            Mesh grid = null;
            var previous = RenderTexture.active;
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = ModelLayer;
                var renderer = instance.GetComponent<MeshRenderer>();
                var cameraObject = new GameObject("Architecture native camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.96f, .95f, .91f, 0);
                camera.orthographic = false;
                camera.fieldOfView = 50;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 400;
                camera.aspect = (float)Width / Height;
                var definition = Definition(view);
                camera.transform.position = definition.position;
                camera.transform.LookAt(definition.focus);
                foreach (var corner in BoundsCorners(renderer.bounds))
                {
                    var point = camera.WorldToViewportPoint(corner);
                    if (point.z <= camera.nearClipPlane || point.x < .025f || point.x > .975f || point.y < .055f || point.y > .94f)
                        throw new InvalidOperationException("Fixed native camera clips the model: " + prefab.name + " / " + view);
                }
                AddLights(scene);
                grid = AddMetricGrid(scene, renderer.sharedMaterial);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                if (!target.Create()) throw new InvalidOperationException("Architecture preview render texture unavailable.");
                camera.targetTexture = target;
                camera.cullingMask = 1 << ModelLayer;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                // Check the building-only pass; otherwise a visible grid could hide an empty model render.
                if (image.GetPixels32().Count(pixel => pixel.a > 32) < 200)
                    throw new InvalidOperationException("Empty native architecture render: " + prefab.name + " / " + view);
                camera.cullingMask |= 1 << GridLayer;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                AddCaption(image, prefab.name + " / " + view + " / METRES");
                return image;
            }
            catch
            {
                if (image != null) Object.DestroyImmediate(image);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
                if (grid != null) Object.DestroyImmediate(grid);
            }
        }

        public static string CameraManifest() => JsonUtility.ToJson(new RigManifest
        {
            views = new[] { Definition(ArchitecturePreviewView.Front), Definition(ArchitecturePreviewView.Rear), Definition(ArchitecturePreviewView.Roof) }
        }, true);

        /// <summary>Close-up for inspecting detail; deliberately separate from the fixed-metre comparison views.</summary>
        public static Texture2D RenderDetail(GameObject prefab)
        {
            var image = TownModelPreviews.Render(prefab);
            AddCaption(image, prefab.name + " / DETAIL");
            return image;
        }

        private static ViewDefinition Definition(ArchitecturePreviewView view)
        {
            return new ViewDefinition
            {
                name = view.ToString(),
                position = view == ArchitecturePreviewView.Front ? new Vector3(45, 29, 60)
                    : view == ArchitecturePreviewView.Rear ? new Vector3(-45, 29, -60) : new Vector3(28, 75, 38),
                focus = view == ArchitecturePreviewView.Roof ? new Vector3(0, 11, 0) : new Vector3(0, 12, 0)
            };
        }

        private static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
        {
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
        }

        private static void AddLights(Scene scene)
        {
            var main = new GameObject("Architecture preview daylight", typeof(Light));
            SceneManager.MoveGameObjectToScene(main, scene);
            main.transform.rotation = Quaternion.Euler(40, -135, 0);
            var sun = main.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.cullingMask = (1 << ModelLayer) | (1 << GridLayer);
            var fill = new GameObject("Architecture preview fill", typeof(Light));
            SceneManager.MoveGameObjectToScene(fill, scene);
            fill.transform.position = new Vector3(-65, 58, -40);
            var light = fill.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 450;
            light.intensity = 3000;
            light.cullingMask = sun.cullingMask;
        }

        private static Mesh AddMetricGrid(Scene scene, Material material)
        {
            var builder = new TownMeshBuilder(TownArchitecturePalette.UV);
            // Two-metre minor divisions, ten-metre majors and one-metre edge ticks; no human reference model.
            for (int coordinate = -40; coordinate <= 40; coordinate += 2)
            {
                if (coordinate % 10 == 0) continue;
                builder.Ground(coordinate, 0, .025f, 80, -.026f, TownColor.PlasterGrey);
                builder.Ground(0, coordinate, 80, .025f, -.026f, TownColor.PlasterGrey);
            }
            for (int coordinate = -40; coordinate <= 40; coordinate += 10)
            {
                builder.Ground(coordinate, 0, .045f, 80, -.025f, TownColor.Pavement);
                builder.Ground(0, coordinate, 80, .045f, -.025f, TownColor.Pavement);
            }
            for (int coordinate = -40; coordinate <= 40; coordinate++)
            {
                builder.Ground(coordinate, -40, .045f, .4f, -.025f, TownColor.Frame);
                builder.Ground(-40, coordinate, .4f, .045f, -.025f, TownColor.Frame);
            }
            var mesh = builder.Build("Architecture native metric grid");
            var root = new GameObject("Temporary ten-metre grid", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = GridLayer;
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            return mesh;
        }

        public static void Sheet(IReadOnlyList<Texture2D> images, IReadOnlyList<string> ids, string view, string path)
        {
            if (images.Count != 9 || ids.Count != images.Count) throw new InvalidOperationException("Nine labelled architecture variants are required per sheet.");
            const int columns = 3, tileWidth = 512, pictureHeight = 320, labelHeight = 26;
            int tileHeight = pictureHeight + labelHeight;
            var sheet = new Texture2D(columns * tileWidth, 3 * tileHeight, TextureFormat.RGBA32, false);
            try
            {
                var pixels = Enumerable.Repeat(Background, sheet.width * sheet.height).ToArray();
                for (int index = 0; index < images.Count; index++)
                {
                    int left = index % columns * tileWidth, bottom = (2 - index / columns) * tileHeight;
                    var source = images[index];
                    for (int y = 0; y < pictureHeight; y++)
                        for (int x = 0; x < tileWidth; x++)
                        {
                            var color = (Color32)source.GetPixelBilinear((x + .5f) / tileWidth, (y + .5f) / pictureHeight);
                            if (color.a == 0) color = Background;
                            color.a = 255;
                            pixels[(bottom + y) * sheet.width + left + x] = color;
                        }
                    DrawLabel(pixels, sheet.width, sheet.height, left + 10, bottom + pictureHeight + 6, ids[index] + " / " + view, 2);
                }
                sheet.SetPixels32(pixels);
                sheet.Apply();
                File.WriteAllBytes(path, sheet.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(sheet); }
        }

        private static void AddCaption(Texture2D image, string text)
        {
            var pixels = image.GetPixels32();
            for (int y = image.height - CaptionHeight; y < image.height; y++)
                for (int x = 0; x < image.width; x++) pixels[y * image.width + x] = Background;
            DrawLabel(pixels, image.width, image.height, 12, image.height - 24, text, 2);
            image.SetPixels32(pixels);
            image.Apply();
        }

        private static void DrawLabel(Color32[] pixels, int width, int height, int left, int bottom, string label, int scale)
        {
            var ink = new Color32(38, 60, 53, 255);
            foreach (char character in label.ToUpperInvariant())
            {
                var rows = Glyph(character).Split('/');
                for (int row = 0; row < rows.Length; row++)
                    for (int column = 0; column < rows[row].Length; column++)
                    {
                        if (rows[row][column] != '1') continue;
                        for (int dy = 0; dy < scale; dy++)
                            for (int dx = 0; dx < scale; dx++)
                            {
                                int x = left + column * scale + dx, y = bottom + (6 - row) * scale + dy;
                                if (x >= 0 && x < width && y >= 0 && y < height) pixels[y * width + x] = ink;
                            }
                    }
                left += 6 * scale;
            }
        }

        // A tiny deterministic ASCII label font avoids editor GUI / platform font dependencies in batch capture.
        // Russian display names are preserved in the HTML catalogue; image labels identify exact asset IDs.
        private static string Glyph(char character)
        {
            switch (character)
            {
                case 'A': return "01110/10001/10001/11111/10001/10001/10001";
                case 'B': return "11110/10001/10001/11110/10001/10001/11110";
                case 'C': return "01111/10000/10000/10000/10000/10000/01111";
                case 'D': return "11110/10001/10001/10001/10001/10001/11110";
                case 'E': return "11111/10000/10000/11110/10000/10000/11111";
                case 'F': return "11111/10000/10000/11110/10000/10000/10000";
                case 'G': return "01111/10000/10000/10111/10001/10001/01111";
                case 'H': return "10001/10001/10001/11111/10001/10001/10001";
                case 'I': return "11111/00100/00100/00100/00100/00100/11111";
                case 'J': return "00111/00010/00010/00010/10010/10010/01100";
                case 'K': return "10001/10010/10100/11000/10100/10010/10001";
                case 'L': return "10000/10000/10000/10000/10000/10000/11111";
                case 'M': return "10001/11011/10101/10101/10001/10001/10001";
                case 'N': return "10001/11001/10101/10011/10001/10001/10001";
                case 'O': return "01110/10001/10001/10001/10001/10001/01110";
                case 'P': return "11110/10001/10001/11110/10000/10000/10000";
                case 'Q': return "01110/10001/10001/10001/10101/10010/01101";
                case 'R': return "11110/10001/10001/11110/10100/10010/10001";
                case 'S': return "01111/10000/10000/01110/00001/00001/11110";
                case 'T': return "11111/00100/00100/00100/00100/00100/00100";
                case 'U': return "10001/10001/10001/10001/10001/10001/01110";
                case 'V': return "10001/10001/10001/10001/10001/01010/00100";
                case 'W': return "10001/10001/10001/10101/10101/11011/10001";
                case 'X': return "10001/10001/01010/00100/01010/10001/10001";
                case 'Y': return "10001/10001/01010/00100/00100/00100/00100";
                case 'Z': return "11111/00001/00010/00100/01000/10000/11111";
                case '0': return "01110/10001/10011/10101/11001/10001/01110";
                case '1': return "00100/01100/00100/00100/00100/00100/01110";
                case '2': return "01110/10001/00001/00010/00100/01000/11111";
                case '3': return "11110/00001/00001/01110/00001/00001/11110";
                case '4': return "00010/00110/01010/10010/11111/00010/00010";
                case '5': return "11111/10000/10000/11110/00001/00001/11110";
                case '6': return "01110/10000/10000/11110/10001/10001/01110";
                case '7': return "11111/00001/00010/00100/01000/01000/01000";
                case '8': return "01110/10001/10001/01110/10001/10001/01110";
                case '9': return "01110/10001/10001/01111/00001/00001/01110";
                case '/': return "00001/00001/00010/00100/01000/10000/10000";
                case '-': return "00000/00000/00000/11111/00000/00000/00000";
                default: return "00000/00000/00000/00000/00000/00000/00000";
            }
        }

        [Serializable] private sealed class RigManifest
        {
            public int schemaVersion = 1, width = Width, height = Height;
            public float fieldOfView = 50, nearClip = .1f, farClip = 400;
            public string units = "Native metres; fixed poses for every model; grid minor divisions 2 m, majors 10 m, edge ticks 1 m.";
            public ViewDefinition[] views;
        }
        [Serializable] private sealed class ViewDefinition { public string name; public Vector3 position, focus; }
    }
}
