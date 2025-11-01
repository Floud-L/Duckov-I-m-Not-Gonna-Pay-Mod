using Duckov;
using Duckov.Economy;
using Duckov.Modding;
using ItemStatsSystem;
using Saves;
using SodaCraft.Localizations;
using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Bank {

    public class ModBehaviour : Duckov.Modding.ModBehaviour {
        public static bool EnableTestMode = false;
        public static readonly int DebtNoteTypeID = 14001;
        public static int BalanceRatePerLevel = 5000;
        public static int DueDays = 2;

        private GameObject? bankPanel = null;
        private ATMPanel_BorrowPanel borrowPanel;
        private ATMPanel_PayPanel payPanel;
        private static TextMeshProUGUI totalBalanceText;
        private static TextMeshProUGUI debtText;
        private static TextMeshProUGUI debtDaysText;
        

        public static long TotalBalance {
            get {
                return EXPManager.Level * BalanceRatePerLevel;
            }
        }

        private static long debt = 0;

        public static event Action<long, long> DebtChanged;
        public static long Debt {
            get {
                return debt;
            }
            set {
                SetDebt(value);
            }
        }

        private static void SetDebt(long newValue) {
            if (debt == newValue) return;
            var old = debt;
            debt = newValue;
            RefreshInfo();
            Save();
            DebtChanged?.Invoke(old, newValue);
        }

        private static ModBehaviour instance;
        public static ModBehaviour Instance {
            get {
                return instance;
            }
        }

        private static bool hasDebtNote = false;
        public static bool HasDebtNote {
            get {
                return hasDebtNote;
            }
        }

        private static long debtDay = -1;
        public static bool isOverDue {
            get {
                if (debtDay == -1) return false;
                var days = GameClock.Day - debtDay;
                return days > DueDays;
            }
        }

        private void Awake() {
            if (instance != null) { 
                DestroyImmediate(this);
            }
            instance = this;

            AssemblyLoader();

            LevelManager.OnAfterLevelInitialized += OnLevelLoaded;
            Debug.Log("Bank Mod Awake");
        }

        private void OnDestroy() {
            LevelManager.OnAfterLevelInitialized -= OnLevelLoaded;
            ItemCreator.DestroyCreatedItems();
        }

        private void Update() {
            EnemyCreator.OnUpdate();
            if (EnableTestMode) {
                TestAddDebtNoteItem();
            }
        }

        protected override void OnAfterSetup() {
            ConfigManager.ApplyToGame();
            ItemCreator.Init();
            EnemyCreator.Init();
            
        }

        private void OnLevelLoaded() {
            OnEnterBase();
            HasDebtNoteCheck();
            EnemyCreator.OnLevelLoaded();

        }

        private void OnEnterBase() {
            if (LevelManager.Instance.IsBaseLevel && bankPanel == null) {
                Debug.Log($"[Bank] 进入基地");
                var UICanvas = GameObject.Find("LevelConfig/LevelManager(Clone)/GameplayUICanvas");
                bankPanel = UICanvas?.transform.Find("ATMView/Panel")?.gameObject;
                if (bankPanel != null) {
                    AddBankUI();
                    Load();
                    DebtNoteDestroyCheck();
                }
            }
        }

        private void AddBankUI() {
            var originalButtonObj = bankPanel.transform.Find("OperationLayoutParent/Panel_Select/Btn_Select_Save")?.gameObject;
            var originalTextObj = bankPanel.transform.Find("Info/Cash")?.gameObject;
            var originalPanelObj = bankPanel.transform.Find("OperationLayoutParent/Panel_Save")?.gameObject;

            var exitButton = bankPanel.transform.Find("Title/Btn_Exit")?.GetComponent<Button>();

            if (originalButtonObj == null||originalTextObj == null)
                return;

            var borrowButtonObj = GameObject.Instantiate(originalButtonObj, originalButtonObj.transform.parent);
            var payButtonObj = GameObject.Instantiate(originalButtonObj, originalButtonObj.transform.parent);
            var totalBalanceObj = GameObject.Instantiate(originalTextObj, originalTextObj.transform.parent);
            var debtObj = GameObject.Instantiate(originalTextObj, originalTextObj.transform.parent);
            var debtDaysObj = GameObject.Instantiate(originalTextObj, originalTextObj.transform.parent);
            var borrowPanelObj = GameObject.Instantiate(originalPanelObj, originalPanelObj.transform.parent);
            var payPanelObj = GameObject.Instantiate(originalPanelObj, originalPanelObj.transform.parent);

            var borrowButton = borrowButtonObj.GetComponent<Button>();
            var payButton = payButtonObj.GetComponent<Button>();

            DestroyImmediate(borrowPanelObj.GetComponent<ATMPanel_SavePanel>());
            DestroyImmediate(payPanelObj.GetComponent<ATMPanel_SavePanel>());

            totalBalanceText = totalBalanceObj.transform.Find("Value_Cash").GetComponent<TextMeshProUGUI>();
            debtText = debtObj.transform.Find("Value_Cash").GetComponent<TextMeshProUGUI>();
            debtDaysText = debtDaysObj.transform.Find("Value_Cash").GetComponent<TextMeshProUGUI>();
            borrowPanel = borrowPanelObj.AddComponent<ATMPanel_BorrowPanel>();
            payPanel = payPanelObj.AddComponent<ATMPanel_PayPanel>();
            bankPanel.transform.parent.gameObject.AddComponent<ATMView_Extend>();

            borrowButtonObj.name = "Btn_Select_Borrow";
            payButtonObj.name = "Btn_Select_Pay";
            totalBalanceObj.name = "TotalBalance";
            debtObj.name = "Debt";
            debtDaysObj.name = "DebtDays";
            borrowPanelObj.name = "Panel_Borrow";
            payPanelObj.name = "Panel_Pay";

            borrowButtonObj.GetComponentInChildren<TextLocalizor>().Key = borrowButtonObj.name;
            payButtonObj.GetComponentInChildren<TextLocalizor>().Key = payButtonObj.name;
            debtObj.GetComponentInChildren<TextLocalizor>().Key = debtObj.name;
            totalBalanceObj.GetComponentInChildren<TextLocalizor>().Key = totalBalanceObj.name;
            debtDaysObj.GetComponentInChildren<TextLocalizor>().Key = debtDaysObj.name;

            LocalizationManager.SetOverrideText(borrowButtonObj.name,"借钱");
            LocalizationManager.SetOverrideText(payButtonObj.name,"还钱");
            LocalizationManager.SetOverrideText(debtObj.name,"欠款");
            LocalizationManager.SetOverrideText(totalBalanceObj.name,"可借额度");
            LocalizationManager.SetOverrideText(debtDaysObj.name,"还款天数");
            

            borrowButton.onClick.AddListener(ShowBorrowPanel);
            payButton.onClick.AddListener(ShowPayPanel);

            borrowPanel.onQuit += BorrowPanel_onQuit;
            payPanel.onQuit += PayPanel_onQuit;

            borrowPanelObj.SetActive(true);
            payPanelObj.SetActive(true);
            RefreshInfo();
        }

        private void BorrowPanel_onQuit(ATMPanel_BorrowPanel panel) {
            ShowSelectPanel();
        }

        private void PayPanel_onQuit(ATMPanel_PayPanel panel) {
            ShowSelectPanel();
        }

        private void ShowSelectPanel() {
            HideAllPanels();
            bankPanel.GetComponent<ATMPanel>().ShowSelectPanel();
        }

        private void ShowBorrowPanel() {
            HideAllPanels();
            borrowPanel.Show();
        }

        private void ShowPayPanel() {
            HideAllPanels();
            payPanel.Show();
        }

        public void HideAllPanels(bool skip = false) {
            borrowPanel.Hide();
            payPanel.Hide();

            var ATMPanelHideAllPanels = ATMPanelHideAllPanelsInvoker.CreateHideAllPanelDelegate(bankPanel.GetComponent<ATMPanel>());
            ATMPanelHideAllPanels(false);

            RefreshInfo();
        }

        public static void RefreshInfo() {
            totalBalanceText.text = $"{(TotalBalance-Debt):n0}";
            debtText.text = $"{Debt:n0}";
            debtDaysText.text = debtDay == -1 ? "无欠款" : $"{DueDays - (GameClock.Day - debtDay):n0}";
            if (isOverDue) {
                debtDaysText.text = "追债中";
            }
        }

        private void TestAddDebtNoteItem() {
            if (Keyboard.current.f3Key.wasPressedThisFrame) {
                var item = ItemAssetsCollection.InstantiateSync(14001);
                ItemUtilities.SendToPlayerCharacterInventory(item);
                Debug.Log("[Bank] 生成欠条");
            }
        }

        private void DebtNoteDestroyCheck() {
            if (LevelManager.Instance.IsBaseLevel) {
                if (DestroyItem(DebtNoteTypeID)) {
                    Debt = 0;
                    debtDay = -1;
                    Debug.Log("[Bank] 清除欠款");
                }
                RefreshInfo();
            }
            
        }

        private void HasDebtNoteCheck() {
            var itemList = ItemUtilities.FindAllBelongsToPlayer((e) => e != null && e.TypeID == DebtNoteTypeID);
            if (itemList.Count > 0) {
                hasDebtNote = true;
            }
            else { 
                hasDebtNote = false;
            }
        }

        private bool DestroyItem(int typeID) {
            var itemList = ItemUtilities.FindAllBelongsToPlayer((e) => e != null && e.TypeID == typeID);
            if (itemList.Count == 0) {
                Debug.Log("[Bank] 没有找到欠条");
                return false;
            }
            foreach (var item in itemList) {
                item.Detach();
            }
            Debug.Log("[Bank] 已销毁欠条");
            return true;
        }

        private static void Save() {
            SavesSystem.Save("Debt",Debt);
            SavesSystem.Save("DebtDays",debtDay);
            Debug.Log("[Bank] 已保存欠款数据");
        }

        private static void Load() {
            if (SavesSystem.KeyExisits("Debt")) { 
                var data = SavesSystem.Load<long>("Debt");
                var days = SavesSystem.Load<long>("DebtDays");
                if (data > 0&& days == -1) {
                    days = GameClock.Day;
                }
                else if(data<=0){
                    days = -1;
                }
                debtDay = days;
                Debt = data;
                
                Debug.Log("[Bank] 已加载欠款数据");
            }
        }

        public static bool BorrowMoney(long money) {
            if(money>(TotalBalance-Debt))
                return false;
            if(money<=0)
                return false;
            EconomyManager.Add(money);
            if (Debt == 0) {
                debtDay = GameClock.Day;
            }
            Debt += money;
            RefreshInfo();
            return true;
        }

        public static bool PayMoney(long money) {
            if(money>Debt)
                return false;
            if (money <= 0)
                return false;
            Cost cost = new Cost(money);
            if (!cost.Pay()) {
                return false;
            }
            Debt -= money;
            if (Debt == 0) { 
                debtDay = -1;
            }
            RefreshInfo();
            return true;
        }

        public static class ATMPanelHideAllPanelsInvoker {
            public static Action<bool> CreateHideAllPanelDelegate(ATMPanel panel) {
                if (panel == null) throw new ArgumentNullException(nameof(panel));

                var mi = typeof(ATMPanel).GetMethod("HideAllPanels", BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi == null) throw new MissingMethodException("[Bank] ATMPanel.HideAllPanels 未找到");

                return (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), panel, mi);
            }
        }

        public static class DigitInputPanelClearInvoker {
            public static Action CreateClearDelegate(DigitInputPanel panel) {
                if (panel == null) throw new ArgumentNullException(nameof(panel));

                var mi = typeof(DigitInputPanel).GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi == null) throw new MissingMethodException("[Bank] DigitInputPanel.Clear 未找到");

                return (Action)Delegate.CreateDelegate(typeof(Action), panel, mi);
            }
        }

        public Assembly AssemblyLoader() {
            string assemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,"YamlDotNet.dll");

            Debug.Log($"[Bank] YamlDotNet path: {assemblyPath}");

            if (!File.Exists(assemblyPath)) {
                throw new FileNotFoundException("[Bank] 找不到 YamlDotNet.dll YAML序列化模块无法加载！", assemblyPath);
            }

            try {
                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                Console.WriteLine("[Bank] 已加载 YamlDotNet: " + assembly.FullName);
                return assembly;
            }
            catch (Exception ex) {
                throw new InvalidOperationException("[Bank] 加载 YamlDotNet 程序集失败。", ex);
            }
        }

    }
}