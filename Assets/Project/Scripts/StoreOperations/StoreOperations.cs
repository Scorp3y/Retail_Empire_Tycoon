using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Scene boundary for trading, visits, wages and employee duties. No offline charges.</summary>
    public sealed class StoreOperations : MonoBehaviour
    {
        [SerializeField] private ShopBalance balance;
        [SerializeField] private MoneyController money;
        [SerializeField] private ProductInventory warehouse;
        [SerializeField] private BuildController building;
        [SerializeField] private StoreProgression progression;
        [SerializeField] private WorkMinigame work;
        [SerializeField] private GameObject[] customerModels;
        [SerializeField] private GameObject employeeModel;
        [SerializeField] private GameObject registerModel;
        [SerializeField] private Material dirtMaterial;
        private readonly StaffRoster _staff = new StaffRoster();
        private readonly List<ShopperVisit> _visits = new List<ShopperVisit>();
        private readonly List<EmployeeDuty> _employees = new List<EmployeeDuty>();
        private readonly List<ShopDirtSpot> _dirt = new List<ShopDirtSpot>();
        private readonly HashSet<PlacedShelfStock> _restocking = new HashSet<PlacedShelfStock>();
        private float _arrival, _wages, _refresh;
        private float _reputation = 3;
        private int _completedSales;
        private bool _ready;
        private bool _refreshWorld;
        private bool _wasBuilding;
        private GameObject _register;
        private Vector3 _entry, _arrivalPoint, _staffRestPoint, _checkout;
        private StoreLevelId _worldLevel;
        public StaffRoster Staff => _staff;
        public ShopBalance Balance => balance;
        public ProductInventory Warehouse => warehouse;
        public StoreNavigation Navigation { get; private set; }
        public IReadOnlyList<ShopperVisit> Visits => _visits;
        public IReadOnlyList<ShopDirtSpot> Dirt => _dirt;
        public bool IsOpen { get; private set; }
        public bool StaffWorking { get; private set; } = true;
        public int Level => 1 + (progression != null ? progression.Purchased.Count : 0);
        public float Rating { get; private set; } = 3;
        public string Notice { get; private set; } = "Магазин закрыт. Заполни полки и открой его.";
        public Vector3 CheckoutPosition => _checkout;
        public PlacedObject CheckoutObject { get; private set; }
        public Vector3 ArrivalPosition => _arrivalPoint;
        public Vector3 EntrancePosition => _entry;
        public bool WorkActive => work != null && work.IsActive;
        public int Money => money.Money;
        public bool CanAffordHire(StaffRole role) => money.Money >= balance.HireCost(role);

        private void Awake()
        {
            if (balance == null || money == null || warehouse == null || building == null || building.grid == null || building.territory == null)
            { Debug.LogError("Store operations are not configured. Run Retail Empire/Shop/Configure gameplay.", this); enabled = false; return; }
            Navigation = new StoreNavigation(building.grid, building.territory);
        }
        private void Start() { _arrival = 5; }
        private void Update() { Advance(Time.deltaTime); }
        public void Advance(float seconds)
        {
            if (seconds <= 0 || Navigation == null) return;
            if (!_ready || _refreshWorld || progression != null && _worldLevel != progression.State.CurrentLevel)
            {
                if (!ConfigureWorld()) return;
            }
            if (building.mode == BuildMode.Build) { _wasBuilding = true; return; }
            if (_wasBuilding) { _refreshWorld = true; _wasBuilding = false; return; }
            if (WorkActive) return;
            _refresh += seconds;
            if (_refresh >= 2) { _refresh = 0; RefreshRating(); }
            foreach (var visit in _visits.ToArray()) AdvanceVisit(visit, seconds);
            if (IsOpen)
            {
                _arrival -= seconds;
                if (_arrival <= 0) { _arrival = balance.ArrivalSeconds(Level, Rating); TrySpawnCustomer(); }
                ChargeWages(seconds);
            }
            foreach (var employee in _employees.ToArray()) if (employee != null) employee.Advance(seconds, IsOpen && StaffWorking);
            _dirt.RemoveAll(d => d == null);
        }
        private bool ConfigureWorld()
        {
            if (building.territory.PurchasedRects.Count == 0) return false;
            var rect = building.territory.PurchasedRects[0];
            var front = building.grid.CellToWorld(new Vector3Int((rect.min.x + rect.max.x) / 2, 0, rect.min.z + 2));
            var placedObjects = FindObjectsOfType<PlacedObject>();
            Navigation.RefreshDoorways(placedObjects);
            var door = placedObjects.Where(p => p.item != null && p.item.isDoorway).OrderBy(p => (p.transform.position - front).sqrMagnitude).FirstOrDefault();
            Vector3 inward = Vector3.forward;
            if (door != null)
            {
                front = door.transform.position;
                var shelves = FindObjectsOfType<PlacedShelfStock>();
                if (shelves.Length > 0)
                {
                    Vector3 center = Vector3.zero; foreach (var shelf in shelves) center += shelf.transform.position;
                    Vector3 toInterior = center / shelves.Length - front;
                    inward = Vector3.Dot(toInterior, door.transform.forward) >= 0 ? door.transform.forward : -door.transform.forward;
                }
            }
            if (!Navigation.TryNearest(front, out _entry)) { Notice = "Вход заблокирован — освободи проход."; return false; }
            if (!Navigation.TryNearest(front - inward * .8f, out _arrivalPoint, true)) return false;
            Vector3 lateral = Vector3.Cross(Vector3.up, inward);
            var placedCheckout = FindAccessibleCheckout(placedObjects);
            bool checkoutFound = placedCheckout != null;
            for (int distance = 0; distance < 4 && !checkoutFound; distance++)
                foreach (int side in new[] { 1, -1 })
                {
                    Vector3 candidate = _entry + inward * (1.2f + distance * .3f) + lateral * side * .9f;
                    if (!Navigation.TryNearest(candidate, out var point) || (point - _entry).magnitude < 1.05f) continue;
                    if (Physics.CheckSphere(point + Vector3.up * .3f, .27f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!Navigation.TryPath(_entry, point, out _)) continue;
                    _checkout = point; checkoutFound = true; break;
                }
            if (!checkoutFound) { Notice = "Нет места для кассы — освободи зону внутри магазина."; return false; }
            CheckoutObject = placedCheckout;
            // Idle employees must wait inside, not be created between the closed door leaves.
            if (!Navigation.TryNearest(_entry + inward * .7f - lateral * .35f, out _staffRestPoint)
                || !Navigation.TryPath(_entry, _staffRestPoint, out _)) _staffRestPoint = _checkout;
            if (_ready)
            {
                work?.Cancel();
                foreach (var visit in _visits.ToArray()) RemoveVisit(visit);
                foreach (var employee in _employees)
                {
                    if (employee == null) continue;
                    employee.CancelDuty();
                    Destroy(employee.gameObject);
                }
                _employees.Clear();
                // Destroy runs at frame end; release job reservations before rebuilding the same layout.
                _restocking.Clear();
                foreach (var dirt in _dirt) if (dirt != null) Destroy(dirt.gameObject);
                _dirt.Clear();
                if (_register != null) Destroy(_register);
            }
            _register = null;
            if (placedCheckout == null)
            {
                // Old saves have no purchased checkout. Keep their existing trading loop available.
                _register = new GameObject("Player checkout station"); _register.transform.SetParent(transform);
                _register.transform.position = _checkout;
                if (registerModel != null)
                {
                    var model = Instantiate(registerModel, _register.transform); model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one * 0.45f;
                    foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
                }
            }
            _worldLevel = progression != null ? progression.State.CurrentLevel : StoreLevelId.Lvl1;
            _ready = true; _refreshWorld = false; RebuildStaff(); return true;
        }
        private PlacedObject FindAccessibleCheckout(IEnumerable<PlacedObject> objects)
        {
            foreach (var placed in objects.Where(p => p.item != null && p.item.isCheckout)
                         .OrderBy(p => (p.transform.position - _entry).sqrMagnitude))
            {
                var bounds = placed.item.placementBounds;
                var facing = Quaternion.Euler(0, placed.item.frontFacing * 90, 0) * Vector3.forward;
                // Measure the front in prefab space; its pivot may not coincide with the counter center.
                float depth = Mathf.Abs(facing.x) * bounds.extents.x + Mathf.Abs(facing.z) * bounds.extents.z;
                Vector3 center = placed.transform.position + placed.transform.rotation * new Vector3(bounds.center.x, 0, bounds.center.z);
                Vector3 candidate = center + placed.transform.rotation * facing * (depth + building.grid.cellSize * 2);
                if (!Navigation.TryNearest(candidate, out var point) || (point - _entry).magnitude < 1.05f) continue;
                if (!Navigation.TryPath(_entry, point, out _)) continue;
                _checkout = point;
                return placed;
            }
            return null;
        }
        public void SetOpen(bool open)
        {
            IsOpen = open; _arrival = Mathf.Min(_arrival, 5);
            Notice = open ? "Магазин открыт. Покупатели идут!" : "Магазин закрыт. Обслужи оставшихся покупателей.";
        }
        public bool TryHire(StaffRole role)
        {
            if (!_staff.CanHire(role, Level)) { Notice = "Лимит найма. Купи следующий участок."; return false; }
            if (!money.TrySpend(balance.HireCost(role))) { Notice = "Не хватает денег на найм."; return false; }
            _staff.TryHire(role, Level); if (_ready) AddEmployee(role);
            Notice = "Сотрудник нанят. Зарплата списывается во время работы магазина."; return true;
        }
        public bool TryDismiss(StaffRole role)
        {
            if (!_staff.TryDismiss(role)) return false;
            var employee = _employees.LastOrDefault(e => e != null && e.Role == role);
            if (employee != null) { employee.CancelDuty(); _employees.Remove(employee); Destroy(employee.gameObject); }
            return true;
        }
        private void RebuildStaff()
        {
            foreach (var employee in _employees) if (employee != null) { employee.CancelDuty(); Destroy(employee.gameObject); }
            _employees.Clear();
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole)))
                for (int i = 0; i < _staff.Count(role); i++) AddEmployee(role);
        }
        private void AddEmployee(StaffRole role)
        {
            var character = SpawnCharacter(employeeModel, _staffRestPoint, role == StaffRole.Guard ? 1.05f : 0.65f);
            if (character == null) return;
            var duty = character.gameObject.AddComponent<EmployeeDuty>(); duty.Initialize(this, character, role); _employees.Add(duty);
        }
        private ShopCharacter SpawnCharacter(GameObject prefab, Vector3 point, float walkingSpeed = 0.65f)
        {
            if (prefab == null) return null;
            var root = new GameObject("Shop character"); root.transform.SetParent(transform); root.transform.position = point;
            var model = Instantiate(prefab, root.transform); model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                model.transform.localScale *= 0.48f / Mathf.Max(0.01f, bounds.size.y);
                bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                model.transform.position += Vector3.up * (point.y - bounds.min.y);
            }
            var character = root.AddComponent<ShopCharacter>(); character.Initialize(Navigation, walkingSpeed);
            var hitArea = root.AddComponent<CapsuleCollider>();
            hitArea.isTrigger = true; hitArea.height = 0.48f; hitArea.radius = 0.085f; hitArea.center = Vector3.up * 0.24f;
            return character;
        }
        public bool TrySpawnCustomer(bool forceThief = false)
        {
            if (!_ready || !IsOpen || customerModels == null || customerModels.Length == 0 || _visits.Count >= 4 + Level * 2) return false;
            var shelves = FindObjectsOfType<PlacedShelfStock>().Where(s => s.CurrentProduct != null && s.CurrentAmount - Reserved(s) > 0).ToArray();
            if (shelves.Length == 0) { Notice = "Покупатели ждут ассортимент: заполни полки."; return false; }
            PlacedShelfStock shelf = null; Vector3 approach = default;
            int first = UnityEngine.Random.Range(0, shelves.Length);
            for (int i = 0; i < shelves.Length; i++)
            {
                var candidate = shelves[(first + i) % shelves.Length];
                if (!TryShelfApproach(candidate, _arrivalPoint, out approach)) continue;
                shelf = candidate; break;
            }
            if (shelf == null) { Notice = "Нет прохода к полкам. Освободи место перед товарами."; return false; }
            var character = SpawnCharacter(customerModels[UnityEngine.Random.Range(0, customerModels.Length)], _arrivalPoint);
            if (character == null) return false;
            if (!character.MoveTo(approach)) { Destroy(character.gameObject); Notice = "Нет прохода к полке. Оставь свободные клетки перед ней."; return false; }
            var visit = new ShopperVisit(character, shelf, Mathf.Min(balance.basketSize, shelf.CurrentAmount - Reserved(shelf)), forceThief || UnityEngine.Random.value < balance.thiefChance, balance.thiefClicks);
            _visits.Add(visit); return true;
        }
        public bool TryShelfApproach(PlacedShelfStock shelf, Vector3 start, out Vector3 approach)
        {
            approach = default;
            var placed = shelf != null ? shelf.GetComponent<PlacedObject>() : null;
            if (placed == null || placed.item == null) return false;
            var item = placed.item; var bounds = item.placementBounds;
            Vector3 forward = Quaternion.Euler(0, item.frontFacing * 90, 0) * Vector3.forward;
            Vector3 lateral = Vector3.Cross(Vector3.up, forward);
            float depth = Mathf.Abs(forward.x) * bounds.extents.x + Mathf.Abs(forward.z) * bounds.extents.z;
            float width = Mathf.Abs(lateral.x) * bounds.extents.x + Mathf.Abs(lateral.z) * bounds.extents.z;
            Vector3 center = placed.transform.position + placed.transform.rotation * new Vector3(bounds.center.x, 0, bounds.center.z);
            for (int side = 0; side < (item.twoSidedAccess ? 2 : 1); side++)
                foreach (float offset in new[] { 0f, -.6f, .6f })
                {
                    Vector3 target = center + placed.transform.rotation * (forward * (side == 0 ? 1 : -1) * (depth + building.grid.cellSize * 2) + lateral * width * offset);
                    if (!Navigation.TryNearest(target, out var point) || !Navigation.TryPath(start, point, out _, true)) continue;
                    approach = point; return true;
                }
            return false;
        }
        private int Reserved(PlacedShelfStock shelf) => _visits.Where(v => v.Shelf == shelf && v.Stage != VisitStage.Leaving).Sum(v => v.Quantity);
        private void AdvanceVisit(ShopperVisit visit, float seconds)
        {
            if (visit.Character == null || visit.Shelf == null) { RemoveVisit(visit); return; }
            if (!visit.Restrained) visit.Character.Advance(seconds);
            visit.Wait(seconds);
            if (visit.Stage == VisitStage.Shopping && visit.Character.Arrived && visit.WaitingSeconds >= 2)
            {
                visit.ChangeStage(visit.IsThief ? VisitStage.Escaping : VisitStage.Queueing);
                if (!visit.Character.MoveTo(visit.IsThief ? _arrivalPoint : _checkout + Vector3.right * _visits.Count(v => v.Stage == VisitStage.Waiting) * 0.18f))
                { RemoveVisit(visit); Notice = "Покупатель не смог пройти к выходу или кассе."; }
            }
            else if (visit.Stage == VisitStage.Queueing && visit.Character.Arrived) visit.ChangeStage(VisitStage.Waiting);
            else if (visit.Stage == VisitStage.Waiting && !visit.Claimed && visit.WaitingSeconds > balance.customerPatienceSeconds)
            { _reputation = Mathf.Max(1, _reputation - 0.12f); Leave(visit); Notice = "Покупатель ушёл из очереди: нужна помощь на кассе."; }
            else if (visit.Stage == VisitStage.Escaping && visit.Character.Arrived)
            { visit.CommitGoods(); _reputation = Mathf.Max(1, _reputation - 0.15f); Notice = "Вор сбежал с товаром!"; RemoveVisit(visit); }
            else if (visit.Stage == VisitStage.Leaving && visit.Character.Arrived) RemoveVisit(visit);
            // Furniture can change routes; never strand a visit forever.
            else if (visit.Stage != VisitStage.Waiting && visit.WaitingSeconds > 45) { RemoveVisit(visit); Notice = "Проход заблокирован."; }
        }
        public ShopperVisit ClaimCheckout()
        {
            foreach (var visit in _visits) if (visit.TryClaim()) return visit;
            return null;
        }
        public bool CompleteCheckout(ShopperVisit visit)
        {
            if (!_visits.Contains(visit) || !visit.Claimed || visit.Stage != VisitStage.Waiting || !visit.CommitGoods()) return false;
            money.Add(visit.Quantity * visit.Product.SellPrice); _completedSales++; _reputation = Mathf.Min(5, _reputation + 0.04f);
            if (_dirt.Count < 8 && UnityEngine.Random.value < balance.litterChance) SpawnDirt(visit.Character.transform.position);
            Leave(visit); Notice = "Продажа завершена: " + MoneyFormat.Compact(visit.Quantity * visit.Product.SellPrice); return true;
        }
        private void Leave(ShopperVisit visit)
        {
            visit.Release(); visit.ChangeStage(VisitStage.Leaving);
            if (!visit.Character.MoveTo(_arrivalPoint)) RemoveVisit(visit);
        }
        private void RemoveVisit(ShopperVisit visit) { _visits.Remove(visit); if (visit.Character != null) Destroy(visit.Character.gameObject); }
        public void CatchThief(ShopperVisit visit)
        {
            if (!_visits.Contains(visit) || visit.Stage != VisitStage.Escaping) return;
            _reputation = Mathf.Min(5, _reputation + 0.03f); RemoveVisit(visit); Notice = "Вор пойман. Товар сохранён.";
        }
        public void ClickThief(ShopperVisit visit) { if (visit.CatchClick()) CatchThief(visit); }
        public ShopDirtSpot SpawnDirt(Vector3 position)
        {
            if (_dirt.Count >= 8) return null;
            var root = new GameObject("Spilled dirt"); root.transform.SetParent(transform); root.transform.position = position;
            for (int i = 0; i < 5; i++)
            {
                var paper = GameObject.CreatePrimitive(PrimitiveType.Cube); paper.transform.SetParent(root.transform);
                paper.transform.localPosition = new Vector3((i % 3 - 1) * 0.06f, 0.005f, (i / 3) * 0.055f);
                paper.transform.localScale = new Vector3(0.09f, 0.01f, 0.055f); paper.transform.localRotation = Quaternion.Euler(0, i * 32, 0);
                paper.GetComponent<Collider>().enabled = false; paper.GetComponent<Renderer>().sharedMaterial = dirtMaterial;
            }
            var dirt = root.AddComponent<ShopDirtSpot>(); _dirt.Add(dirt); return dirt;
        }
        public void Clean(ShopDirtSpot dirt) { if (dirt == null) return; _dirt.Remove(dirt); Destroy(dirt.gameObject); Notice = "Пол чистый."; }
        public bool IsShelfInPlayerWork(PlacedShelfStock shelf) => work != null && work.ActiveShelf == shelf;
        public bool TryClaimRestock(out PlacedShelfStock shelf, out RetailEmpireTycoon.Core.ProductItemData product)
        {
            shelf = null; product = null;
            foreach (var candidate in FindObjectsOfType<PlacedShelfStock>())
            {
                if (candidate.IsFull || _restocking.Contains(candidate) || IsShelfInPlayerWork(candidate)) continue;
                var assigned = candidate.AssignedProduct;
                if (assigned != null && warehouse.GetCount(assigned) > 0 && candidate.CanAccept(assigned)) product = assigned;
                else if (assigned == null)
                    foreach (var entry in warehouse.Entries)
                        if (entry.Count > 0 && candidate.CanAccept(entry.Item)) { product = entry.Item; break; }
                if (product == null) continue;
                shelf = candidate; _restocking.Add(shelf); return true;
            }
            return false;
        }
        public void ReleaseRestock(PlacedShelfStock shelf) { _restocking.Remove(shelf); }
        private void ChargeWages(float seconds)
        {
            int perMinute = 0; foreach (StaffRole role in Enum.GetValues(typeof(StaffRole))) perMinute += _staff.Count(role) * balance.Wage(role);
            _wages += perMinute * seconds / 60;
            int due = Mathf.FloorToInt(_wages);
            if (due <= 0) { StaffWorking = true; return; }
            if (!money.TrySpend(due)) { StaffWorking = false; _wages = Mathf.Min(_wages, perMinute); Notice = "Не хватает денег на зарплату. Персонал ждёт оплату."; return; }
            _wages -= due; StaffWorking = true;
        }
        private void RefreshRating()
        {
            var shelves = FindObjectsOfType<PlacedShelfStock>();
            int variety = shelves.Where(s => s.CurrentProduct != null && !s.IsEmpty).Select(s => s.CurrentProduct).Distinct().Count();
            float stockRatio = shelves.Length == 0 ? 0 : (float)shelves.Count(s => !s.IsEmpty) / shelves.Length;
            float sizeBonus = 0.12f * (Level - 1);
            Rating = Mathf.Clamp(_reputation * 0.55f + stockRatio * 1.1f + Mathf.Min(5, variety) * 0.16f + sizeBonus - _dirt.Count * 0.12f - _visits.Count(v => v.WaitingSeconds > 35 && v.Stage == VisitStage.Waiting) * 0.08f, 1, 5);
        }
        public ShopOperationsSaveData BuildSaveData() => new ShopOperationsSaveData { isOpen = IsOpen, reputation = _reputation, staff = _staff.Snapshot(), unpaidWages = _wages, completedSales = _completedSales };
        public void ApplySaveData(ShopOperationsSaveData data)
        {
            data ??= new ShopOperationsSaveData();
            foreach (var visit in _visits.ToArray()) RemoveVisit(visit);
            _reputation = float.IsNaN(data.reputation) || float.IsInfinity(data.reputation) ? 3 : Mathf.Clamp(data.reputation, 1, 5);
            _wages = float.IsNaN(data.unpaidWages) || float.IsInfinity(data.unpaidWages) ? 0 : Mathf.Clamp(data.unpaidWages, 0, 10000);
            _completedSales = Mathf.Max(0, data.completedSales); _staff.Restore(data.staff, Level); SetOpen(data.isOpen);
            // Loading the same store level can still replace all furniture and the entrance.
            // Rebuild routes only after the world and shelf saves have been restored.
            _refreshWorld = true;
            ConfigureWorld(); RefreshRating();
        }
        private void OnDisable() { work?.Cancel(); foreach (var employee in _employees) if (employee != null) employee.CancelDuty(); }
    }
}
