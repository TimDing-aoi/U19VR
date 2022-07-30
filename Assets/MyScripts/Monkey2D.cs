///////////////////////////////////////////////////////////////////////////////////////////
///                                                                                     ///
/// Monkey2D.cs                                                                         ///
/// by Tim Ding                                                                         ///
/// hd840@nyu.edu                                                                       ///
/// For the U19 Project                                                                 ///
///                                                                                     ///
/// <summary>                                                                           ///
/// This script takes care of the stimulus behavior.                                    ///
/// Player will try to catch the FF and be given reward based on how close they are to  ///
/// the final position of the FF.                                                       ///
/// </summary>                                                                          ///
///////////////////////////////////////////////////////////////////////////////////////////

//edit
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static JoystickMonke;
using static Serial;
using UnityEngine.SceneManagement;
using System;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using System.IO.Ports;

public class Monkey2D : MonoBehaviour
{
    //Shared instance
    public static Monkey2D SharedMonkey;

    //In game objects
    public GameObject firefly;
    public GameObject player;
    public GameObject BlueCircle;
    public ParticleSystem particle_System;

    //Cameras
    public Camera Lcam;
    public Camera Rcam;
    public float offset = 0.01f;
    private float lm02;
    private float rm02;
    private Matrix4x4 lm;
    private Matrix4x4 rm;

    //FF settings
    [Tooltip("Radius of firefly")]
    [HideInInspector] public float fireflySize;
    [Tooltip("How long the firefly shows from the beginning of the trial")]
    [HideInInspector] public float lifeSpan;
    [Tooltip("Ratio of trials that will have fireflies always on")]
    [HideInInspector] public float ratioAlwaysOn;
    [Tooltip("Ratio of trials that will have fireflies always on")]
    [HideInInspector] public float Ratio2Obs;
    [Tooltip("Ratio of trials that will have 2 observations")]
    [HideInInspector] public float ratio2Obs;
    [Tooltip("Distance from origin to firefly")]
    [HideInInspector] public float FFMoveRadius;
    private float FFOpacity;

    //Sounds to be played
    public AudioSource audioSource;
    public AudioClip winSound;
    public AudioClip neutralSound;
    public AudioClip loseSound;

    //Per trial detailed settings
    readonly public List<float> YawSpeeds = new List<float>();
    readonly public List<float> SMtrials = new List<float>();
    readonly List<float> FFVelocities = new List<float>();
    readonly List<float> FFVRatios = new List<float>();
    readonly List<Tuple<float, float, float, float, float>> AllTrials = new List<Tuple<float, float, float, float, float>>();
    //FFv, SMspeed, is self-motion trial, is always on trial, is 2-obs trial
    [HideInInspector] public bool selfmotiontrial;
    [HideInInspector] public bool AlwaysOntrial;
    [HideInInspector] public bool DoubleObservtrial;
    [HideInInspector] public float velocity;
    [HideInInspector] public float noise_SD;
    [HideInInspector] public float velocity_Noised;

    //Acceleration Params
    private float FF0_acc;
    private float t_max = 1.5f;//Total FF move time
    private float t0_acc;
    private float t1_acc;
    private float sign_v;
    private float dFF_acc;

    //Other Settings
    [Tooltip("Maximum number of trials before quitting (0 for infinity)")]
    [HideInInspector] public int ntrials;
    [Tooltip("Player height")]
    [HideInInspector] public float p_height;
    [Tooltip("How long the juice valve is open")]
    [HideInInspector] public float juiceTime;
    private float minJuiceTime;
    private float maxJuiceTime;
    private float RewardWindow;
    float start_wait_time = 0.3f;

    //Current phase of the game
    public enum Phases
    {
        begin = 0,
        trial = 1,
        check = 2,
        juice = 3,
        ITI = 4,
        none = 9
    }

    //Phase trackers
    [HideInInspector] public Phases phase;
    [HideInInspector] public Phases currPhase;
    private bool isBegin = false;
    private bool isCheck = false;
    private bool isEnd = false;
    public bool isIntertrail = false;

    //Current player position
    private Vector3 pPos;

    //Gitter FF phase flag and locations
    public float GFFPhaseFlag = 0;
    public float FFnoise = 0;
    public float GFFTrueDegree = 0;
    public float SelfMotionSpeed = 0;
    private float GFFTrueRadians = 0;
    readonly public List<float> FFnoiseList = new List<float>();
    readonly public List<float> CIScores = new List<float>();

    //Data Saving

    // Trial number
    [HideInInspector] public int trialNum;
    readonly List<int> n = new List<int>();

    // Firefly ON Duration
    readonly List<float> onDur = new List<float>();

    // Firefly Check Coords
    readonly List<string> ffPos = new List<string>();
    string ffPosStr = "";

    // Player position at Check()
    readonly List<string> cPos = new List<string>();

    // Player rotation at Check()
    readonly List<string> cRot = new List<string>();

    // Player origin at beginning of trial
    readonly List<string> origin = new List<string>();
    [HideInInspector] public Vector3 player_origin;

    // Player rotation at origin
    readonly List<string> heading = new List<string>();

