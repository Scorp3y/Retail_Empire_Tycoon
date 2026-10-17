using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Small, bounded pasture behaviour. No update work is performed for distant animals.
    /// Coordinates stay town-local, including the reduced-scale store view.</summary>
    public sealed class FarmAnimal : MonoBehaviour
    {
        public Transform head,tail;public Transform[] legs=System.Array.Empty<Transform>();
        public bool chicken;public Rect pasture;public int seed;
        private Vector3 home,target;private float nextDecision,nextTick,phase;private bool walking;
        private Camera observer;
        public bool IsWalking=>walking;
        private void Awake(){home=transform.localPosition;target=home;phase=seed*.71f;nextDecision=Time.time+1+seed%4;}
        private void Update()
        {
            if(Time.time<nextTick)return;nextTick=Time.time+1f/15;
            if(observer==null)observer=Camera.main;
            if(observer!=null&&(transform.parent.InverseTransformPoint(observer.transform.position)-transform.localPosition).sqrMagnitude>180*180)return;
            Advance(1f/15,Time.time);
        }
        public void Advance(float seconds,float elapsed)
        {
            if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            if(elapsed>=nextDecision)
            {
                phase+=1.37f;walking=!walking;nextDecision=elapsed+(walking?3:4)+Mathf.Abs(Mathf.Sin(phase))*3;
                float radius=chicken?.65f:1.7f;
                target=home+new Vector3(Mathf.Sin(phase)*radius,0,Mathf.Cos(phase*1.7f)*radius);
                target.x=Mathf.Clamp(target.x,pasture.xMin,pasture.xMax);target.z=Mathf.Clamp(target.z,pasture.yMin,pasture.yMax);
            }
            if(walking)
            {
                var delta=target-transform.localPosition;delta.y=0;
                if(delta.sqrMagnitude>.02f){transform.localRotation=Quaternion.RotateTowards(transform.localRotation,Quaternion.LookRotation(delta),seconds*55);transform.localPosition=Vector3.MoveTowards(transform.localPosition,target,seconds*(chicken?.3f:.38f));}
                else walking=false;
            }
            float gait=elapsed*(chicken?12:5)+phase;
            for(int i=0;i<legs.Length;i++)if(legs[i]!=null)legs[i].localRotation=Quaternion.Euler(walking?Mathf.Sin(gait+i%2*Mathf.PI+(i>1?Mathf.PI:0))*22:0,0,0);
            if(head!=null)head.localRotation=Quaternion.Euler(walking?Mathf.Sin(gait)*3:Mathf.Lerp(5,chicken?45:38,(Mathf.Sin(elapsed*(chicken?4:.7f)+phase)+1)/2),Mathf.Sin(elapsed*.5f+phase)*5,0);
            if(tail!=null)tail.localRotation=Quaternion.Euler(0,Mathf.Sin(elapsed*2+phase)*7,chicken?0:Mathf.Sin(elapsed*2+phase)*22);
        }
    }
}
