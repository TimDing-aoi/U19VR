using UnityEngine;
using TMPro;
using SFB;
using System;
using System.Xml;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using static ViveSR.anipal.Eye.SRanipal_Eye_Framework;
using static Monkey2D;
using UnityEngine.SceneManagement;
using System.Text;

public class GoToSettings : MonoBehaviour
{
    public Canvas mainMenu;
    public Canvas settingMenu;
    public GameObject obj;
    private TMP_InputField input;
    public GameObject settingMenu1;

    // Start is called before the first frame update
    void Start()
    {
        UnityEngine.Screen.SetResolution(1920, 1080, true);
        Application.targetFrameRate = 200;
        if (obj.name == "Counter")
        {
            GetComponent<TMP_Text>().text = string.Format("Previous Run Number: {0}\nGood: {1}, Total: {2}", PlayerPrefs.GetInt("Run Number"), PlayerPrefs.GetInt("Good Trials"), PlayerPrefs.GetInt("Total Trials"));
        }
        else
        {
            input = obj.GetComponent<TMP_InputField>();
        }
        PlayerPrefs.SetInt("Save", 0);
    }

    public void ToSettings()
    {
        mainMenu.enabled = false;
        settingMenu.enabled = true;
        foreach (Transform child in settingMenu1.transform)
        {
            foreach (Transform children in child)
            {
                if (children.gameObject.CompareTag("Setting"))
                {
                    if (children.name == "Path" || children.name == "Name" || children.name == "Date")
                    {
                        TMP_InputField field = children.GetComponent<TMP_InputField>();
                        string LastValue = PlayerPrefs.GetString(children.name);
                        field.text = LastValue;
                    }
                    else
                    {
                        TMP_InputField field = children.GetComponent<TMP_InputField>();
                        float LastValue = PlayerPrefs.GetFloat(children.name);
                        if (field != null)
                        {
                            field.text = LastValue.ToString();
                        }
                    }
                }
            }
        }
    }

    public void BeginCalibration()
    {
        PlayerPrefs.SetFloat("calib", 1);
        SceneManager.LoadScene(1);
    }

    public void BeginGame()
    {
        PlayerPrefs.SetFloat("calib", 0);
        SceneManager.LoadScene(2);
    }

    public void ToMainMenu()
    {
        mainMenu.enabled = true;
        settingMenu.enabled = false;
    }

    /// <summary>
    /// Instead of hard-coding every single setting, just use the name of the
    /// object that this function is currently acting upon as the key for its
    /// value. There is no avoiding hard-coding setting the respective varibles
    /// to the correct value, however; you need to remember what the names of
    /// the objects and what variable they are associated with.
    /// 
    /// For example:
    /// If I have an object whose name is "Distance" and, in the game, I set it
    /// to "90", as in the TMP_InputField.text = "90", that value gets stored in
    /// PlayerPrefs associated to the key "Distance", but there is no way to 
    /// store the keys in a separate class and use them later. Anyway, trying to
    /// get keys from somewhere is harder, so just hard-code it when retrieving
    /// the values.
    /// </summary>
    public void SaveSetting()
    {
        try
        {
            if (obj.name == "Name" || obj.name == "Date")
            {
                if (input.text == null)
                {
                    string errorText = obj.name + ": Invalid or missing TMP_InputField text.";
                    throw new Exception(errorText);
                }

                PlayerPrefs.SetString(obj.name, input.text);
            }
            else if (obj.name == "Path")
            {
                if (input.text == null)
                {
                    string errorText = obj.name + ": Invalid or missing TMP_InputField text.";
                    throw new Exception(errorText);
                }

                string temp = input.text + "\\test.txt";
                try
                {
                    File.WriteAllText(temp, "test");
                    PlayerPrefs.SetString(obj.name, input.text);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
            else
            {
                PlayerPrefs.SetFloat(obj.name, float.Parse(input.text));
                if (input.text == null)
                {
                    string errorText = obj.name + ": Invalid or missing TMP_InputField text.";
                    throw new Exception(errorText);
                }
            }
            if (obj.name == null)
            {
                throw new Exception("Invalid or missing object name.");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    public void LoadXML()
    {
        try
        {
            var extensions = new[] {
                new ExtensionFilter("Extensible Markup Language ", "xml")
            };
            var path = StandaloneFileBrowser.OpenFilePanel("Open File Destination", "", extensions, false);
            // TODO: set all playerprefs and corresponding text fields to xml settings

            XmlDocument doc = new XmlDocument();
            doc.Load(path[0]);

            foreach (Transform child in settingMenu1.transform)
            {
                foreach (Transform children in child)
                {
                    if (children.gameObject.CompareTag("Setting"))
                    {
                        if (children.name == "Path" || children.name == "Name" || children.name == "Date")
                        {
                            TMP_InputField field = children.GetComponent<TMP_InputField>();
                            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                            {
                                foreach (XmlNode setting in node.ChildNodes)
                                {
                                    if (setting.Name == children.name.Replace(" ", ""))
                                    {
                                        field.text = setting.InnerText;
                                        PlayerPrefs.SetString(children.name, field.text);
                                    }
                                }
                            }
                        }
                        else
                        {
                            TMP_InputField field = children.GetComponent<TMP_InputField>();
                            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                            {
                                foreach (XmlNode setting in node.ChildNodes)
                                {
                                    if (setting.Name == children.name.Replace(" ", ""))
                                    {
                                        field.text = setting.InnerText;
                                        PlayerPrefs.SetFloat(children.name, float.Parse(field.text));
                                    }
                                    else if (setting.Name == "RunNumber")
                                    {
                                        PlayerPrefs.SetInt("Run Number", int.Parse(setting.InnerText));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    public void Reset_run (){
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt("Run Number", 1);
        ToSettings();
    }
}
