using System.Collections.Generic;

namespace RetailEmpireTycoon.Parking
{
    /// <summary>One car per bay. A single maneuver lease protects the narrow shared driveway.</summary>
    public sealed class ParkingReservations
    {
        private readonly HashSet<int> bays = new HashSet<int>();
        private int? maneuver;
        public bool Reserve(int bay) => bays.Add(bay);
        public bool BeginManeuver(int bay)
        {
            if (!bays.Contains(bay) || maneuver.HasValue) return false;
            maneuver = bay; return true;
        }
        public void EndManeuver(int bay) { if(maneuver==bay) maneuver=null; }
        public void Release(int bay) { bays.Remove(bay); EndManeuver(bay); }
        public bool IsReserved(int bay) => bays.Contains(bay);
    }
}
