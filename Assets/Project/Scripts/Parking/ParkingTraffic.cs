using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using UnityEngine;

namespace RetailEmpireTycoon.Parking
{
    /// <summary>Parking arrivals reserve their bay and approach; parked visitors release it only after departure.</summary>
    public sealed class ParkingTraffic : MonoBehaviour
    {
        public GridSystem grid;
        public FloorPainter asphalt;
        public GameObject carPrefab;
        public Transform roadEntry;
        public float arrivalInterval=12;
        [Min(0)] public float parkingStaySeconds=25;
        private readonly ParkingReservations reservations=new ParkingReservations();
        private readonly List<Visit> visits=new List<Visit>();
        private Visit moving;
        private float nextArrival;
        private sealed class Visit
        {
            public ParkingBay bay; public GameObject car; public List<Vector3> route;
            public int id,next; public float leaveAt,blockedSince; public bool leaving;
        }
        private void Update()
        {
            foreach(var visit in visits.ToArray())
                if(visit.bay==null || visit.car==null) Release(visit);
            if(moving!=null) { Move(); return; }
            var departure=visits.FirstOrDefault(v=>v.leaveAt>0 && Time.time>=v.leaveAt);
            if(departure!=null)
            {
                if(!reservations.BeginManeuver(departure.bay.GetInstanceID())) return;
                departure.leaving=true; departure.route.Reverse(); departure.next=0; departure.blockedSince=0;
                moving=departure; return;
            }
            if(Time.time<nextArrival) return; nextArrival=Time.time+arrivalInterval;
            foreach(var bay in FindObjectsOfType<ParkingBay>().Where(b=>b.GetComponentInParent<BuildPreview>()==null && !(b.GetComponent<PlacedObject>()?.playerParking ?? false)))
            {
                int id=bay.GetInstanceID(); if(reservations.IsReserved(id)) continue;
                var route=Route(bay); if(route.Count==0) continue;
                if(!reservations.Reserve(id)||!reservations.BeginManeuver(id)) continue;
                var car=Instantiate(carPrefab,roadEntry.position,roadEntry.rotation);
                car.name="Parking visitor";car.transform.SetParent(transform,true);
                moving=new Visit{id=id,bay=bay,car=car,route=route}; visits.Add(moving); break;
            }
        }
        private List<Vector3> Route(ParkingBay bay)
        {
            var cells=asphalt.BuildSaveData().Select(s=>new Vector3Int(s.x,0,s.z)).Where(asphalt.IsAsphalt).ToHashSet();
            if(cells.Count==0) return new List<Vector3>();
            var start=cells.OrderBy(c=>(grid.CellToWorld(c)-roadEntry.position).sqrMagnitude).First();
            var goal=grid.WorldToCell(bay.ApproachPosition);
            if(!cells.Contains(goal)) return new List<Vector3>();
            var frontier=new Queue<Vector3Int>(); var previous=new Dictionary<Vector3Int,Vector3Int>(); frontier.Enqueue(start); previous[start]=start;
            while(frontier.Count>0)
            {
                var current=frontier.Dequeue(); if(current==goal) break;
                foreach(var direction in new[]{Vector3Int.left,Vector3Int.right,Vector3Int.forward,Vector3Int.back})
                {
                    var next=current+direction;
                    // Reserve space for the entire car, not just its center point.
                    if(previous.ContainsKey(next)||!cells.Contains(next)||grid.IsOccupied(next)
                        || !cells.Contains(next+new Vector3Int(direction.z,0,direction.x))
                        || !cells.Contains(next-new Vector3Int(direction.z,0,direction.x))
                        || grid.IsOccupied(next+new Vector3Int(direction.z,0,direction.x))
                        || grid.IsOccupied(next-new Vector3Int(direction.z,0,direction.x))) continue;
                    previous[next]=current; frontier.Enqueue(next);
                }
            }
            if(!previous.ContainsKey(goal)) return new List<Vector3>();
            var path=new List<Vector3>(); var cursor=goal;
            while(cursor!=start) { path.Add(grid.CellToWorld(cursor)+Vector3.up*.02f); cursor=previous[cursor]; }
            path.Add(grid.CellToWorld(start)+Vector3.up*.02f); path.Reverse(); path.Add(bay.ParkPosition+Vector3.up*.02f);
            return path;
        }
        private void Move()
        {
            if(moving.next>=moving.route.Count)
            {
                if(moving.leaving) Release(moving);
                else { moving.car.transform.rotation=moving.bay.transform.rotation; moving.leaveAt=Time.time+parkingStaySeconds; reservations.EndManeuver(moving.bay.GetInstanceID()); moving=null; }
                return;
            }
            var car=moving.car; var target=moving.route[moving.next]; var delta=target-car.transform.position; delta.y=0;
            bool obstacle=Physics.SphereCastAll(car.transform.position+Vector3.up*.17f,.15f,delta.normalized,
                Mathf.Min(delta.magnitude,.35f),~0,QueryTriggerInteraction.Ignore)
                .Any(h=>!h.transform.IsChildOf(car.transform) && h.transform!=car.transform && h.collider.bounds.max.y>.08f);
            if(obstacle)
            {
                if(moving.blockedSince==0) moving.blockedSince=Time.time;
                // A removed bay or blocked driveway must not retain a lease forever.
                if(Time.time-moving.blockedSince>12) Release(moving);
                return;
            }
            moving.blockedSince=0;
            if(delta.sqrMagnitude<.001f) { moving.next++; return; }
            car.transform.position=Vector3.MoveTowards(car.transform.position,target,.45f*Time.deltaTime);
            car.transform.rotation=Quaternion.Slerp(car.transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*5);
        }
        private void Release(Visit visit)
        {
            reservations.Release(visit.id);
            if(visit.car!=null) Destroy(visit.car); visits.Remove(visit); if(moving==visit) moving=null;
        }
        private void OnDisable() { foreach(var visit in visits.ToArray()) Release(visit); }
    }
}
