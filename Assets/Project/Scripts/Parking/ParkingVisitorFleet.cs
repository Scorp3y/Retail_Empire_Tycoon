using System;
using System.Collections.Generic;
using RetailEmpireTycoon.City;
using UnityEngine;

namespace RetailEmpireTycoon.Parking
{
    /// <summary>Explicit passenger/light-vehicle allowlist. Road trucks never become parking visitors.</summary>
    public sealed class ParkingVisitorFleet : ScriptableObject
    {
        [SerializeField] private TrafficVehicle[] vehicles=Array.Empty<TrafficVehicle>();
        [SerializeField] private float nativeToShopScale=.5f;
        public IReadOnlyList<TrafficVehicle> Vehicles=>vehicles;
        public float ModelScale=>nativeToShopScale;
        public static bool IsAllowed(string id)
        {
            if(string.IsNullOrEmpty(id)||id.Length<2||!int.TryParse(id.Substring(0,2),out int number))return false;
            return number==1||number==2||number==3||number==4||number==5||number==6||number==7||number==10
                ||number==14||number==15||number==16||number==17||number==18||number==20;
        }
        public void Configure(TrafficVehicle[] models,float scale)
        {
            if(models==null||models.Length==0||scale<=0||float.IsNaN(scale)||float.IsInfinity(scale))throw new ArgumentException("Valid parking fleet required.");
            foreach(var car in models)if(car==null||!IsAllowed(car.DesignId))throw new ArgumentException("Truck/large van is not permitted as a store visitor.");
            vehicles=(TrafficVehicle[])models.Clone();nativeToShopScale=scale;
        }
        public TrafficVehicle NextFitting(ParkingBay bay,ref int cursor)
        {
            for(int i=0;i<vehicles.Length;i++)
            {
                var car=vehicles[cursor++%vehicles.Length];
                if(car!=null && IsAllowed(car.DesignId) && bay.Fits(car.Width*nativeToShopScale,car.Length*nativeToShopScale))return car;
            }
            return null;
        }
    }
}
