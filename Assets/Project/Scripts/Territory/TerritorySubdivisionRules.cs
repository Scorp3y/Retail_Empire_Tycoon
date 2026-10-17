using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.Territory
{
    /// <summary>One authored land catalogue owns adjacency, prices and the migration of old region ownership.</summary>
    [CreateAssetMenu(menuName="Retail Empire Tycoon/Territory Subdivision")]
    public sealed class TerritorySubdivisionRules : ScriptableObject
    {
        public const int CurrentVersion=2;
        [Serializable] public sealed class Parcel
        {
            [SerializeField] private TerritoryId id,legacyRegion;
            [SerializeField] private Rect bounds;
            [SerializeField] private int price;
            public TerritoryId Id=>id;
            public TerritoryId LegacyRegion=>legacyRegion;
            public Rect Bounds=>bounds;
            public int Price=>price;
            public Parcel(TerritoryId id,TerritoryId legacyRegion,Rect bounds,int price)
            {this.id=id;this.legacyRegion=legacyRegion;this.bounds=bounds;this.price=price;}
        }
        [SerializeField] private Rect initialBounds;
        [SerializeField] private List<Parcel> parcels=new();
        public Rect InitialBounds=>initialBounds;
        public IReadOnlyList<Parcel> Parcels=>parcels;
        public bool Contains(TerritoryId id)=>parcels.Any(p=>p.Id==id);
        /// <summary>Returns the authoritative price; throws for an ID outside this catalogue.</summary>
        public int PriceOf(TerritoryId id)=>parcels.Single(p=>p.Id==id).Price;
        public void Configure(Rect initial,IEnumerable<Parcel> definitions)
        {
            if(definitions==null)throw new ArgumentNullException(nameof(definitions));
            var list=definitions.ToList();
            if(list.Count!=24||list.Any(p=>p==null)||list.Select(p=>p.Id).Distinct().Count()!=24)throw new ArgumentException("Exactly 24 distinct parcels are required.");
            if(list.Select(p=>p.LegacyRegion).Distinct().Count()!=9)throw new ArgumentException("All nine legacy regions must retain their land.");
            foreach(var p in list)
            {
                if((int)p.Id<100||(int)p.Id>123||(int)p.LegacyRegion<0||(int)p.LegacyRegion>8||p.Price<0||!Valid(p.Bounds))
                    throw new ArgumentException("Invalid parcel definition: "+p.Id);
                if(Overlaps(p.Bounds,initial)||list.Any(q=>q!=p&&Overlaps(p.Bounds,q.Bounds)))throw new ArgumentException("Land parcels overlap.");
            }
            if(!Valid(initial))throw new ArgumentException("Invalid initial plot.");
            var reached=new HashSet<TerritoryId>();
            while(true)
            {
                var next=list.Where(p=>!reached.Contains(p.Id)&&(Adjacent(p.Bounds,initial)||list.Any(q=>reached.Contains(q.Id)&&Adjacent(p.Bounds,q.Bounds)))).ToArray();
                if(next.Length==0)break;foreach(var p in next)reached.Add(p.Id);
            }
            if(reached.Count!=list.Count)throw new ArgumentException("The land catalogue contains unreachable parcels.");
            initialBounds=initial;parcels=list;
        }
        public bool IsAvailable(TerritoryId id,ISet<TerritoryId> purchased)
        {
            if(purchased==null)throw new ArgumentNullException(nameof(purchased));
            var target=parcels.SingleOrDefault(p=>p.Id==id);
            return target!=null&&!purchased.Contains(id)&&(Adjacent(target.Bounds,initialBounds)||parcels.Any(p=>purchased.Contains(p.Id)&&Adjacent(target.Bounds,p.Bounds)));
        }
        public HashSet<TerritoryId> Restore(TerritorySaveData data)
        {
            var result=new HashSet<TerritoryId>();
            foreach(string value in data?.purchased??new List<string>())
            {
                if(!Enum.TryParse(value,out TerritoryId id)||!Enum.IsDefined(typeof(TerritoryId),id))continue;
                if(Contains(id))result.Add(id);
                else if((data?.layoutVersion??0)<CurrentVersion)
                    foreach(var parcel in parcels.Where(p=>p.LegacyRegion==id))result.Add(parcel.Id);
            }
            AddCompletedRegions(result);return result;
        }
        public void AddCompletedRegions(ISet<TerritoryId> purchased)
        {
            // Derived aliases preserve old store-level/catalogue unlocks; they are not extra purchases.
            foreach(var group in parcels.GroupBy(p=>p.LegacyRegion))
                if(group.All(p=>purchased.Contains(p.Id)))purchased.Add(group.Key);
                else purchased.Remove(group.Key);
        }
        public static bool Adjacent(Rect a,Rect b)
        {
            const float tolerance=.002f;
            bool vertical=(Mathf.Abs(a.xMax-b.xMin)<tolerance||Mathf.Abs(b.xMax-a.xMin)<tolerance)&&Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)>tolerance;
            bool horizontal=(Mathf.Abs(a.yMax-b.yMin)<tolerance||Mathf.Abs(b.yMax-a.yMin)<tolerance)&&Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)>tolerance;
            return vertical||horizontal;
        }
        private static bool Overlaps(Rect a,Rect b)=>Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)>.002f&&Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)>.002f;
        private static bool Valid(Rect r)=>r.width>0&&r.height>0&&!float.IsNaN(r.x+r.y+r.width+r.height)&&!float.IsInfinity(r.x+r.y+r.width+r.height);
    }
}
