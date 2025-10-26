using Duckov.Economy;
using Duckov.UI.Animations;
using SodaCraft.Localizations;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bank {
    public class ATMPanel_PayPanel :MonoBehaviour{
        [SerializeField]
        private FadeGroup fadeGroup;

        [SerializeField]
        private DigitInputPanel inputPanel;

        [SerializeField]
        private Button confirmButton;

        [SerializeField]
        private GameObject insufficientIndicator;

        [SerializeField]
        private Button quitButton;

        public event Action<ATMPanel_PayPanel> onQuit;

        private long CashAmount => ModBehaviour.Debt;

        private void Awake() {
            Bind();
            inputPanel.onInputFieldValueChanged += OnInputValueChanged;
            inputPanel.maxFunction = () => CashAmount;
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);
            quitButton.onClick.AddListener(OnQuitButtonClicked);
            ChangeTextLocalizor();
        }

        private void OnEnable() {
            Refresh();
        }

        private void ChangeTextLocalizor() {
            this.transform.Find("OperationTitleBar/Text (TMP)").GetComponent<TextLocalizor>().Key = "Btn_Select_Pay";
            this.transform.Find("Control/Confirm/InsufficientIndicator/InsufficientText").GetComponent<TextLocalizor>().Key = "UI_ATM_InsufficientPay";
            LocalizationManager.SetOverrideText("UI_ATM_InsufficientPay", "没那么多欠款");
        }

        private void Bind() { 
            fadeGroup = this.GetComponent<FadeGroup>();
            inputPanel = this.transform.Find("Control/DigitInputPanel").GetComponent<DigitInputPanel>();
            confirmButton = this.transform.Find("Control/Confirm/Btn_Confirm").GetComponent<Button>();
            insufficientIndicator = this.transform.Find("Control/Confirm/InsufficientIndicator").gameObject;
            quitButton = this.transform.Find("OperationTitleBar/Btn_Exit").GetComponent<Button>();
        }

        private void OnInputValueChanged(string v) {
            Refresh();
        }

        private void OnConfirmButtonClicked() {
            if (ModBehaviour.PayMoney(inputPanel.Value)) {
                var DigitInputPanelClear = ModBehaviour.DigitInputPanelClearInvoker.CreateClearDelegate(inputPanel);
                DigitInputPanelClear();
            }
            
        }

        private void OnQuitButtonClicked() {
            this.onQuit?.Invoke(this);
        }

        private void Refresh() {
            bool flag = ModBehaviour.Debt >= inputPanel.Value;
            flag &= inputPanel.Value <= 10000000;
            flag &= inputPanel.Value >= 0;
            insufficientIndicator.SetActive(!flag);
        }

        internal void Hide(bool skip = false) {
            if (skip) {
                fadeGroup.SkipHide();
            }
            else {
                fadeGroup.Hide();
            }
        }

        internal void Show() {
            fadeGroup.Show();
        }
    }
}
