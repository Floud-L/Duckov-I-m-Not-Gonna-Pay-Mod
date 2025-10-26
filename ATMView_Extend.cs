using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Bank {
    public class ATMView_Extend :MonoBehaviour{
        private void OnEnable() {
            
        }

        private void OnDisable() {
            ModBehaviour.Instance.HideAllPanels();
        }
    }
}