    // Player linear and angular velocity
    readonly List<float> max_v = new List<float>();
    readonly List<float> max_w = new List<float>();

    // Firefly velocity
    readonly List<float> fv = new List<float>();

    // Distances from player to firefly
    readonly List<string> dist = new List<string>();
    readonly List<float> distances = new List<float>();

    // Times
    readonly List<float> beginTime = new List<float>();
    readonly List<float> checkTime = new List<float>();
    readonly List<float> rewardTime = new List<float>();
    readonly List<float> juiceDuration = new List<float>();
    readonly List<float> endTime = new List<float>();
    readonly List<float> checkWait = new List<float>();
    readonly List<float> interWait = new List<float>();
    readonly List<float> PreparationStart = new List<float>();
    readonly List<float> HabituationStart = new List<float>();
    readonly List<float> ObservationStart = new List<float>();
    readonly List<float> ActionStart = new List<float>();
    readonly List<float> SelfReportStart = new List<float>();
    readonly List<float> FeedbackStart = new List<float>();
    [HideInInspector] public float programT0 = 0.0f;

    //Timeline times, in seconds
    private static readonly float frameRate = 90f;
    public float preparation_1 = 0.1f;
    public float preparation_2 = 0.2f;
    public float habituation_1 = 0.1f;
    public float habituation_2 = 0.2f;
    public float habituation_3 = 0.05f;
    public float observation;

    // Rewarded?
    bool rewarded;
    readonly List<int> score = new List<int>();

    // Was Always ON?
    readonly List<bool> alwaysON = new List<bool>();

    // File paths
    private string path;

    //Total Points Got
    [HideInInspector] public float points = 0;

    //Random & Seed
    private int seed;
    private System.Random rand;

    //Utility to trim the positions
    private readonly char[] toTrim = { '(', ')' };

    //Current Task Tracker
    CancellationTokenSource source;
    private Task currentTask;
    private bool playing = true;

    //Juice Port
    SerialPort juiceBox;

    //String Builder to save data
    StringBuilder sb = new StringBuilder();

