using System;
using System.IO.Ports;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;


[DisallowMultipleComponent]
public class SyncController : MonoBehaviour
{
    wrmhl sync = new wrmhl();
    public string portName = "COM4";
    public int baudRate = 2000000;
    public int ReadTimeout = 5000;
    public int QueueLength = 1;

    public static SyncController syncController;
  
    public int TTL;
    public bool IsConnected = true;

    public int updatesCounter = 0;

    // Start is called before the first frame update
    void Start()
    {
        PlayerPrefs.SetString("TTL State", "Connected");
        print("This is printing from the SyncController.cs");
        //keyboard = (int)PlayerPrefs.GetFloat("IsKeyboard") == 1;

        updatesCounter = 0;

        syncController = this;

        sync.set(portName, baudRate, ReadTimeout, QueueLength);
        try
        {
            sync.connect();
        }
        catch
        {
            print("Sync controller not connected");
        }
    }

    // Update is called once per frame
    public async void Update()
    {
        try
        {
            TTL = int.Parse(sync.readQueue());
            updatesCounter = 0;
            //if (updatesCounter % 100 == 0)
            //{
            //    PlayerPrefs.SetString("TTL State", "Connected");
            //}

        }
        catch (Exception e)
        {
            updatesCounter++;
            if (updatesCounter % 100 == 0)
                PlayerPrefs.SetString("TTL State", "Disconnected");
        }


        await new WaitForUpdate();
    }

    private void OnDisable()
    {
        sync.close();
    }

    private void OnApplicationQuit()
    {
        sync.close();
    }
}
