using System;
using System.Linq;
using System.Text.Json;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Parking;

internal static class CityDomainTests
{
    private static int assertions;
    private static void Require(bool condition,string message)
    { assertions++;if(!condition) throw new Exception(message); }
    private static void Throws(Action action,string message)
    { bool threw=false;try{action();}catch(ArgumentException){threw=true;}catch(InvalidOperationException){threw=true;}Require(threw,message); }
    public static void Main()
    {
        var ledger=new DeliveryLedger();ledger.Order("product_bread",DeliveryItemKind.Product,100);
        Require(ledger.Count("product_bread",DeliveryItemKind.Product,DeliveryLocation.Depot)==100,"An order must await collection.");
        Require(ledger.Count("product_bread",DeliveryItemKind.Product,DeliveryLocation.Pickup)==0,"Ordering must not put goods in the car.");
        Require(!ledger.Transfer("product_bread",DeliveryItemKind.Product,DeliveryLocation.Depot,DeliveryLocation.Pickup,101),"Overcollection must fail.");
        Require(ledger.Transfer("product_bread",DeliveryItemKind.Product,DeliveryLocation.Depot,DeliveryLocation.Pickup,10),"Collect a real package.");
        var snapshot=ledger.Capture();snapshot.entries[0].quantity=999;
        Require(ledger.Count("product_bread",DeliveryItemKind.Product,DeliveryLocation.Depot)==90,"Snapshot mutation must not alter live paid quantities.");
        var publicEntry=ledger.Entries[0];publicEntry.quantity=888;
        Require(ledger.Count("product_bread",DeliveryItemKind.Product,DeliveryLocation.Depot)==90,"Public entries must not expose mutable live state.");
        var options=new JsonSerializerOptions{IncludeFields=true};
        var saved=JsonSerializer.Serialize(ledger.Capture(),options);
        var restored=new DeliveryLedger();restored.Restore(JsonSerializer.Deserialize<DeliverySaveData>(saved,options));
        Require(restored.Count("product_bread",DeliveryItemKind.Product,DeliveryLocation.Pickup)==10,"In-transit cargo must survive saving.");
        Require(restored.Deliver("product_bread",DeliveryItemKind.Product,10),"Unload collected stock.");
        Require(!restored.Deliver("product_bread",DeliveryItemKind.Product,10),"Repeated unloading must not duplicate goods.");
        Require(restored.Count("product_bread",DeliveryItemKind.Product,DeliveryLocation.Depot)==90,"Unloading must not affect uncollected orders.");
        restored.Order("wall_01",DeliveryItemKind.Building,3);
        Require(restored.Count("wall_01",DeliveryItemKind.Building,DeliveryLocation.Depot)==3,"Buildings use the same delivery path.");
        Throws(()=>restored.Order("",DeliveryItemKind.Product,1),"Reject an empty ID.");
        Throws(()=>restored.Order("x",DeliveryItemKind.Product,0),"Reject zero quantity.");
        Throws(()=>restored.Restore(new DeliverySaveData{version=9}),"Reject an unsupported save before replacing live quantities.");
        Require(restored.Count("wall_01",DeliveryItemKind.Building,DeliveryLocation.Depot)==3,"Failed migration must leave paid stock intact.");
        restored.Restore(null);Require(restored.Entries.Count==0,"Old saves without delivery data start with no new shipments.");
        var capacity=new CargoCapacity(350,2);
        Require(capacity.Fits(0,0,350,2),"An exact fit is valid.");
        Require(!capacity.Fits(300,.5f,51,.1f),"Reject overweight cargo even if volume is free.");
        Require(!capacity.Fits(5,1.8f,5,.3f),"Reject bulky cargo even if mass is free.");
        Require(!capacity.Fits(0,0,float.NaN,1),"Invalid weights cannot bypass capacity.");
        Require(!capacity.Fits(0,0,-1,1),"Negative weights cannot bypass capacity.");
        Throws(()=>new CargoCapacity(0,2),"Reject invalid configuration.");
        var parking=new ParkingReservations();Require(parking.Reserve(1),"Reserve an empty bay.");
        Require(!parking.Reserve(1),"Two cars cannot reserve the same bay.");Require(parking.Reserve(2),"Other bays stay available.");
        Require(parking.BeginManeuver(1),"The first car can enter.");Require(!parking.BeginManeuver(2),"Crossing maneuvers must not overlap.");
        parking.EndManeuver(2);Require(!parking.BeginManeuver(2),"Another bay cannot release an active maneuver.");
        parking.EndManeuver(1);Require(parking.BeginManeuver(2),"The next car can move after the driveway is clear.");
        parking.Release(2);Require(!parking.IsReserved(2),"Leaving releases the bay.");
        Require(!parking.BeginManeuver(2),"An unreserved bay cannot own the driveway.");
        var random=new Random(41);var conservation=new DeliveryLedger();int total=0,delivered=0;
        for(int i=0;i<1000;i++)
        {
            int operation=random.Next(3),quantity=random.Next(1,11);
            if(operation==0){conservation.Order("x",DeliveryItemKind.Product,quantity);total+=quantity;}
            if(operation==1)conservation.Transfer("x",DeliveryItemKind.Product,DeliveryLocation.Depot,DeliveryLocation.Pickup,quantity);
            if(operation==2&&conservation.Deliver("x",DeliveryItemKind.Product,quantity)) delivered+=quantity;
            Require(total==delivered+conservation.Entries.Sum(e=>e.quantity),"Paid units must be conserved across every delivery step.");
        }
        Console.WriteLine($"CITY DOMAIN TESTS PASSED: {assertions} assertions.");
    }
}
