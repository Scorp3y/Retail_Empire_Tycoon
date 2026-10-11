using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Visual-only pedestrian. Authored local routes never reserve parking or alter customer simulation.</summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class TownAmbientWalker : MonoBehaviour
    {
        [SerializeField] private Vector3[] route;
        [SerializeField] private Mesh[] poses;
        [SerializeField] private float metresPerSecond=.8f;
        [SerializeField] private float animationOffset;
        [SerializeField] private float pauseAtRouteEnds;
        private MeshFilter visual;
        private int destination=1;
        private int direction=1;
        private float remainingPause;
        public bool IsWaiting => remainingPause>0;

        /// <summary>Copies an authored local route. Optional endpoint pauses keep waiting pedestrians off vehicle roads.</summary>
        public void Configure(Vector3[] localRoute,Mesh[] animationPoses,float speed,float offset,float endpointPause=0)
        {
            if(!ValidConfiguration(localRoute,animationPoses,speed)||!Finite(offset)||!Finite(endpointPause)||endpointPause<0)
                throw new System.ArgumentException("A pedestrian needs at least two route points, three poses and a positive speed.");
            route=(Vector3[])localRoute.Clone();poses=(Mesh[])animationPoses.Clone();metresPerSecond=speed;animationOffset=offset;
            pauseAtRouteEnds=endpointPause;destination=1;direction=1;remainingPause=0;
        }
        private void Awake()
        {
            visual=GetComponent<MeshFilter>();
            if(!ValidConfiguration(route,poses,metresPerSecond)||!Finite(animationOffset)||!Finite(pauseAtRouteEnds)||pauseAtRouteEnds<0)
            {enabled=false;Debug.LogError("Ambient pedestrian route or poses are missing.",this);}
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static bool ValidConfiguration(Vector3[] points,Mesh[] frames,float speed)
        {
            if(points==null||points.Length<2||frames==null||frames.Length!=3||!Finite(speed)||speed<=0)return false;
            foreach(var frame in frames)if(frame==null)return false;
            for(int i=0;i<points.Length;i++)
            {
                if(!Finite(points[i].x)||!Finite(points[i].y)||!Finite(points[i].z))return false;
                if(i>0&&(points[i]-points[i-1]).sqrMagnitude<.0001f)return false;
            }
            return true;
        }
        private void Update()
        {
            if(IsWaiting)
            {
                remainingPause=Mathf.Max(0,remainingPause-Time.deltaTime);
                if(visual.sharedMesh!=poses[0])visual.sharedMesh=poses[0];
                return;
            }
            Vector3 delta=route[destination]-transform.localPosition;
            transform.localPosition=Vector3.MoveTowards(transform.localPosition,route[destination],metresPerSecond*Time.deltaTime);
            if(delta.sqrMagnitude>.0025f)transform.localRotation=Quaternion.RotateTowards(transform.localRotation,Quaternion.LookRotation(delta),160*Time.deltaTime);
            if((route[destination]-transform.localPosition).sqrMagnitude<.0001f)
            {
                if(destination==route.Length-1){direction=-1;remainingPause=pauseAtRouteEnds;}
                else if(destination==0){direction=1;remainingPause=pauseAtRouteEnds;}
                destination+=direction;
            }
            int phase=Mathf.FloorToInt((Time.time+animationOffset)*5)%4;
            var pose=poses[phase==1?1:phase==3?2:0];
            if(visual.sharedMesh!=pose)visual.sharedMesh=pose;
        }
    }
}
