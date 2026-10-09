using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetailEmpireTycoon.City
{
    public sealed class PickupCamera : MonoBehaviour
    {
        public Transform target;
        public float mouseSensitivity=3;
        public float distance=8;
        public bool ReadMouse=true;
        public float Yaw {get;private set;}
        public float Pitch {get;private set;}=20;
        private Vector3 velocity;
        private bool initialized,blocked,freeCursor;
        private float lastLook;
        private CursorLockMode previousLock;
        private bool previousVisible;
        private void OnEnable() {previousLock=Cursor.lockState;previousVisible=Cursor.visible;ApplyCursor();}
        public void SetInteractionBlocked(bool value) {blocked=value;ApplyCursor();}
        public void Orbit(float horizontal,float vertical)
        {
            Initialize();Yaw=Mathf.Repeat(Yaw+horizontal,360);Pitch=Mathf.Clamp(Pitch+vertical,12,65);lastLook=Time.unscaledTime;
        }
        public void ResetBehindVehicle() {if(target==null)return;Yaw=target.eulerAngles.y;Pitch=20;initialized=true;}
        private void Initialize() {if(!initialized && target!=null)ResetBehindVehicle();}
        private void Update()
        {
            Initialize();if(!ReadMouse||blocked)return;
            if(Input.GetKeyDown(KeyCode.Escape)) {freeCursor=true;ApplyCursor();}
            if(freeCursor && Input.GetMouseButtonDown(0) && (EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject()))
            {freeCursor=false;ApplyCursor();}
            if(freeCursor)return;
            float x=Input.GetAxisRaw("Mouse X"),y=Input.GetAxisRaw("Mouse Y");
            if(Mathf.Abs(x)+Mathf.Abs(y)>.001f)Orbit(x*mouseSensitivity,-y*mouseSensitivity);
            distance=Mathf.Clamp(distance-Input.GetAxis("Mouse ScrollWheel")*4,4,12);
            if(Input.GetKeyDown(KeyCode.V))ResetBehindVehicle();
        }
        private void LateUpdate()
        {
            if(target==null)return;Initialize();
            var pickup=target.GetComponent<PickupDrive>();
            if(!blocked && Time.unscaledTime-lastLook>4 && pickup!=null && pickup.Speed>2)
                Yaw=Mathf.LerpAngle(Yaw,target.eulerAngles.y,1-Mathf.Exp(-Time.unscaledDeltaTime));
            Vector3 look=target.position+Vector3.up*1.5f;
            Vector3 direction=-(Quaternion.Euler(Pitch,Yaw,0)*Vector3.forward);
            var hit=Physics.SphereCastAll(look,.25f,direction,distance,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.transform!=target&&!h.transform.IsChildOf(target)).OrderBy(h=>h.distance).FirstOrDefault();
            float reach=hit.collider!=null?Mathf.Max(.8f,hit.distance-.3f):distance;
            transform.position=Vector3.SmoothDamp(transform.position,look+direction*reach,ref velocity,.1f);
            transform.LookAt(look);
        }
        private void ApplyCursor()
        {
            if(Application.isBatchMode)return;
            bool locked=!blocked&&!freeCursor;
            Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;
        }
        private void OnApplicationFocus(bool focused) {if(!focused){freeCursor=true;ApplyCursor();}}
        private void OnDisable() {if(!Application.isBatchMode){Cursor.lockState=previousLock;Cursor.visible=previousVisible;}}
    }
}
