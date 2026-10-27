using System;
using System.Collections.Generic;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Scene-owned, bounded sidewalk population. Shares appearances, never shopping visits,
    /// reservations or saves. Only the observer's neighbourhood is spawned and advanced.</summary>
    public sealed class TownPedestrianTraffic : MonoBehaviour
    {
        public sealed class Pedestrian
        {
            public GameObject Root {get;internal set;}
            public Animator Animator {get;internal set;}
            public int Appearance {get;internal set;}
            public int RouteIndex {get;internal set;}
            public float Distance {get;internal set;}
            public float Travelled {get;internal set;}
            public Vector3 LocalPosition=>Root.transform.localPosition;
            internal GameObject Visual;
            internal float Speed,Pause,Blocked;
            internal int Direction;
            internal bool Walking;
        }
        private readonly struct Spawn
        {
            public readonly int Route;
            public readonly float Distance;
            public readonly Vector3 Position;
            public Spawn(int route,float distance,Vector3 position){Route=route;Distance=distance;Position=position;}
        }
        [SerializeField] private TownPedestrianRoutes routes;
        [SerializeField] private ShopperRoster roster;
        [SerializeField] private int maximumPedestrians=32;
        [SerializeField] private float nativeHeight=ShopperDimensions.StreetHeight;
        private const float ChunkSize=96;
        private readonly List<Pedestrian> active=new();
        private readonly List<Pedestrian> pool=new();
        private readonly Dictionary<Vector2Int,List<Spawn>> chunks=new();
        private readonly List<Spawn> candidates=new();
        private readonly RaycastHit[] hits=new RaycastHit[32];
        private ShopperSelection selection;
        private System.Random random;
        private Transform observer;
        private Vector2Int observerChunk;
        private bool initialized,hasChunk;
        private int spawnCursor;
        private float nextSpawn,nextObserverSearch;
        public IReadOnlyList<Pedestrian> ActivePedestrians=>active;
        public TownPedestrianRoutes Routes=>routes;
        public int PoolCount=>pool.Count;
        public int MaximumPedestrians=>maximumPedestrians;
        public long SimulatedSteps {get;private set;}
        public void Configure(TownPedestrianRoutes paths,ShopperRoster appearances,float height,int capacity=32)
        {
            if(initialized)throw new InvalidOperationException("A running street population cannot be reconfigured.");
            if(paths==null || appearances==null || appearances.Variants.Count!=50 || height<=0 || float.IsNaN(height)
                || float.IsInfinity(height) || capacity<1 || capacity>64)throw new ArgumentException("Valid sidewalks, fifty appearances and bounded capacity required.");
            routes=paths;roster=appearances;nativeHeight=height;maximumPedestrians=capacity;
        }
        public void SetObserver(Transform target){observer=target;hasChunk=false;}
        private void OnDisable()
        {
            foreach(var person in active)person.Root.SetActive(false);
            active.Clear();hasChunk=false;
        }
        private void Update()
        {
            if(observer==null && Time.unscaledTime>=nextObserverSearch)
            {
                nextObserverSearch=Time.unscaledTime+1;
                var camera=Camera.main;
                if(camera!=null)
                {
                    var chase=camera.GetComponent<PickupCamera>();
                    SetObserver(chase!=null && chase.target!=null?chase.target:camera.transform);
                }
            }
            if(observer!=null && Time.deltaTime>0)Simulate(Mathf.Min(Time.deltaTime,.1f),observer.position,true);
        }
        private void Initialize()
        {
            if(initialized)return;
            if(routes==null || roster==null || roster.Variants.Count!=50)throw new InvalidOperationException("Street population has not been authored.");
            random=new System.Random(89173);selection=roster.CreateSelection(47291);
            for(int i=0;i<routes.Routes.Count;i++)
            {
                var path=routes.Routes[i].Path;
                for(float d=2;d<path.Length-2;d+=14)
                {
                    var position=path.Sample(d,out _);var cell=Cell(position);
                    if(!chunks.TryGetValue(cell,out var group))chunks.Add(cell,group=new List<Spawn>());
                    group.Add(new Spawn(i,d,position));
                }
            }
            initialized=true;
        }
        public void Simulate(float seconds,Vector3 worldObserver,bool physicsQueries)
        {
            if(seconds<=0 || seconds>.25f || float.IsNaN(seconds) || float.IsNaN(worldObserver.sqrMagnitude)
                || float.IsInfinity(worldObserver.sqrMagnitude))throw new ArgumentOutOfRangeException(nameof(seconds));
            Initialize();
            var localObserver=transform.InverseTransformPoint(worldObserver);var cell=Cell(localObserver);
            bool initial=!hasChunk;
            bool neighbourhoodChanged=initial || cell!=observerChunk;
            if(neighbourhoodChanged)
            {
                observerChunk=cell;hasChunk=true;candidates.Clear();spawnCursor=0;
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                    if(chunks.TryGetValue(cell+new Vector2Int(x,z),out var group))candidates.AddRange(group);
                for(int i=candidates.Count-1;i>0;i--)
                {int j=random.Next(i+1);var value=candidates[i];candidates[i]=candidates[j];candidates[j]=value;}
            }
            for(int i=active.Count-1;i>=0;i--)
            {
                var at=Cell(active[i].LocalPosition);
                if(Mathf.Abs(at.x-cell.x)>1 || Mathf.Abs(at.y-cell.y)>1)
                {active[i].Root.SetActive(false);active.RemoveAt(i);}
            }
            nextSpawn-=seconds;
            if(neighbourhoodChanged || nextSpawn<=0)
            {
                nextSpawn=1;
                for(int i=0;i<(neighbourhoodChanged?maximumPedestrians:2) && active.Count<maximumPedestrians;i++)SpawnPerson(localObserver,initial);
            }
            foreach(var person in active){Advance(person,seconds,physicsQueries);SimulatedSteps++;}
        }
        private void SpawnPerson(Vector3 eye,bool initial)
        {
            for(int attempt=0;attempt<candidates.Count;attempt++)
            {
                var spawn=candidates[spawnCursor++%candidates.Count];
                if(!initial && Vector3.Distance(spawn.Position,eye)<28)continue;
                bool occupied=false;
                foreach(var other in active)if(Vector3.Distance(other.LocalPosition,spawn.Position)<4){occupied=true;break;}
                if(occupied)continue;
                Pedestrian person=null;
                foreach(var item in pool)if(!item.Root.activeSelf){person=item;break;}
                if(person==null)
                {
                    if(pool.Count>=maximumPedestrians)return;
                    person=new Pedestrian{Root=new GameObject("Street pedestrian")};
                    person.Root.transform.SetParent(transform,false);person.Root.SetActive(false);pool.Add(person);
                }
                var prefab=selection.Next();int number=prefab.GetComponent<ShopperAppearance>().Number;
                if(person.Visual==null || person.Appearance!=number)
                {
                    if(person.Visual!=null){person.Visual.SetActive(false);Destroy(person.Visual);}
                    person.Visual=Instantiate(prefab,person.Root.transform);person.Visual.GetComponent<ShopperAppearance>().FitHeight(nativeHeight);
                    person.Animator=person.Visual.GetComponentInChildren<Animator>();person.Animator.applyRootMotion=false;person.Appearance=number;
                }
                person.RouteIndex=spawn.Route;person.Distance=spawn.Distance;person.Direction=random.Next(2)==0?-1:1;
                person.Speed=.75f+(float)random.NextDouble()*.35f;person.Pause=person.Blocked=person.Travelled=0;
                Pose(person,0);person.Root.SetActive(true);person.Animator.Rebind();person.Animator.Update(0);
                person.Walking=false;SetWalking(person,true);active.Add(person);return;
            }
        }
        private void Advance(Pedestrian person,float seconds,bool physicsQueries)
        {
            if(person.Pause>0){person.Pause-=seconds;SetWalking(person,false);return;}
            var path=routes.Routes[person.RouteIndex].Path;
            path.Sample(person.Distance,out var tangent);var forward=tangent*person.Direction;
            bool blocked=false;
            foreach(var other in active)
            {
                if(other==person)continue;
                var delta=other.LocalPosition-person.LocalPosition;float along=Vector3.Dot(delta,forward);
                if(along>0 && along<.65f && Vector3.Cross(delta,forward).magnitude<.30f){blocked=true;break;}
            }
            if(!blocked && physicsQueries)blocked=Obstructed(person,forward,person.Speed*seconds+.28f);
            SetWalking(person,!blocked);
            if(blocked)
            {
                person.Blocked+=seconds;
                if(person.Blocked>6){person.Direction*=-1;person.Blocked=0;person.Pause=1;}
                return;
            }
            person.Blocked=0;
            float next=Mathf.Clamp(person.Distance+person.Direction*person.Speed*seconds,0,path.Length);
            person.Travelled+=Mathf.Abs(next-person.Distance);person.Distance=next;Pose(person,seconds);
            if(next<=.001f || next>=path.Length-.001f){person.Direction*=-1;person.Pause=1.5f+(float)random.NextDouble()*3;SetWalking(person,false);}
        }
        private void Pose(Pedestrian person,float seconds)
        {
            var point=routes.Routes[person.RouteIndex].Path.Sample(person.Distance,out var tangent);
            var forward=tangent*person.Direction;
            // Two narrow walking tracks allow opposing pedestrians to pass on the same sidewalk.
            person.Root.transform.localPosition=point+Vector3.Cross(Vector3.up,forward).normalized*.34f;
            var rotation=Quaternion.LookRotation(forward);
            person.Root.transform.localRotation=seconds<=0?rotation:Quaternion.RotateTowards(person.Root.transform.localRotation,rotation,240*seconds);
        }
        private bool Obstructed(Pedestrian person,Vector3 forward,float distance)
        {
            float scale=transform.lossyScale.x;var p=person.LocalPosition;
            int count=Physics.CapsuleCastNonAlloc(transform.TransformPoint(p+Vector3.up*.36f),transform.TransformPoint(p+Vector3.up*(nativeHeight-.25f)),
                .24f*scale,transform.TransformDirection(forward),hits,distance*scale,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return true;
            for(int i=0;i<count;i++)if(!hits[i].collider.transform.IsChildOf(person.Root.transform) && hits[i].normal.y<.65f)return true;
            return false;
        }
        private static void SetWalking(Pedestrian person,bool walking)
        {
            if(person.Walking==walking)return;
            person.Walking=walking;person.Animator.SetBool("isWalking",walking);person.Animator.SetBool("isBrowsing",false);
        }
        private static Vector2Int Cell(Vector3 p)=>new(Mathf.FloorToInt(p.x/ChunkSize),Mathf.FloorToInt(p.z/ChunkSize));
    }
}
