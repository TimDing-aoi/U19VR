using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO.Ports;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine.InputSystem.LowLevel;
using static Monkey2D;

public class JoystickMonke : MonoBehaviour
{
    public static JoystickMonke SharedJoystick;

    //Joystick inputs
    public float moveX;
    public float moveY;
    public float rawX;
    public float rawY;

    //Player Polar coordinates
    public float circX;//Player circular x (rotation) position in radians
    public float circY = 0;//Player circular y (radius) position from origin

    //Causal Inference Max rot speed
    public float maxJoyRotDeg = 0.0f;// deg/s

    //Causal Inference linear speed
    public float LinSpeed = 0.0f;

    //Counters
    private float hbobCounter = 0;
    private float accelCounter = 0;
    private float decelCounter = 0;
    private float feedbackCounter = 0;

    //Frame rate
    private static readonly float frameRate = 90f;

    //Joystick and prev positions
    CTIJoystick USBJoystick;
    float prevX = 0.0f;
    float prevY = 0.0f;

    // Start is called before the first frame update
    void Awake()
    {
        SharedJoystick = this;
    }

    void Start()
    {
        USBJoystick = CTIJoystick.current;
        LinSpeed = PlayerPrefs.GetFloat("LinearSpeed");
        maxJoyRotDeg = PlayerPrefs.GetFloat("AngularSpeed");
    }

    private void FixedUpdate()
    {
        moveX = -USBJoystick.x.ReadValue();
        moveY = -USBJoystick.y.ReadValue();

        if (moveX < 0.0f)
        {
            moveX += 1.0f;
        }
        else if (moveX > 0.0f)
        {
            moveX -= 1.0f;
        }
        else if (moveX == 0)
        {
            moveX += Math.Sign(prevX);
        }
        prevX = moveX;

        if (moveY < 0.0f)
        {
            moveY += 1.0f;
        }
        else if (moveY > 0.0f)
        {
            moveY -= 1.0f;
        }
        else if (moveY == 0)
        {
            moveY += Math.Sign(prevY);
        }
        prevY = moveY;

        rawX = moveY;
        rawY = moveX;
        moveX = moveY;
        moveY = 1f;



        if (SharedMonkey.GFFPhaseFlag == 6)
        //feedback
        {
            float SMspeed = 0.01f;//Mathf.Abs(SharedMonkey.SelfMotionSpeed / frameRate);
            print(SMspeed);
            Quaternion currentRotation = transform.rotation;
            Vector3 player_vec = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 FF_vec = new Vector3(SharedMonkey.firefly.transform.position.x, 0, SharedMonkey.firefly.transform.position.z);
            float degree_score = Vector3.Angle(player_vec, FF_vec);
            float degrees_to_rotate = 0;
            if (SharedMonkey.GFFTrueRadians > circX)
            {
                SMspeed = -SMspeed;
                degrees_to_rotate = 90 + degree_score / 2;
            }
            else
            {
                degrees_to_rotate = 90 - degree_score / 2;
            }
            if (feedbackCounter < degrees_to_rotate)
            {
                currentRotation.y += SMspeed;
                transform.rotation = currentRotation;
            }
        }
        else if (Vector3.Distance(new Vector3(0f, 0f, 0f), transform.position) > SharedMonkey.FFMoveRadius || SharedMonkey.GFFPhaseFlag == 1
            || SharedMonkey.GFFPhaseFlag == 2 || !SharedMonkey.selfmotiontrial && SharedMonkey.GFFPhaseFlag == 3)
        //Out of circle(Feedback) OR Preparation & Habituation & No selfmotion's Observation
        {
            transform.rotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
            moveY = 0;
            circY = 0;
            accelCounter = 0;
            decelCounter = 0;
            hbobCounter = 0;
            feedbackCounter = 0;
            circX = 0;
        }
        else if (SharedMonkey.selfmotiontrial && SharedMonkey.GFFPhaseFlag == 2.5)
        //Selfmotion Ramp Up
        {
            float updur = PlayerPrefs.GetFloat("RampUpDur");
            float accelTime = Mathf.Ceil(updur * 90);
            float SMspeed = SharedMonkey.SelfMotionSpeed;
            float acceleration = (accelCounter / accelTime) * SMspeed;
            transform.rotation = Quaternion.Euler(0.0f, 90.0f + hbobCounter, 0.0f);
            moveY = 0;
            circY = 0;
            accelCounter++;
            hbobCounter += acceleration / frameRate;
            if (hbobCounter > 0)
            {
                circX = (360 - hbobCounter) * Mathf.Deg2Rad;
            }
            else
            {
                circX = -hbobCounter * Mathf.Deg2Rad;
            }
        }
        else if (SharedMonkey.selfmotiontrial && SharedMonkey.GFFPhaseFlag == 3.5)
        //Selfmotion Ramp Down
        {
            float downdur = PlayerPrefs.GetFloat("RampDownDur");
            if(downdur != 0)
            {
                float decelTime = Mathf.Ceil(downdur * 90);
                float SMspeed = SharedMonkey.SelfMotionSpeed;
                float deceleration = (1 - (decelCounter / decelTime)) * SMspeed;
                transform.rotation = Quaternion.Euler(0.0f, 90.0f + hbobCounter, 0.0f);
                moveY = 0;
                circY = 0;
                decelCounter++;
                hbobCounter += deceleration / frameRate;
                if (hbobCounter > 0)
                {
                    circX = (360 - hbobCounter) * Mathf.Deg2Rad;
                }
                else
                {
                    circX = -hbobCounter * Mathf.Deg2Rad;
                }
            }
        }
        else if (SharedMonkey.selfmotiontrial && SharedMonkey.GFFPhaseFlag == 3)
        //Selfmotion Observation
        {
            float SMspeed = SharedMonkey.SelfMotionSpeed / frameRate;
            transform.rotation = Quaternion.Euler(0.0f, 90.0f + hbobCounter, 0.0f);
            moveY = 0;
            circY = 0;
            hbobCounter += SMspeed;
            if (hbobCounter > 0)
            {
                circX = (360 - hbobCounter) * Mathf.Deg2Rad;
            }
            else
            {
                circX = -hbobCounter * Mathf.Deg2Rad;
            }
        }
        else if (SharedMonkey.GFFPhaseFlag == 4)
        //action
        {
            float fixedSpeed = PlayerPrefs.GetFloat("LinearSpeed"); // in meter per second

            circY += Time.deltaTime;

            float joyConvRateDeg = maxJoyRotDeg / frameRate;
            float theta = joyConvRateDeg * moveX; // moveX consider to be in degree; We use joyConvRate in Degree
            theta *= Mathf.Deg2Rad;
            circX -= theta;
            float x = Mathf.Cos(circX);
            float z = Mathf.Sin(circX);

            transform.position = new Vector3(fixedSpeed * circY * x, 0f, fixedSpeed * circY * z);
            transform.LookAt(new Vector3(0f, 0f, 0f));
            transform.Rotate(0f, 180f, 0f);
            transform.position = new Vector3(fixedSpeed * circY * x, 1f, fixedSpeed * circY * z);
        }
    }
}