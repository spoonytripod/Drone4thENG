using UnityEngine;
using UTMLatLngConverter;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;


public class Drone : MonoBehaviour
{

    public enum Mode { Centerpoint, Calibration }
    public enum ECMode { AllRotation, YawOnly, YawAlt }

    [Header("General settings")]
    public Mode currentMode;
    public string groundlist;
    public string dronelist;

    [Header("Object settings")]
    public GameObject TargetOBJ;
    [Range(1, 26)]public int originTargetID = 1;
    public GameObject DroneOBJ;
    public GameObject GroundOBJ;
    public GameObject HitmarkerOBJ;
    public GameObject DemoDroneOBJ;
    public GameObject DemoGroundOBJ;
    public GameObject DemoHitOBJ;
    public GameObject PixelOBJ;

    [Header("Camera property")]
    public float focalLength = 15f;
    public float pixelPitch = 3.28f;
    public int centerX = 2640;
    public int centerY = 1978;

    [Header("Error correction settings")]
    public ECMode currentECMode;
    [Range(1, 50)] public int startSampleNumber = 1;
    public int sampleSize = 5;

    // Initialize variables    
    private double originEast = 0;
    private double originNorth = 0;
    private double originHeight = 0;
    private int groundID = 0;
    private int droneID = 0;
    private List<Dictionary<string, object>> ground;
    private List<Dictionary<string, object>> drone;
    private List<CalculationResult> calcResult = new List<CalculationResult>();
    RaycastHit hitRay;
    float correctionAltitude = new float();
    Vector3 correctionRotation = new Vector3();
    Quaternion correctionQuaternion = new Quaternion();

    void Awake()
    {

    }

    void Start()
    {
        Application.targetFrameRate = 60;
        Debug.ClearDeveloperConsole();

        // Load data from CSVs
        ground = ReadCSV.Read(groundlist);
        drone = ReadCSV.Read(dronelist);

        // Set coordinates origin
        originEast = (double)ground[originTargetID - 1]["easting"];
        originNorth = (double)ground[originTargetID - 1]["northing"];
        originHeight = (double)ground[originTargetID - 1]["height"];

        UTMtoUnity(ground); // Convert UTM coord. of [GROUND] to Unity coord.
        GPStoUnity(drone); // Convert GPS coord. of [DRONE] to Unity coord.
        SetDronePosition(drone, droneID);
        SetTargetPosition(ground, 0, 5); // Align a target 3D model using CP 1, 6
    }

    // Update is called once per frame
    void Update()
    {
        // (PRESS ←→) Select DRONE POSITION
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (droneID == drone.Count)
            {
                Debug.LogWarning("Drone index upper bound has been reached");
                Debug.Log("Image ID : " + drone[droneID]["number"]);
            }
            else
            {
                droneID += 1;
                SetDronePosition(drone, droneID);
                Debug.Log("Image ID : " + drone[droneID]["number"]);
            }
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (droneID == 0)
            {
                Debug.LogWarning("Drone index lower bound has been reached");
                Debug.Log("Image ID : " + drone[droneID]["number"]);
            }
            else
            {
                droneID -= 1;
                SetDronePosition(drone, droneID);
                Debug.Log("Image ID : " + drone[droneID]["number"]);
            }
        }