    /// <summary>
    /// When the stimulus is activated
    /// </summary>
    void OnEnable()
    {
        //Run in background and clear cache
        Application.runInBackground = true;
        sb.Clear();

        //Juice port
        juiceBox = serial.sp;

        //Send Block Start Marker
        SendMarker("f", 1000.0f);
        programT0 = Time.realtimeSinceStartup;

        //VR set up
        UnityEngine.XR.InputTracking.disablePositionalTracking = true;
        UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(Lcam, true);
        UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(Rcam, true);
        XRSettings.occlusionMaskScale = 10f;
        XRSettings.useOcclusionMesh = false;
        Lcam.ResetProjectionMatrix();
        Rcam.ResetProjectionMatrix();
        lm = Lcam.projectionMatrix;
        lm02 = lm.m02;
        rm = Rcam.projectionMatrix;
        rm02 = rm.m02;
        lm.m02 = lm02 + offset;
        Lcam.SetStereoProjectionMatrix(Camera.StereoscopicEye.Left, lm);
        Lcam.projectionMatrix = lm;
        rm.m02 = rm02 - offset;
        Rcam.SetStereoProjectionMatrix(Camera.StereoscopicEye.Right, rm);
        Rcam.projectionMatrix = rm;
        List<XRDisplaySubsystem> displaySubsystems = new List<XRDisplaySubsystem>();
        SubsystemManager.GetInstances<XRDisplaySubsystem>(displaySubsystems);
        XRSettings.enabled = true;

        SharedMonkey = this;

        //Experiment set up
        seed = UnityEngine.Random.Range(1, 10000);
        rand = new System.Random(seed);
        p_height = PlayerPrefs.GetFloat("pHeight");
        FFMoveRadius = PlayerPrefs.GetFloat("FFmoveradius");
        fireflySize = PlayerPrefs.GetFloat("Size");
        firefly.transform.localScale = new Vector3(fireflySize, fireflySize, 1);
        ratioAlwaysOn = PlayerPrefs.GetFloat("Ratio");
        ratio2Obs = PlayerPrefs.GetFloat("Ratio2Obs");
        minJuiceTime = PlayerPrefs.GetFloat("Min Juice Time");
        maxJuiceTime = PlayerPrefs.GetFloat("Max Juice Time");
        RewardWindow = PlayerPrefs.GetFloat("RewardWindow");
        float FF_radius_ratio = fireflySize / (4 * FFMoveRadius * Mathf.PI);
        float FF_radius_deg = FF_radius_ratio * 360;
        RewardWindow += FF_radius_deg;
        dFF_acc = PlayerPrefs.GetFloat("FFacceleration");
        observation = PlayerPrefs.GetFloat("FFOnTime");
        FFOpacity = PlayerPrefs.GetFloat("FFOpacity");

        //FF velocities
        FFVelocities.Add(PlayerPrefs.GetFloat("V1"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V2"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V3"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V4"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V5"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V6"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V7"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V8"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V9"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V10"));
        FFVelocities.Add(PlayerPrefs.GetFloat("V11"));

        //Ratio of the FF velocity
        FFVRatios.Add(PlayerPrefs.GetFloat("VR1"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR2"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR3"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR4"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR5"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR6"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR7"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR8"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR9"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR10"));
        FFVRatios.Add(PlayerPrefs.GetFloat("VR11"));

        //Self motion speeds
        YawSpeeds.Add(PlayerPrefs.GetFloat("SelfMotionSpeed1"));
        YawSpeeds.Add(PlayerPrefs.GetFloat("SelfMotionSpeed2"));
        YawSpeeds.Add(PlayerPrefs.GetFloat("SelfMotionSpeed3"));
        YawSpeeds.Add(PlayerPrefs.GetFloat("SelfMotionSpeed4"));
        YawSpeeds.Add(PlayerPrefs.GetFloat("SelfMotionSpeed5"));

        //Number of trials per SM speed
        SMtrials.Add(PlayerPrefs.GetFloat("NtrialsSM1"));
        SMtrials.Add(PlayerPrefs.GetFloat("NtrialsSM2"));
        SMtrials.Add(PlayerPrefs.GetFloat("NtrialsSM3"));
        SMtrials.Add(PlayerPrefs.GetFloat("NtrialsSM4"));
        SMtrials.Add(PlayerPrefs.GetFloat("NtrialsSM5"));
        
        //Pre-generating trial conditions
        //11 FF velocities
        for (int velocitiescondition = 0; velocitiescondition < 11; velocitiescondition++)
        {
            //5 SM speeds
            for (int speeds = 0; speeds < 5; speeds++)
            {
                int conditioncount = (int)(SMtrials[speeds] * FFVRatios[velocitiescondition]);
                while (conditioncount > 0)
                {
                    float condition_velocity = FFVelocities[velocitiescondition];
                    float condition_speed = YawSpeeds[speeds];
                    Tuple<float, float, float, float, float> TrialAttributes;
                    //Deciding always on or 2-obs randomly
                    if (condition_speed != 0)
                    {
                        bool always_on_trial = rand.NextDouble() <= ratioAlwaysOn;
                        bool two_obs_trial = false;
                        if (!always_on_trial)
                        {
                            two_obs_trial = rand.NextDouble() <= ratio2Obs;
                        }
                        float alwaysontr = 0;
                        float obs2tr = 0;
                        if (always_on_trial) alwaysontr = 1f;
                        if (two_obs_trial) obs2tr = 1f;
                        TrialAttributes = new Tuple<float, float, float, float, float>(condition_velocity, condition_speed, 1f, alwaysontr, obs2tr);
                    }
                    else
                    {
                        bool always_on_trial = rand.NextDouble() <= ratioAlwaysOn;
                        bool two_obs_trial = rand.NextDouble() <= ratio2Obs;
                        float alwaysontr = 0;
                        float obs2tr = 0;
                        if (always_on_trial) alwaysontr = 1f;
                        if (two_obs_trial) obs2tr = 1f;
                        TrialAttributes = new Tuple<float, float, float, float, float>(condition_velocity, condition_speed, 0f, alwaysontr, obs2tr);
                    }
                    AllTrials.Add(TrialAttributes);
                    conditioncount--;
                }
            }
        }
        Shuffle(AllTrials);
        string setupcheck = "Causal Inference Task: total number of " + AllTrials.Count.ToString() + " trials";
        print(setupcheck);
        ntrials = AllTrials.Count;

        BlueCircle.SetActive(true);
        drawLine(FFMoveRadius, 200);

        path = PlayerPrefs.GetString("Path");

        //Experiment Set up
        trialNum = 0;
        currPhase = Phases.begin;
        phase = Phases.begin;

        //Player initial pos
        player.transform.position = Vector3.up * p_height;
        player.transform.rotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);

        //If not using eye tracker
        if (PlayerPrefs.GetFloat("calib") == 0)
        {
            string firstLine = "TrialNum,TrialTime,BackendPhase,OnOff,PosX,PosY,PosZ,RotX,RotY,RotZ,RotW,CleanLinearVelocity,CleanAngularVelocity,FFX,FFY,FFZ,FFV/linear,GazeX,GazeY,GazeZ,GazeX0,GazeY0,GazeZ0,HitX,HitY,HitZ,ConvergeDist," +
                "LeftPupilDiam,RightPupilDiam,LeftOpen,RightOpen,CIFFPhase,FFTrueLocationDegree,FFnoiseDegree,frameCounter,FFV/degrees,SelfMotionSpeed";
            sb.Append(firstLine + PlayerPrefs.GetString("Name") + "," + PlayerPrefs.GetString("Date") + "," + PlayerPrefs.GetInt("Run Number").ToString("D3") + "\n");
        }
    }

    //On Stimulus End
    private void OnDisable()
    {
        juiceBox.Close();
    }

    /// <summary>
    /// Update is called once per frame, to update the game per frame
    /// 
    /// Every frame, add the time it occurs, the trial time (resets every new trial),
    /// trial number, and position and rotation of player and record it.
    /// 
    /// Use Flags to identify phases here to ensure that frames bahve correctly for the phase it is in
    /// </summary>
    void Update()
    {
        //Moves the optic flow with the player
        float EPSILON_OFFSET = 0.0002f;
        particle_System.transform.position = player.transform.position - (Vector3.up * (p_height - EPSILON_OFFSET));

        //Task switch based on phase
        if (playing && Time.realtimeSinceStartup - programT0 > start_wait_time)
        {
            switch (phase)
            {
                case Phases.begin:
                    phase = Phases.none;
                    currentTask = Begin();
                    break;

                case Phases.trial:
                    phase = Phases.none;
                    currentTask = Trial();
                    break;

                case Phases.check:
                    phase = Phases.none;
                    currentTask = Check();
                    break;

                case Phases.none:
                    break;
            }

            //Moving the firefly in phases
            double randStdNormal = 0;
            //Action (Flag = 4)
            if (GFFPhaseFlag == 4)
            {
                sign_v = Math.Sign(velocity);
                GFFTrueRadians = FF0_acc * Mathf.Deg2Rad;
                GFFTrueRadians += velocity * Mathf.Deg2Rad * (Time.time - t0_acc);
                GFFTrueRadians += sign_v * dFF_acc * Mathf.Deg2Rad * (((Time.time - t1_acc) / t_max) * ((Time.time - t1_acc) / t_max));
                //Add noise if desired
                //velocity_Noised = GFFTrueRadians + (float)randStdNormal * Mathf.Deg2Rad;
                float x = FFMoveRadius * Mathf.Cos(GFFTrueRadians);
                float y = 0.0001f;
                float z = FFMoveRadius * Mathf.Sin(GFFTrueRadians);
                firefly.transform.position = new Vector3(x, y, z);
            }
            //Observation (Flag = 3) and Ramp down (Flag = 3.5)
            else if (GFFPhaseFlag > 2.5 && GFFPhaseFlag < 4)
            {
                GFFTrueRadians = FF0_acc * Mathf.Deg2Rad + velocity * Mathf.Deg2Rad * (Time.time - t0_acc);
                float x = FFMoveRadius * Mathf.Cos(GFFTrueRadians);
                float y = 0.0001f;
                float z = FFMoveRadius * Mathf.Sin(GFFTrueRadians);
                firefly.transform.position = new Vector3(x, y, z);
            }
            FFnoise = (float)randStdNormal;
            FFnoiseList.Add(FFnoise);
            GFFTrueDegree = GFFTrueRadians;

            if (currentTask.IsFaulted)
            {
                print(currentTask.Exception);
            }
        }
    }

    /// <summary>
    /// Capture data at 90 Hz, and send markers at supposed times
    /// 
    /// Set Unity's fixed timestep to 1/90 in order to get 90 Hz recording
    /// Edit -> Project Settings -> Time -> Fixed Timestep
    /// </summary>
    public void FixedUpdate()
    {
        var keyboard = Keyboard.current;
        if ((keyboard.enterKey.isPressed || trialNum >= ntrials) && playing)
        {
            playing = false;

            Save();
            SendMarker("x", 1000.0f);

            juiceBox.Close();

            SceneManager.LoadScene("MainMenu");
        }

        if (isBegin)
        {
            isBegin = false;
            SendMarker("s", 1000.0f);
        }

        if (isCheck)
        {
            isCheck = false;
            checkTime.Add(Time.realtimeSinceStartup - programT0);
        }

        if (isEnd)
        {
            isEnd = false;
            SendMarker("e", 1000.0f);
        }

        if (PlayerPrefs.GetFloat("calib") == 0)
        {
            string transformedFFPos = new Vector3(firefly.transform.position.z, firefly.transform.position.y, firefly.transform.position.x).ToString("F8").Trim(toTrim).Replace(" ", "");
            Vector3 fake_location = new Vector3(-999f, -999f, -999f);
            sb.Append(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21}\n",
                   trialNum,
                   Time.realtimeSinceStartup,
                   (int)currPhase,
                   firefly.activeInHierarchy ? 1 : 0,
                   string.Join(",", player.transform.position.z, player.transform.position.y, player.transform.position.x),
                   string.Join(",", player.transform.rotation.x, player.transform.rotation.y, player.transform.rotation.z, player.transform.rotation.w),
                   -999,
                   SharedJoystick.moveX * SharedJoystick.maxJoyRotDeg,
                   transformedFFPos,
                   -999,
                   string.Join(",", -999, -999, -999),
                   string.Join(",", player.transform.position.x, player.transform.position.y, player.transform.position.z),
                   fake_location.ToString("F8").Trim(toTrim).Replace(" ", ""),
                   -999,
                   string.Join(",", -999, -999),
                   string.Join(",", -999, -999),
                   GFFPhaseFlag,
                   GFFTrueDegree * Mathf.Rad2Deg,
                   FFnoise,
                   Time.frameCount,
                   velocity,
                   SelfMotionSpeed));
        }
    }

    /// <summary>
    /// Begin Phase.
    /// Get trial conditions, set up player and FF positions
    /// </summary>
    async Task Begin()
    {
        await new WaitForEndOfFrame();

        //Max vel and rot
        max_v.Add(SharedJoystick.LinSpeed);
        max_w.Add(SharedJoystick.maxJoyRotDeg);

        currPhase = Phases.begin;
        isBegin = true;
        
        //FF origin position
        firefly.SetActive(false);
        Vector3 position;
        float x = FFMoveRadius * Mathf.Cos(0f);
        float y = 0;
        float z = FFMoveRadius * Mathf.Sin(0f);
        position = new Vector3(x, y, z);
        firefly.transform.position = position;
        firefly.SetActive(false);

        //Player origin position
        player.transform.rotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);
        player.transform.position = Vector3.up * p_height;
        player_origin = player.transform.position;
        origin.Add(player_origin.ToString("F5").Trim(toTrim).Replace(" ", ""));
        heading.Add(player.transform.rotation.ToString("F5").Trim(toTrim).Replace(" ", ""));

        //Drawing the trial condition from pregenerated trials
        var trial_info = AllTrials[trialNum];
        trialNum++;
        n.Add(trialNum);
        velocity = trial_info.Item1;
        SelfMotionSpeed = trial_info.Item2;
        selfmotiontrial = trial_info.Item3 == 1;
        AlwaysOntrial = trial_info.Item4 == 1;
        DoubleObservtrial = trial_info.Item5 == 1;
        string trialset = "Trial velocity =" + velocity.ToString() + "\n" + "Trial SMspeed:" + SelfMotionSpeed.ToString() + " Selfmotion:" + selfmotiontrial.ToString() + " Always On:"
            + AlwaysOntrial.ToString() + "Double Obsv:" + DoubleObservtrial.ToString();
        print(trialset);
        noise_SD = 0; //Not using noise for now

        fv.Add(velocity);
        onDur.Add(observation);

        //Was it an always on trial?
        if (AlwaysOntrial)
        {
            alwaysON.Add(true);
        }
        else
        {
            alwaysON.Add(false);
        }

        phase = Phases.trial;
        currPhase = Phases.trial;
    }

