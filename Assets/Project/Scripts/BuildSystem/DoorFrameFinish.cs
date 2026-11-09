using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    public enum DoorFinish { Auto, Graphite, Cream, DarkGreen, Bronze }

    /// <summary>Swaps only the authored frame slots and uses the same shared materials on every model.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorFrameFinish : MonoBehaviour
    {
        [Serializable]
        public struct Slot
        {
            public Renderer renderer;
            public int index;
            public Material original;
        }

        [SerializeField] private Slot[] slots;
        [SerializeField] private Material[] palette;
        [SerializeField] private bool cornerOpening;
        public DoorFinish Choice { get; private set; }
        public DoorFinish Resolved { get; private set; }

        public void Configure(Slot[] bindings, Material[] sharedPalette, bool corner)
        {
            slots = bindings;
            palette = sharedPalette;
            cornerOpening = corner;
        }

        public void SetChoice(DoorFinish choice) => Choice = choice >= DoorFinish.Auto && choice <= DoorFinish.Bronze
            ? choice : DoorFinish.Auto;

        public DoorFinish Resolve(DoorFinish choice, IEnumerable<Vector3Int> footprint, int facing,
            IReadOnlyDictionary<Vector3Int, DoorFinish> walls)
        {
            if (choice != DoorFinish.Auto) return choice;
            var right = StoreyLink.Direction(facing + 1);
            var front = StoreyLink.Direction(facing);
            var cells = new HashSet<Vector3Int>(footprint);
            int frontEdge = int.MinValue, rightEdge = int.MinValue;
            if (cornerOpening)
                foreach (var cell in cells)
                {
                    frontEdge = Mathf.Max(frontEdge, Along(cell, front));
                    rightEdge = Mathf.Max(rightEdge, Along(cell, right));
                }
            var match = DoorFinish.Auto;
            foreach (var cell in cells)
            {
                if (!cornerOpening || Along(cell, front) >= frontEdge - 1)
                    if (DifferentWall(cell - right)) return DoorFinish.Graphite;
                if (!cornerOpening)
                {
                    if (DifferentWall(cell + right)) return DoorFinish.Graphite;
                }
                else if (Along(cell, right) >= rightEdge - 1 && DifferentWall(cell - front))
                    return DoorFinish.Graphite;
            }
            return match;

            bool DifferentWall(Vector3Int neighbor)
            {
                if (cells.Contains(neighbor) || !walls.TryGetValue(neighbor, out var finish)) return false;
                if (match != DoorFinish.Auto && match != finish) return true;
                match = finish;
                return false;
            }
        }

        private static int Along(Vector3Int cell, Vector3Int direction) => cell.x * direction.x + cell.z * direction.z;

        public void Apply(DoorFinish finish)
        {
            if (Resolved == finish) return;
            Resolved = finish;
            foreach (var slot in slots) Replace(slot.renderer, slot.index, MaterialFor(slot, finish));
        }

        internal void ApplyPreview(Transform target, DoorFinish finish, Func<Material, Material> previewMaterial)
        {
            foreach (var slot in slots)
            {
                var renderer = FindCopy(slot.renderer.transform, target).GetComponent<Renderer>();
                Replace(renderer, slot.index, previewMaterial(MaterialFor(slot, finish)));
            }
        }

        private Transform FindCopy(Transform source, Transform target) =>
            source == transform ? target : FindCopy(source.parent, target).GetChild(source.GetSiblingIndex());

        private Material MaterialFor(Slot slot, DoorFinish finish) =>
            finish == DoorFinish.Auto ? slot.original : palette[(int)finish - 1];

        private static void Replace(Renderer renderer, int index, Material material)
        {
            var materials = renderer.sharedMaterials;
            materials[index] = material;
            renderer.sharedMaterials = materials;
        }
    }
}
