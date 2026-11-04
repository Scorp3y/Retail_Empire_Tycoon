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
        private PickupDrive workshopVehicle;
        private WorkshopCategory workshopAngle;
        private bool otherSide;
        private Camera workshopLens;
        private float normalFieldOfView;
        private void OnEnable() {previousLock=Cursor.lockState;previousVisible=Cursor.visible;ApplyCursor();}
        public void SetInteractionBlocked(bool value) {blocked=value;ApplyCursor();}
        public void Orbit(float horizontal,float vertical)
        {
            Initialize();Yaw=Mathf.Repeat(Yaw+horizontal,360);Pitch=Mathf.Clamp(Pitch+vertical,12,65);lastLook=Time.unscaledTime;
        }
        public void ResetBehindVehicle() {if(target==null)return;Yaw=target.eulerAngles.y;Pitch=20;initialized=true;}
        public void EnterWorkshop(PickupDrive vehicle)
        {
            workshopVehicle = vehicle;
            otherSide = false;
            if (TryGetComponent<Camera>(out workshopLens)) normalFieldOfView = workshopLens.fieldOfView;
            SetInteractionBlocked(true);
        }
        public void ShowWorkshopAngle(WorkshopCategory angle) => workshopAngle = angle;
        public void FlipWorkshopView() => otherSide = !otherSide;
        public void ExitWorkshop()
        {
            workshopVehicle = null;
            if (workshopLens != null) workshopLens.fieldOfView = normalFieldOfView;
            workshopLens = null;
            velocity = Vector3.zero;
            ResetBehindVehicle();
            SetInteractionBlocked(false);
        }
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
            if (workshopVehicle != null) { RenderWorkshop(); return; }
            var pickup=target.GetComponent<PickupDrive>();
            if(!blocked && Time.unscaledTime-lastLook>4 && pickup!=null && pickup.Speed>2)
                Yaw=Mathf.LerpAngle(Yaw,target.eulerAngles.y,1-Mathf.Exp(-Time.unscaledDeltaTime));
            Vector3 look=target.position+Vector3.up*(pickup!=null?pickup.CameraHeight:1.5f);
            Vector3 direction=-(Quaternion.Euler(Pitch,Yaw,0)*Vector3.forward);
            var hit=Physics.SphereCastAll(look,.25f,direction,distance,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.transform!=target&&!h.transform.IsChildOf(target)).OrderBy(h=>h.distance).FirstOrDefault();
            float reach=hit.collider!=null?Mathf.Max(.8f,hit.distance-.3f):distance;
            transform.position=Vector3.SmoothDamp(transform.position,look+direction*reach,ref velocity,.1f);
            transform.LookAt(look);
        }
        private void RenderWorkshop()
        {
            var body = workshopVehicle.GetComponent<BoxCollider>();
            var car = workshopVehicle.transform;
            var focus = car.TransformPoint(body.center);
            float length = body.size.z;
            float height = body.size.y;
            float largeVehicle = Mathf.InverseLerp(5.4f, 8.5f, length);
            Vector3 view = workshopAngle switch
            {
                WorkshopCategory.Engine => new Vector3(.80f, .30f, .69f),
                WorkshopCategory.Handling => new Vector3(1f, .22f, .21f),
                WorkshopCategory.Cargo => new Vector3(.83f, .34f, -.64f),
                _ => new Vector3(.94f, .40f, .49f)
            };
            if (otherSide) view.x *= -1;
            var ray = car.TransformDirection(view.normalized);
            float distance = Mathf.Max(5.35f, length * .87f);
            var hit = Physics.SphereCastAll(focus, .22f, ray, distance, ~0, QueryTriggerInteraction.Ignore)
                .Where(item => !item.transform.IsChildOf(car)).OrderBy(item => item.distance).FirstOrDefault();
            if (hit.collider != null)
            {
                distance = Mathf.Max(1.35f, hit.distance - .24f);
            }
            Vector3 position = focus + ray * distance;
            position.y = Mathf.Max(position.y, car.position.y + height * .6f + .32f);
            Vector3 right = Vector3.Cross(Vector3.up, focus - position).normalized;
            Vector3 aim = focus - right * distance * Mathf.Lerp(.20f, .16f, largeVehicle);
            transform.position = Vector3.SmoothDamp(transform.position, position, ref velocity, .16f, Mathf.Infinity, Time.unscaledDeltaTime);
            float blend = 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aim - position),
                blend);
            if (workshopLens != null)
                workshopLens.fieldOfView = Mathf.Lerp(workshopLens.fieldOfView, normalFieldOfView + 13 * largeVehicle, blend);
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
