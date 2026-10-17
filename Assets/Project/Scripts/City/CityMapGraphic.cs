using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.City
{
    /// <summary>Draws the actual authored road graph, not an unrelated decorative map texture.</summary>
    public sealed class CityMapGraphic : Graphic
    {
        public TownWorldPlaces town;
        public PickupDrive pickup;
        public DeliveryCheckpoint home,destination;
        private TownWorldPlaces cachedTown;
        private TownForestMapData forestDistrict;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(town==null||town.layout==null)return;
            var layout=town.layout;
            // Resolve once per map/town, not by scanning the large scenery hierarchy every UI frame.
            if(cachedTown!=town){cachedTown=town;forestDistrict=town.GetComponentInChildren<TownForestMapData>();}
            AddRect(mesh,layout.cityBounds,new Color(.48f,.64f,.37f));
            foreach(var district in layout.districts)AddRect(mesh,district.bounds,district.color);
            if(forestDistrict!=null)AddRect(mesh,forestDistrict.DistrictBounds,new Color(.51f,.65f,.40f));
            AddRect(mesh,layout.reservedShopPlot,new Color(.81f,.79f,.62f));
            foreach(var road in layout.roads)
            {
                var p=road.Position(layout.roadTileSize);
                AddRect(mesh,new Rect(p.x-5,p.z-5,10,10),new Color(.2f,.25f,.23f));
            }
            if(forestDistrict!=null)
                foreach(var road in forestDistrict.RoadAreas)AddRect(mesh,road,new Color(.2f,.25f,.23f));
            AddPath(mesh,layout.countryRoad,new Color(.2f,.25f,.23f));
            AddPath(mesh,layout.railway,new Color(.78f,.68f,.48f));
            AddPath(mesh,layout.freightSiding,new Color(.78f,.68f,.48f));
            for(float z=layout.cityBounds.yMin;z<layout.cityBounds.yMax;z+=6)
                AddRect(mesh,new Rect(layout.RiverCenter(z)-14,z,28,6),new Color(.25f,.55f,.72f));
            // Redraw transport over the river so bridges stay visible on the same authored map.
            AddPath(mesh,layout.countryRoad,new Color(.2f,.25f,.23f));
            AddPath(mesh,layout.railway,new Color(.78f,.68f,.48f));
            foreach(var checkpoint in town.suppliers)Marker(mesh,checkpoint.transform.position,5,new Color(.93f,.72f,.16f));
            if(home!=null)Marker(mesh,home.transform.position,6,new Color(.85f,.31f,.23f));
            if(destination!=null)Marker(mesh,destination.transform.position,9,new Color(.95f,.94f,.8f));
            if(pickup!=null)Marker(mesh,pickup.transform.position,6,new Color(.18f,.49f,.87f));
        }
        private Vector2 Map(Vector2 position)
        {
            var bounds=town.layout.cityBounds;var rect=rectTransform.rect;
            return new Vector2(rect.xMin+(position.x-bounds.xMin)/bounds.width*rect.width,
                rect.yMin+(position.y-bounds.yMin)/bounds.height*rect.height);
        }
        private void AddPath(VertexHelper mesh,TownTransportPath path,Color color)
        {
            if(path?.points==null)return;
            for(int i=1;i<path.points.Length;i++)
            {
                var a=new Vector2(path.points[i-1].x,path.points[i-1].z);
                var b=new Vector2(path.points[i].x,path.points[i].z);
                var side=new Vector2(-(b-a).y,(b-a).x).normalized*path.width/2;
                int start=mesh.currentVertCount;
                foreach(var point in new[]{a-side,a+side,b+side,b-side})mesh.AddVert(Map(point),color,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
        }
        private void AddRect(VertexHelper mesh,Rect rect,Color color)
        {
            AddQuad(mesh,Map(new Vector2(rect.xMin,rect.yMin)),Map(new Vector2(rect.xMax,rect.yMax)),color);
        }
        private void Marker(VertexHelper mesh,Vector3 position,float pixels,Color color)
        {
            var center=Map(new Vector2(position.x,position.z));AddQuad(mesh,center-Vector2.one*pixels/2,center+Vector2.one*pixels/2,color);
        }
        private static void AddQuad(VertexHelper mesh,Vector2 min,Vector2 max,Color color)
        {
            int start=mesh.currentVertCount;
            mesh.AddVert(new Vector3(min.x,min.y),color,Vector2.zero);mesh.AddVert(new Vector3(min.x,max.y),color,Vector2.zero);
            mesh.AddVert(new Vector3(max.x,max.y),color,Vector2.zero);mesh.AddVert(new Vector3(max.x,min.y),color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