        // (PRESS ↑↓) Select TARGET(GROUND) POSITION
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (groundID == (ground.Count - 1))
            {
                Debug.LogWarning("Target(Ground) index upper bound has been reached");
                Debug.Log("Target(Ground) ID : " + (groundID + 1));
            }
            else
            {
                groundID += 1;
                SetGroundPosition(ground, groundID);
                Debug.Log("Target(Ground) ID : " + ground[groundID]["number"]);
            }
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (groundID == 0)
            {
                Debug.LogWarning("Target(Ground) index upper bound has been reached");
                Debug.Log("Target(Ground) ID : " + (groundID + 1));
            }
            else
            {
                groundID -= 1;
                SetGroundPosition(ground, groundID);
                Debug.Log("Target(Ground) ID : " + ground[groundID]["number"]);
            }
        }

        // (PRESS Enter) Calculate and save RESULT
        // (PRESS LShift+Enter) Calculate and save "ALL" RESULTS
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                for (droneID = 1; droneID < drone.Count; droneID++)
                {
                    groundID = (int)drone[droneID]["ground"] - 1;

                    SetDronePosition(drone, droneID);
                    SetGroundPosition(ground, groundID);

                    switch (currentMode)
                    {
                        case Mode.Centerpoint:
                            RaycastUpdate();
                            break;
                        case Mode.Calibration:
                            RaycastUpdateCalib();
                            break;
                    }
                    ResultUpdate();
                    SaveResult();
                }
            }
            else
            {
                switch (currentMode)
                {
                    case Mode.Centerpoint:
                        RaycastUpdate();
                        break;
                    case Mode.Calibration:
                        RaycastUpdateCalib();
                        break;
                }
                ResultUpdate();
                SaveResult();
            }
        }

        // (PRESS E) Error correction
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Initialization
            droneID = startSampleNumber;
            correctionRotation = Vector3.zero;
            correctionQuaternion = Quaternion.identity;
            correctionAltitude = 0;
            float[] altError = new float[sampleSize];

            List<Quaternion> quaternionErrorList = new List<Quaternion>();
            FileStream fs = new FileStream("Assets/errcrr.csv", FileMode.Append, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.Unicode);

            sw.WriteLine("EC Mode : {0},Start ID : {1},End ID : {2}", currentECMode, startSampleNumber, startSampleNumber + sampleSize - 1);
            sw.WriteLine("Error x,Error y,Error z,Error alt,Distance hit");

            // Calculation errors
            for (int ecID = 1; ecID <= sampleSize; ecID++)
            {
                groundID = (int)drone[droneID]["ground"] - 1;

                SetDronePosition(drone, droneID);
                SetGroundPosition(ground, groundID);

                switch (currentMode)
                {
                    case Mode.Centerpoint:
                        RaycastUpdate();
                        break;
                    case Mode.Calibration:
                        RaycastUpdateCalib();
                        break;
                }
                ResultUpdate();

                droneID++;
            }

            // Calculate rotation errors
            foreach (var result in calcResult.Select((value, index) => (value, index)))
            {
                Quaternion quaternionToground = Quaternion.LookRotation(result.value.GroundPosition - result.value.DronePosition);
                Quaternion quaternionTohit = Quaternion.LookRotation(result.value.HitRay.point - result.value.DronePosition);

                Vector3 rotationToground = quaternionToground.eulerAngles;
                Vector3 rotationTohit = quaternionTohit.eulerAngles;
                Vector3 rotationError = rotationToground - rotationTohit; // Error between 'rotation to ground' and 'rotation to hit'

                altError[result.index] = result.value.HitRay.point.y - result.value.GroundPosition.y; // Calculate altitude error

                switch (currentECMode)
                {
                    case ECMode.YawOnly:
                        rotationError.x = 0;
                        rotationError.z = 0;
                        break;
                    case ECMode.YawAlt:
                        rotationError.x = 0;
                        rotationError.z = 0;
                        correctionAltitude = altError.Average(); // Average altitude errors
                        break;
                }

                Quaternion quaternionError = Quaternion.Euler(rotationError); // Convert rotation to quaternion
                quaternionErrorList.Add(quaternionError);                
                
                sw.WriteLine("{0},{1},{2},{3},{4}", 
                    rotationError.x.ToString("F3"), 
                    rotationError.y.ToString("F3"), 
                    rotationError.z.ToString("F3"),
                    altError[result.index].ToString("F3"),
                    result.value.HitRay.distance.ToString("F3"));

                calcResult = new List<CalculationResult>(); // Initialize calculation result
            }

            // Averaging rotation errors
            Quaternion[] quaternions = new Quaternion[quaternionErrorList.Count];
            quaternions = quaternionErrorList.ToArray(); // Convert list to array

            int count = quaternions.Length;
            float weight = 1.0f / (float)count;
            Quaternion quaternionAvg = Quaternion.identity; // Initialize quaternion

            for (int i = 0; i < count; i++)
            {
                quaternionAvg *= Quaternion.Slerp(Quaternion.identity, quaternions[i], weight);
            }

            // Allocate corrections
            correctionQuaternion = quaternionAvg;
            correctionRotation = correctionQuaternion.eulerAngles;

            // Save correction data
            sw.WriteLine("correction x,correction y,correction z,correction alt");
            sw.WriteLine("{0},{1},{2},{3}", 
                correctionRotation.x.ToString("F3"), 
                correctionRotation.y.ToString("F3"), 
                correctionRotation.z.ToString("F3"), 
                correctionAltitude.ToString("F3"));
            sw.Close();

            Debug.Log("Correction quaternion in Euler : " + correctionRotation);
            Debug.Log("Correction Altitude : " + correctionAltitude);
        }

        // (PRESS R) Initialize error correction vector
        if (Input.GetKeyDown(KeyCode.R))
        {
            droneID = 0;
            correctionRotation = Vector3.zero;
            correctionQuaternion = Quaternion.identity;
            correctionAltitude = 0;

            Debug.Log(correctionQuaternion.eulerAngles);
        }

        // (PRESS V) Visualize caculated results
        if (Input.GetKeyDown(KeyCode.V))
        {
            this.transform.GetChild(0).gameObject.SetActive(false);
            GroundOBJ.gameObject.SetActive(false);
            HitmarkerOBJ.gameObject.SetActive(false);

            foreach (var result in calcResult)
            {
                Debug.Log("Drone ID : " + result.DroneID + " / Ground ID : " + (result.GroundID));

                // Object rendering
                GameObject DemoDroneClone = Instantiate(DemoDroneOBJ, result.DronePosition, result.Rotation);
                DemoDroneClone.tag = "Clone";
                GameObject DemoGroundClone = Instantiate(DemoGroundOBJ, result.GroundPosition, result.Rotation); // 여기서 Angle은 gameobject가 구체이므로 무효함
                DemoGroundClone.tag = "Clone";
                GameObject DemoHitClone = Instantiate(DemoHitOBJ, result.HitRay.point, result.Rotation); // 여기서 Angle은 gameobject가 구체이므로 무효함
                DemoHitClone.tag = "Clone";

                // Line rendering
                LineRenderer LR = DemoDroneClone.GetComponent<LineRenderer>();
                LR.startColor = Color.red;
                LR.endColor = Color.red;
                LR.startWidth = 0.1f;
                LR.endWidth = 0.1f;
                LR.SetPosition(0, result.DronePosition);
                LR.SetPosition(1, result.HitRay.point);
            }
        }

        // (PRESS Delete) Destroy all cloned game objects
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            this.transform.GetChild(0).gameObject.SetActive(true);
            GroundOBJ.gameObject.SetActive(true);
            HitmarkerOBJ.gameObject.SetActive(true);

            GameObject[] cloneObjects;
            cloneObjects = GameObject.FindGameObjectsWithTag("Clone");

            for (int i = 0; i < cloneObjects.Length; i++)
            {
                Destroy(cloneObjects[i]);
            }
        }
    }

    class CalculationResult
    {
        public int DroneID;
        public int GroundID;
        public RaycastHit HitRay;
        public Vector3 DronePosition;
        public Quaternion Rotation;
        public Vector3 Forward;
        public Vector3 GroundPosition;
    }

    void UTMtoUnity(List<Dictionary<string, object>> UTMcoord)
    {
        for (int count = 0; count < UTMcoord.Count; count++)
        {
            UTMcoord[count]["easting"] = (double)UTMcoord[count]["easting"] - originEast;
            UTMcoord[count]["northing"] = (double)UTMcoord[count]["northing"] - originNorth;
            UTMcoord[count]["height"] = (double)UTMcoord[count]["height"] - originHeight;

            InfiniteLoopDetector.InfiniteLoopDetector.Run();
        }
    }

    void GPStoUnity(List<Dictionary<string, object>> GPScoord)
    {
        for (int count = 0; count < GPScoord.Count; count++)
        {
            var GPS = new LatLngCoords((double)GPScoord[count]["lat"], (double)GPScoord[count]["long"]);
            var UTM = CoordsConverter.ToUTM(GPS);
            double GPSaltitude = (double)GPScoord[count]["alt"];

            GPScoord[count]["lat"] = UTM.Easting - originEast;
            GPScoord[count]["long"] = UTM.Northing - originNorth;
            GPScoord[count]["alt"] = GPSaltitude - originHeight;

            InfiniteLoopDetector.InfiniteLoopDetector.Run();
        }
    }

    void SetTargetPosition(List<Dictionary<string, object>> Ground, int Index1, int Index2)
    {
        // Set target object in position
        TargetOBJ.transform.position = new Vector3(
            Convert.ToSingle(Ground[Index1]["easting"]),
            Convert.ToSingle(Ground[Index1]["height"]),
            Convert.ToSingle(Ground[Index1]["northing"]));
        TargetOBJ.transform.LookAt(new Vector3(
            Convert.ToSingle(Ground[Index2]["easting"]),
            Convert.ToSingle(Ground[Index2]["height"]),
            Convert.ToSingle(Ground[Index2]["northing"])));
    }

    void SetDronePosition(List<Dictionary<string, object>> Drone, int ID)
    {
        // Drone object positioning
        this.transform.position = new Vector3(
            Convert.ToSingle(Drone[ID]["lat"]),
            Convert.ToSingle(Drone[ID]["alt"]) - correctionAltitude,
            Convert.ToSingle(Drone[ID]["long"]));
        this.transform.eulerAngles = new Vector3(
            Convert.ToSingle(-Convert.ToSingle(Drone[ID]["pitch"])),
            Convert.ToSingle(Drone[ID]["head"]) + Convert.ToSingle(Drone[ID]["yaw"]),
            Convert.ToSingle(-Convert.ToSingle(Drone[ID]["roll"])));
    }

    void SetGroundPosition(List<Dictionary<string, object>> Ground, int ID)
    {
        GroundOBJ.transform.position = new Vector3(
                    Convert.ToSingle(Ground[ID]["easting"]),
                    Convert.ToSingle(Ground[ID]["height"]),
                    Convert.ToSingle(Ground[ID]["northing"]));
    }

    void RaycastUpdate()
    {
        if (Physics.Raycast(this.transform.position, this.transform.forward, out hitRay))
        {
            Vector3 forward = transform.TransformDirection(Vector3.forward) * hitRay.distance;
            HitmarkerOBJ.transform.position = hitRay.point;
            Debug.DrawRay(transform.position, forward, Color.red, 10);
            Debug.Log("Hitted Point Updated");
        }
        else
        {
            Debug.LogWarning("No Hitted Point");
        }
    }

    void RaycastUpdateCalib()
    {
        int pixelX = (int)drone[droneID]["pixelx"];
        int pixelY = (int)drone[droneID]["pixely"];

        // Move origin of raycast to principal point
        this.transform.position -= this.transform.forward * (focalLength / 1000); // Convert to meters
        float imageX = (pixelX - centerX) * pixelPitch / 1000000; // Convert to meters
        float imageY = (centerY - pixelY) * pixelPitch / 1000000; // Convert to meters
        float imageZ = (focalLength / 1000); // Convert to meters

        Vector3 forwardToPixel = new Vector3(imageX, imageY, imageZ); // Direction to designated pixel coordinates
        Quaternion forwardRotation = Quaternion.LookRotation(forwardToPixel);

        this.transform.Rotate(forwardRotation.eulerAngles + correctionRotation);

        // Part from RaycastUpdate()
        if (Physics.Raycast(this.transform.position, this.transform.forward, out hitRay))
        {
            Vector3 forward = transform.TransformDirection(Vector3.forward) * hitRay.distance;
            PixelOBJ.transform.position = hitRay.point;
            Debug.DrawRay(transform.position, forward, Color.blue, 10);
            Debug.Log("Hitted Point Updated");
        }
        else
        {
            Debug.LogWarning("No Hitted Point");
        }
    }

    void ResultUpdate()
    {
        CalculationResult result = new CalculationResult();
        result.DroneID = (int)drone[droneID]["number"];
        result.GroundID = (int)ground[groundID]["number"];
        result.HitRay = hitRay;
        result.DronePosition = this.transform.position;
        result.Rotation = this.transform.rotation;
        result.Forward = this.transform.forward;
        result.GroundPosition = GroundOBJ.transform.position;
        calcResult.Add(result);
    }

    void SaveResult()
    {
        var hitUTM = new UTMCoords(hitRay.point.x + originEast, hitRay.point.z + originNorth, 'S', 52);
        var hitGPS = CoordsConverter.ToLatLng(hitUTM);

        var droneUnity = this.transform.position;
        var droneUTM = new UTMCoords(droneUnity.x + originEast, droneUnity.z + originNorth, 'S', 52);
        var droneGPS = CoordsConverter.ToLatLng(droneUTM);

        List<double> list = new List<double>();
        list.Add(Convert.ToSingle(drone[droneID]["number"]));
        list.Add(Convert.ToSingle(ground[groundID]["number"]));
        list.Add(hitRay.point.x);
        list.Add(hitRay.point.y);
        list.Add(hitRay.point.z);
        list.Add(GroundOBJ.transform.position.x);
        list.Add(GroundOBJ.transform.position.y);
        list.Add(GroundOBJ.transform.position.z);
        list.Add(droneUTM.Easting);
        list.Add(droneUTM.Northing);
        MakeCSV.Save(list);
    }
}