    /// <summary>
    /// Doesn't really do much besides wait for the player to start moving, and, afterwards,
    /// wait until the player stops moving and then start the check phase. Also will go back to
    /// begin phase if player doesn't move before timeout
    /// </summary>
    async Task Trial()
    {
        //Debug.Log("Trial Phase Start.");

        source = new CancellationTokenSource();

        //preperation
        GFFPhaseFlag = 1;
        PreparationStart.Add(Time.realtimeSinceStartup);
        BlueCircle.SetActive(true); //Blue circle shows up
        LineRenderer lr;
        lr = BlueCircle.GetComponent<LineRenderer>();
        lr.sortingOrder = 1;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.materials[0].SetColor("_Color", new Color(0.5529411f, 0.5607843f, 1f, 0f));
        int endFrame = Time.frameCount;
        endFrame = (int)(Time.frameCount + frameRate * preparation_1);
        await new WaitUntil(() => Time.frameCount == endFrame);
        for (int prep = 0; prep < frameRate * preparation_2; prep++)
        {
            await new WaitForSecondsRealtime(1f / frameRate);
            lr.materials[0].SetColor("_Color", new Color(0.5529411f, 0.5607843f, 1f, prep / (frameRate * preparation_2)));
        }

        //Habituation
        GFFPhaseFlag = 2;
        HabituationStart.Add(Time.realtimeSinceStartup);
        endFrame = (int)(Time.frameCount + frameRate * habituation_1);
        await new WaitUntil(() => Time.frameCount == endFrame);
        for (int Habituation_Frame = 0; Habituation_Frame < frameRate * habituation_2; Habituation_Frame++)
        {
            await new WaitForSecondsRealtime(1f / frameRate);
            float ring_color = (float)(((frameRate * habituation_2) - Habituation_Frame) /
                (frameRate * habituation_2));
            lr.materials[0].SetColor("_Color", new Color(0.5529411f, 0.5607843f, 1f, ring_color));
        }
        endFrame = (int)(Time.frameCount + frameRate * habituation_3);
        await new WaitUntil(() => Time.frameCount == endFrame);
        BlueCircle.SetActive(false);

        //ramp up
        GFFPhaseFlag = 2.5f;
        float updur = PlayerPrefs.GetFloat("RampUpDur");
        await new WaitForSeconds(updur);
        float grace_time = PlayerPrefs.GetFloat("GracePeriod");
        await new WaitForSeconds(grace_time);

        // Observation
        GFFPhaseFlag = 3;

        //Firefly Generation
        System.Random randNoise = new System.Random();
        float CImean1 = PlayerPrefs.GetFloat("CIFFmean1");
        float CImean2 = PlayerPrefs.GetFloat("CIFFmean2");
        float drawSD1 = PlayerPrefs.GetFloat("CIFFSD1");
        float drawSD2 = PlayerPrefs.GetFloat("CIFFSD2");
        float FF_circX = 999;//FF pos in deg

        float player_circX = SharedJoystick.circX * Mathf.Rad2Deg;//player pos in deg
        float RatioSD1 = PlayerPrefs.GetFloat("RatioSD1");
        bool use_first_dist = randNoise.NextDouble() < RatioSD1;
        //Choose one of the two given distributions to determine where to generate the FF
        if (use_first_dist)
        {
            while (Mathf.Abs(FF_circX - player_circX) > 22.5)
            {
                double u1 = 1.0 - randNoise.NextDouble(); //uniform(0,1] random doubles
                double u2 = 1.0 - randNoise.NextDouble();
                FF_circX = player_circX + (float)(CImean1 + drawSD1 * Math.Sqrt(-2.0 * Math.Log(u1)) *
                             Math.Sin(2.0 * Math.PI * u2));
            }
        }
        else
        {
            while (Mathf.Abs(FF_circX - player_circX) > 22.5)
            {
                double u1 = 1.0 - randNoise.NextDouble(); //uniform(0,1] random doubles
                double u2 = 1.0 - randNoise.NextDouble();
                FF_circX = player_circX + (float)(CImean2 + drawSD2 * Math.Sqrt(-2.0 * Math.Log(u1)) *
                             Math.Sin(2.0 * Math.PI * u2));
            }
        }
        float x = FFMoveRadius * Mathf.Cos(FF_circX * Mathf.Deg2Rad);
        float y = 0;
        float z = FFMoveRadius * Mathf.Sin(FF_circX * Mathf.Deg2Rad);
        Vector3 position = new Vector3(x, y, z);
        FF0_acc = FF_circX;
        t0_acc = Time.time;//FF generation time
        firefly.transform.position = position;
        GFFTrueRadians = FF_circX * Mathf.Deg2Rad;
        firefly.SetActive(true);
        ObservationStart.Add(Time.realtimeSinceStartup);

        endFrame = (int)(Time.frameCount + frameRate * observation);
        await new WaitUntil(() => Time.frameCount == endFrame);
        if (!AlwaysOntrial || FFOpacity == 0)
        {
            firefly.SetActive(false);
        }
        else
        {
            SpriteRenderer FFcr = firefly.GetComponent<SpriteRenderer>();
            FFcr.materials[0].SetColor("_Color", new Color(1f, 1f, 1f, FFOpacity));
        }

        //ramp down
        GFFPhaseFlag = 3.5f;
        float downdur = PlayerPrefs.GetFloat("RampDownDur");
        await new WaitForSeconds(downdur);

        //Action
        GFFPhaseFlag = 4;
        t1_acc = Time.time;//Action phase begin time
        ActionStart.Add(Time.realtimeSinceStartup);
        float ActionTime = FFMoveRadius / PlayerPrefs.GetFloat("LinearSpeed");
        await new WaitForSeconds(ActionTime/2);
        if (DoubleObservtrial)
        {
            firefly.SetActive(true);
        }
        await new WaitForSeconds(observation);
        if (!AlwaysOntrial)
        {
            firefly.SetActive(false);
        }
        await new WaitForSeconds(ActionTime/2 - observation);

        if (AlwaysOntrial)
        {
            firefly.SetActive(false);
        }

        //Moving on to check/feedback
        source.Cancel();
        velocity = 0.0f;
        phase = Phases.check;
        currPhase = Phases.check;
    }

