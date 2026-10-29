using System;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Animates the two landing door pairs. The cabin controller alone requests passage or closure.</summary>
    public sealed class LiftLandingDoors : MonoBehaviour
    {
        [Serializable]
        public sealed class Landing
        {
            [SerializeField] private Transform left,right;
            private Vector3 leftClosed,rightClosed;
            public Transform Left=>left;
            public Transform Right=>right;
            public Landing(Transform left,Transform right){this.left=left;this.right=right;}
            internal void Initialize()
            {
                if(left==null||right==null)throw new InvalidOperationException("Both lift door leaves must be configured.");
                leftClosed=left.localPosition;rightClosed=right.localPosition;
            }
            internal void Pose(float amount,float travel)
            {
                float offset=Mathf.SmoothStep(0,1,amount)*travel;
                left.localPosition=leftClosed+Vector3.left*offset;right.localPosition=rightClosed+Vector3.right*offset;
            }
        }
        [SerializeField] private Landing[] landings=Array.Empty<Landing>();
        [SerializeField] private float travel=.27f,openingSpeed=3f;
        public float DoorPlane=>doorPlane;
        [SerializeField] private float doorPlane;
        private readonly float[] progress=new float[2];
        private int requested=-1;
        private float lastRequest;
        public bool AllClosed=>progress.All(value=>value<=.01f);
        public bool IsOpen(int floor)=>floor>=0 && floor<progress.Length && progress[floor]>=.99f;
        public void Configure(Landing[] pairs,float openingTravel,float entrancePlane)
        {
            if(pairs==null||pairs.Length!=2||pairs.Any(pair=>pair==null)||openingTravel<=0)throw new ArgumentException("A two-stop lift requires two valid door pairs and a positive travel.");
            landings=pairs;travel=openingTravel;doorPlane=entrancePlane;
        }
        private void Awake()
        {
            if(landings.Length!=2)throw new InvalidOperationException("Lift landing doors require exactly two stops.");
            foreach(var landing in landings)landing.Initialize();
        }
        public void Request(int floor)
        {requested=floor>=0&&floor<2?floor:-1;lastRequest=Time.time;}
        private void Update()
        {
            int target=Time.time-lastRequest<.5f?requested:-1;
            for(int i=0;i<landings.Length;i++)
            {
                progress[i]=Mathf.MoveTowards(progress[i],i==target?1:0,Time.deltaTime*openingSpeed);
                landings[i].Pose(progress[i],travel);
            }
        }
    }
}
