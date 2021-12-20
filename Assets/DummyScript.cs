using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummyScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMSelectable Sel;

    private void Start()
    {
        Sel.OnInteract += delegate () { Module.HandlePass(); return false; };
    }
}
