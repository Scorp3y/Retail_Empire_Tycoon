using System;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    public enum JunctionSignalAspect { Red, RedAmber, Green, Amber }

    public readonly struct JunctionSignalState : IEquatable<JunctionSignalState>
    {
        public JunctionSignalAspect NorthSouth { get; }
        public JunctionSignalAspect EastWest { get; }
        public bool PedestrianWalk { get; }

        public JunctionSignalState(JunctionSignalAspect northSouth, JunctionSignalAspect eastWest, bool walk=false)
        {
            NorthSouth=northSouth;EastWest=eastWest;PedestrianWalk=walk;
        }

        public bool Equals(JunctionSignalState other)=>NorthSouth==other.NorthSouth&&EastWest==other.EastWest&&PedestrianWalk==other.PedestrianWalk;
    }

    /// <summary>
    /// Owns one junction's signal cycle. Opposing approaches share an axis; pedestrians have an exclusive
    /// all-vehicle-red phase and a clearance interval. Does not move, brake or fine player/NPC vehicles.
    /// </summary>
    public sealed class TownJunctionSignals : MonoBehaviour
    {
        [Serializable]
        public sealed class LampGroup
        {
            [SerializeField] private GameObject red;
            [SerializeField] private GameObject amber;
            [SerializeField] private GameObject green;

            public LampGroup(GameObject red,GameObject amber,GameObject green)
            {
                this.red=red;this.amber=amber;this.green=green;
            }

            public bool IsComplete=>red!=null&&amber!=null&&green!=null;
            public bool Shows(JunctionSignalAspect aspect)=>red.activeSelf==(aspect==JunctionSignalAspect.Red||aspect==JunctionSignalAspect.RedAmber)
                &&amber.activeSelf==(aspect==JunctionSignalAspect.Amber||aspect==JunctionSignalAspect.RedAmber)
                &&green.activeSelf==(aspect==JunctionSignalAspect.Green);

            internal void Display(JunctionSignalAspect aspect)
            {
                red.SetActive(aspect==JunctionSignalAspect.Red||aspect==JunctionSignalAspect.RedAmber);
                amber.SetActive(aspect==JunctionSignalAspect.Amber||aspect==JunctionSignalAspect.RedAmber);
                green.SetActive(aspect==JunctionSignalAspect.Green);
            }
        }

        public const double GreenSeconds=20;
        public const double CycleSeconds=64;
        [SerializeField] private LampGroup northSouth;
        [SerializeField] private LampGroup eastWest;
        [SerializeField] private GameObject pedestrianRed;
        [SerializeField] private GameObject pedestrianGreen;
        [SerializeField] private float phaseOffset;
        private bool hasDisplayed;
        public JunctionSignalState Current { get; private set; }
        public float PhaseOffset=>phaseOffset;
        public bool IsConfigured=>northSouth!=null&&northSouth.IsComplete&&eastWest!=null&&eastWest.IsComplete&&pedestrianRed!=null&&pedestrianGreen!=null;
        public bool LampsMatchState=>IsConfigured&&northSouth.Shows(Current.NorthSouth)&&eastWest.Shows(Current.EastWest)
            &&pedestrianGreen.activeSelf==Current.PedestrianWalk&&pedestrianRed.activeSelf!=Current.PedestrianWalk;

        public void Configure(LampGroup northSouth,LampGroup eastWest,GameObject pedestrianRed,GameObject pedestrianGreen,float offset)
        {
            if(northSouth==null||!northSouth.IsComplete||eastWest==null||!eastWest.IsComplete||pedestrianRed==null||pedestrianGreen==null)
                throw new ArgumentException("A junction requires complete vehicle and pedestrian lamp groups.");
            if(float.IsNaN(offset)||float.IsInfinity(offset))throw new ArgumentOutOfRangeException(nameof(offset));
            this.northSouth=northSouth;this.eastWest=eastWest;this.pedestrianRed=pedestrianRed;this.pedestrianGreen=pedestrianGreen;
            phaseOffset=Mathf.Repeat(offset,(float)CycleSeconds);hasDisplayed=false;DisplayAt(phaseOffset);
        }

        private void OnEnable(){hasDisplayed=false;}

        /// <summary>Deterministic signal preview and runtime update; non-finite time fails closed to all red.</summary>
        public void DisplayAt(double elapsedSeconds)
        {
            if(!IsConfigured)throw new InvalidOperationException("Junction lamp bindings are incomplete.");
            var state=Evaluate(elapsedSeconds);
            if(hasDisplayed&&Current.Equals(state))return;
            Current=state;hasDisplayed=true;
            northSouth.Display(state.NorthSouth);eastWest.Display(state.EastWest);
            pedestrianRed.SetActive(!state.PedestrianWalk);pedestrianGreen.SetActive(state.PedestrianWalk);
        }

        /// <summary>20 s green per axis, 3 s amber, all-red interlocks and an exclusive pedestrian phase.
        /// TownSignalChunks supplies time only to junctions near the observer; there is no per-junction Update.</summary>
        public static JunctionSignalState Evaluate(double elapsedSeconds)
        {
            const JunctionSignalAspect red=JunctionSignalAspect.Red;
            if(double.IsNaN(elapsedSeconds)||double.IsInfinity(elapsedSeconds))return new JunctionSignalState(red,red);
            double t=(elapsedSeconds%CycleSeconds+CycleSeconds)%CycleSeconds;
            if(t<20)return new JunctionSignalState(JunctionSignalAspect.Green,red);
            if(t<23)return new JunctionSignalState(JunctionSignalAspect.Amber,red);
            if(t<25)return new JunctionSignalState(red,red);
            if(t<26)return new JunctionSignalState(red,JunctionSignalAspect.RedAmber);
            if(t<46)return new JunctionSignalState(red,JunctionSignalAspect.Green);
            if(t<49)return new JunctionSignalState(red,JunctionSignalAspect.Amber);
            if(t<51)return new JunctionSignalState(red,red);
            if(t<58)return new JunctionSignalState(red,red,true);
            if(t<63)return new JunctionSignalState(red,red);
            return new JunctionSignalState(JunctionSignalAspect.RedAmber,red);
        }
    }
}