    /// <summary>
    /// Save the player's position (pPos) and the firefly (reward zone)'s position (fPos)
    /// and start a coroutine to wait for some random amount of time between the user's
    /// specified minimum and maximum wait times
    /// </summary>
    async Task Check()
    {
        isCheck = true;

        //Supposed: self report; but we don't do this for monkeys
        GFFPhaseFlag = 5;
        SelfReportStart.Add(Time.realtimeSinceStartup);

        // Feedback
        GFFPhaseFlag = 6;
        FeedbackStart.Add(Time.realtimeSinceStartup);

        rewarded = false;

        Vector3 pos;
        Quaternion rot;

        pPos = player.transform.position - new Vector3(0.0f, p_height, 0.0f);
        pos = player.transform.position;
        rot = player.transform.rotation;

        checkWait.Add(0.0f);
        audioSource.clip = loseSound;

        float degree_score;
        Vector3 player_vec = new Vector3(player.transform.position.x, 0, player.transform.position.z);
        Vector3 FF_vec = new Vector3(firefly.transform.position.x, 0, firefly.transform.position.z);
        degree_score = Vector3.Angle(player_vec, FF_vec);
        if (degree_score <= RewardWindow)
        {
            rewarded = true;
        }
        print(string.Format("Window: {0}", RewardWindow));
        print(string.Format("Scored: {0}", degree_score));
        ffPosStr = string.Format("{0},{1},{2}", firefly.transform.position.z, firefly.transform.position.y, firefly.transform.position.x);
        distances.Add(degree_score);

        if (rewarded)
        {
            audioSource.clip = winSound;
            juiceTime = Mathf.Lerp(maxJuiceTime, minJuiceTime, Mathf.InverseLerp(0.0f, RewardWindow, degree_score));
            CIScores.Add(degree_score);
            juiceDuration.Add(juiceTime);
            rewardTime.Add(Time.realtimeSinceStartup);
            audioSource.Play();

            points++;
            SendMarker("j", juiceTime);

            await new WaitForSeconds((juiceTime / 1000.0f) + 0.25f);
        }
        else
        {
            audioSource.clip = loseSound;
            CIScores.Add(0.0f);
            juiceDuration.Add(0.0f);
            rewardTime.Add(0.0f);
            audioSource.Play();

            await new WaitForSeconds((juiceTime / 1000.0f) + 0.25f);
        }

        score.Add(rewarded ? 1 : 0);
        ffPos.Add(ffPosStr);
        dist.Add(distances[0].ToString("F5"));
        cPos.Add(pos.ToString("F5").Trim(toTrim).Replace(" ", ""));
        cRot.Add(rot.ToString("F5").Trim(toTrim).Replace(" ", ""));


        float wait = PlayerPrefs.GetFloat("ITIduration");
        currPhase = Phases.ITI;
        await new WaitForSeconds(wait);
        interWait.Add(wait);

        distances.Clear();
        ffPosStr = "";
        player.transform.position = Vector3.up * p_height;
        player.transform.rotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);

