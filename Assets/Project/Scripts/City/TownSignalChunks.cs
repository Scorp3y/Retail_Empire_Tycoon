using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Streams signal simulation, not the whole town. Only a 3x3 neighbourhood is ticked;
    /// distant lamps keep their cached appearance and resume at the absolute cycle time on return.</summary>
    public sealed class TownSignalChunks : MonoBehaviour
    {
        [Serializable]
        public sealed class Junction
        {
            [SerializeField] private TownJunctionSignals signal;
            [SerializeField] private Vector3 center;
            public TownJunctionSignals Signal=>signal;
            public Vector3 Center=>center;
            public Junction(TownJunctionSignals signal,Vector3 center){this.signal=signal;this.center=center;}
        }
        [SerializeField] private List<Junction> junctions=new();
        [SerializeField] private float chunkSize=96;
        private readonly Dictionary<Vector2Int,List<Junction>> chunks=new();
        private readonly List<Junction> active=new();
        private Transform observer;
        private Vector2Int currentChunk;
        private bool indexed,hasChunk;
        private double nextTick,nextObserverSearch;
        public int ActiveJunctionCount=>active.Count;
        public int RegisteredJunctionCount=>junctions.Count;
        public long EvaluationCount{get;private set;}
        public Vector2Int CurrentChunk=>currentChunk;
        public IReadOnlyList<Junction> Junctions=>junctions;
        public bool IsActive(TownJunctionSignals signal)=>active.Any(j=>j.Signal==signal);

        public void Configure(IEnumerable<Junction> bindings,float nativeChunkSize=96)
        {
            var entries=bindings?.ToList()??throw new ArgumentNullException(nameof(bindings));
            if(nativeChunkSize<=0||float.IsNaN(nativeChunkSize)||float.IsInfinity(nativeChunkSize))throw new ArgumentOutOfRangeException(nameof(nativeChunkSize));
            if(entries.Count==0||entries.Any(j=>j==null||j.Signal==null||!j.Signal.IsConfigured||!Finite(j.Center))||entries.Select(j=>j.Signal).Distinct().Count()!=entries.Count)
                throw new ArgumentException("Signal chunks require distinct configured junctions with finite local centres.");
            junctions=entries;chunkSize=nativeChunkSize;indexed=false;hasChunk=false;
        }
        public void SetObserver(Transform target){observer=target;hasChunk=false;nextTick=0;}
        private void OnEnable(){hasChunk=false;nextTick=0;}
        private void LateUpdate()
        {
            if(observer==null&&Time.unscaledTimeAsDouble>=nextObserverSearch)
            {
                nextObserverSearch=Time.unscaledTimeAsDouble+1;
                var camera=Camera.main;if(camera!=null)SetObserver(camera.transform);
            }
            if(observer==null||Time.unscaledTimeAsDouble<nextTick)return;
            nextTick=Time.unscaledTimeAsDouble+.2;
            Tick(Time.timeAsDouble,observer.position);
        }
        /// <summary>Observer positions are world-space; chunk centres are native town-local metres,
        /// so the same index works in both the full-size driving scene and scaled shop view.</summary>
        public void Tick(double elapsedSeconds,Vector3 observerWorldPosition)
        {
            if(!Finite(observerWorldPosition))throw new ArgumentOutOfRangeException(nameof(observerWorldPosition));
            if(!indexed)BuildIndex();
            var next=Cell(transform.InverseTransformPoint(observerWorldPosition));
            if(!hasChunk||next!=currentChunk)
            {
                currentChunk=next;hasChunk=true;active.Clear();
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                    if(chunks.TryGetValue(next+new Vector2Int(x,z),out var group))active.AddRange(group);
            }
            foreach(var j in active){j.Signal.DisplayAt(elapsedSeconds+j.Signal.PhaseOffset);EvaluationCount++;}
        }
        private void BuildIndex()
        {
            chunks.Clear();foreach(var j in junctions)
            {
                if(j==null||j.Signal==null||!j.Signal.IsConfigured)throw new InvalidOperationException("A streamed junction lost its lamp bindings.");
                var key=Cell(j.Center);if(!chunks.TryGetValue(key,out var group))chunks.Add(key,group=new());group.Add(j);
            }
            indexed=true;
        }
        private Vector2Int Cell(Vector3 p)=>new(Mathf.FloorToInt(p.x/chunkSize),Mathf.FloorToInt(p.z/chunkSize));
        private static bool Finite(Vector3 p)=>!float.IsNaN(p.x+p.y+p.z)&&!float.IsInfinity(p.x+p.y+p.z);
    }
}