        isEnd = true;

        isIntertrail = true;
        await new WaitForSeconds(wait);

        phase = Phases.begin;
    }

    public async void SendMarker(string mark, float time)
    {
        string toSend = "i" + mark + time.ToString();
        
        switch (mark)
        {
            case "j":
                break;
            case "s":
                beginTime.Add(Time.realtimeSinceStartup - programT0);
                break;
            case "e":
                endTime.Add(Time.realtimeSinceStartup - programT0);
                break;
            default:
                break;
        }

        juiceBox.Write(toSend);
        await new WaitForSeconds(time / 1000.0f);
    }

    public float RandomizeSpeeds(float min, float max)
    {
        return (float)(rand.NextDouble() * (max - min) + min);
    }

    /// <summary>
    /// Data Saving.
    /// </summary>
    public void Save()
    {
        try
        {
            string firstLine;

            List<int> temp;

            StringBuilder csvDisc = new StringBuilder();
            firstLine = "n,max_v,max_w,ffv,onDuration,Answer,PosX0,PosY0,PosZ0,RotX0,RotY0,RotZ0,RotW0,ffX,ffY,ffZ,pCheckX,pCheckY,pCheckZ,rCheckX,rCheckY,rCheckZ,rCheckW,distToFF,rewarded,timeout," +
                "beginTime,checkTime,duration,delays,ITI,endTime,PrepStart,HabituStart,ObservStart,ActionStart,ReportStart,FeedbackStart,CIScore,JuiceDuration,RewardTime"
            + PlayerPrefs.GetString("Name") + "," + PlayerPrefs.GetString("Date") + "," + PlayerPrefs.GetInt("Run Number").ToString("D3");
            csvDisc.AppendLine(firstLine);

        temp = new List<int>()
            {
                n.Count,
                max_v.Count,
                max_w.Count,
                fv.Count,
                onDur.Count,
                origin.Count,
                heading.Count,
                ffPos.Count,
                cPos.Count,
                cRot.Count,
                dist.Count,
                score.Count,
                beginTime.Count,
                checkTime.Count,
                endTime.Count,
                PreparationStart.Count,
                HabituationStart.Count,
                ObservationStart.Count,
                ActionStart.Count,
                FeedbackStart.Count,
                CIScores.Count,
                juiceDuration.Count,
                rewardTime.Count
            };

            temp.Sort();

            var totalScore = 0f;
            for (int i = 0; i < temp[0]; i++)
            {
                var line = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28}",
                        n[i],
                        max_v[i],
                        max_w[i],
                        fv[i],
                        onDur[i],
                        -999,
                        origin[i],
                        heading[i],
                        ffPos[i],
                        cPos[i],
                        cRot[i],
                        dist[i],
                        score[i],
                        0,
                        beginTime[i],
                        checkTime[i],
                        -999,
                        -999,
                        -999,
                        endTime[i],
                        PreparationStart[i],
                        HabituationStart[i],
                        ObservationStart[i],
                        ActionStart[i],
                        -999,
                        FeedbackStart[i],
                        CIScores[i],
                        juiceDuration[i],
                        rewardTime[i]);
                csvDisc.AppendLine(line);

                totalScore += score[i];
            }
            string discPath = path + "/discontinuous_data_" + PlayerPrefs.GetString("Name") + "_" + DateTime.Today.ToString("MMddyyyy") + "_" + PlayerPrefs.GetInt("Run Number").ToString("D3") + ".txt";

            File.WriteAllText(discPath, csvDisc.ToString());

            if (PlayerPrefs.GetFloat("calib") == 0)
            {
                string contpath = path + "/continuous_data_" + PlayerPrefs.GetString("Name") + "_" + DateTime.Today.ToString("MMddyyyy") + "_" + PlayerPrefs.GetInt("Run Number").ToString("D3") + ".txt";
                File.AppendAllText(contpath, sb.ToString());
            }

            PlayerPrefs.SetFloat("Good Trials", totalScore);
            PlayerPrefs.SetInt("Total Trials", n.Count);

            SaveConfigs();
            PlayerPrefs.SetInt("Run Number", PlayerPrefs.GetInt("Run Number") + 1);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError(e);
        }
    }

    void drawLine(float radius, int segments)
    {
        LineRenderer lr;
        lr = BlueCircle.GetComponent<LineRenderer>();
        Vector3[] points = new Vector3[segments + 1];
        for (int i = 0; i < segments; i++)
        {
            float angle = ((float)i / (float)segments) * 360 * Mathf.Deg2Rad;
            float x = Mathf.Sin(angle) * radius;
            float z = Mathf.Cos(angle) * radius;
            points[i] = new Vector3(x, 0f, z);
        }
        points[segments] = points[0];
        lr.positionCount = segments + 1;
        lr.SetPositions(points);
    }

    void Shuffle(List<Tuple<float, float, float, float, float>> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rand.Next(n + 1);
            var value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    public void SaveConfigs()
    {
        print("Saving Configs");

        int trial_count = 0;
        string metaPath = path + "/CIMetaData_" + PlayerPrefs.GetInt("Optic Flow Seed").ToString() + ".txt";
        File.AppendAllText(metaPath, "TrialNum,TrialFFV,TrialSelfMotionSpeed,Selfmotion,ObservCondition,FFmoving\n");
        foreach (var tuple in AllTrials)
        {
            trial_count++;
            string trialtext = string.Format("{0},{1},{2},{3},{4},{5} \n", trial_count, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
            File.AppendAllText(metaPath, trialtext);
        }

        System.IO.Directory.CreateDirectory(path + "/configs/");
        string configPath = path + "/configs/" + "config" + "_" + PlayerPrefs.GetString("Name") + "_" + DateTime.Today.ToString("MMddyyyy") + "_" + PlayerPrefs.GetInt("Run Number").ToString("D3") + ".xml";

        var settings = new XmlWriterSettings();
        settings.OmitXmlDeclaration = true;
        settings.Indent = true;
        settings.NewLineOnAttributes = true;

        XmlWriter xmlWriter = XmlWriter.Create(configPath, settings);

        xmlWriter.WriteStartDocument();

        xmlWriter.WriteStartElement("Settings");

        xmlWriter.WriteStartElement("Setting");
        xmlWriter.WriteAttributeString("Type", "Optic Flow Settings");

        xmlWriter.WriteStartElement("lifeSpan");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("lifeSpan").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("Density");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("Density").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("drawDistance");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("drawDistance").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("tHeight");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("tHeight").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("pHeight");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("pHeight").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("Setting");
        xmlWriter.WriteAttributeString("Type", "Joystick Settings");

        xmlWriter.WriteStartElement("LinearSpeed");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("LinearSpeed").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("AngularSpeed");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("AngularSpeed").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("Setting");
        xmlWriter.WriteAttributeString("Type", "Firefly Settings");

        xmlWriter.WriteStartElement("Size");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("Size").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("Ratio");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("Ratio").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("FFmoveradius");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("FFmoveradius").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("RampUpDur");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("RampUpDur").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("GracePeriod");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("GracePeriod").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("CIFFmean1");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("CIFFmean1").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("CIFFmean2");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("CIFFmean2").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("CIFFSD1");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("CIFFSD1").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("CIFFSD2");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("CIFFSD2").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("RampDownDur");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("RampDownDur").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("ITIduration");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("ITIduration").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("FFacceleration");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("FFacceleration").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("Ratio2Obs");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("Ratio2Obs").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("FFOnTime");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("FFOnTime").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("Setting");
        xmlWriter.WriteAttributeString("Type", "Circular Moving Firefly Settings");

        for(int i = 1; i <= 11; i++)
        {
            string key = "V" + i.ToString();
            xmlWriter.WriteStartElement(key);
            xmlWriter.WriteString(PlayerPrefs.GetFloat(key).ToString());
            xmlWriter.WriteEndElement();
            key = "VR" + i.ToString();
            xmlWriter.WriteStartElement(key);
            xmlWriter.WriteString(PlayerPrefs.GetFloat(key).ToString());
            xmlWriter.WriteEndElement();
        }

        for (int i = 1; i <= 5; i++)
        {
            string key = "SelfMotionSpeed" + i.ToString();
            xmlWriter.WriteStartElement(key);
            xmlWriter.WriteString(PlayerPrefs.GetFloat(key).ToString());
            xmlWriter.WriteEndElement();
            key = "NtrialsSM" + i.ToString();
            xmlWriter.WriteStartElement(key);
            xmlWriter.WriteString(PlayerPrefs.GetFloat(key).ToString());
            xmlWriter.WriteEndElement();
        }

        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("Setting");
        xmlWriter.WriteAttributeString("Type", "Training Reward Settings");

        xmlWriter.WriteStartElement("MinJuiceTime");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("Min Juice Time").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("MaxJuiceTime");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("Max Juice Time").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("RewardWindow");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("RewardWindow").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("FFOpacity");
        xmlWriter.WriteString(PlayerPrefs.GetFloat("FFOpacity").ToString());
        xmlWriter.WriteEndElement();

        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("Setting");
        xmlWriter.WriteAttributeString("Type", "Data Collection Settings");

        xmlWriter.WriteStartElement("Path");
        xmlWriter.WriteString(PlayerPrefs.GetString("Path"));
        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("Name");
        xmlWriter.WriteString(PlayerPrefs.GetString("Name"));
        xmlWriter.WriteEndElement();

        xmlWriter.WriteEndElement();

        xmlWriter.WriteEndElement();

        xmlWriter.WriteEndDocument();
        xmlWriter.Close();
    }
}