using UnityEngine;
using UTMLatLngConverter;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;


public class Drone : MonoBehaviour
{
    public enum Mode { Centerpoint, Calibration }
    public Mode currentMode;
    public string groundlist;
    public string dronelist;

    [Header("Object Settings")]
    public GameObject TargetOBJ;
    public GameObject DroneOBJ;
    public GameObject GroundOBJ;
    public GameObject HitOBJ;
    public GameObject DemoDroneOBJ;
    public GameObject DemoGroundOBJ;
    public GameObject DemoHitOBJ;
    public GameObject PixelOBJ;

    [Header("Camera Property")]
    public float focalLength = 15f;
    public float pixelPitch = 3.28f;
    public int centerX = 2640;
    public int centerY = 1978;

    [Header("Error Correction")]
    public int startSample = 1;
    [Range(1, 15)] public int sampleSize = 5;

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

        // Set Origin
        originEast = (double)ground[0]["easting"];
        originNorth = (double)ground[0]["northing"];
        originHeight = (double)ground[0]["height"];

        UTMtoUnity(ground); // convert UTM coord. of [ground] to Unity coord.
        GPStoUnity(drone); // convert GPS coord. of [drone] to Unity coord.
        SetDronePosition(drone, droneID);
        SetTargetPosition(ground, 0, 5); // Align a target 3D model using CP 1, 6
    }

    // Update is called once per frame
    void Update()
    {
        // Select DRONE POSITION
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

        // Select TARGET(GROUND) POSITION
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
            correctionRotation = new Vector3(0, 0, 0);
            correctionQuaternion = new Quaternion(0, 0, 0, 0);

            for (droneID = startSample; droneID <= sampleSize; droneID++)
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
            }

            /*// #1 Vector correction method
            List<Vector3> rotationDiffList = new List<Vector3>();

            foreach (var result in calcResult)
            {
                Quaternion quaternionToground = Quaternion.LookRotation(result.GroundPosition - result.DronePosition);
                Quaternion quaternionTohit = Quaternion.LookRotation(result.HitRay.point - result.DronePosition);

                Vector3 rotationToground = quaternionToground.eulerAngles;
                Vector3 rotationTohit = quaternionTohit.eulerAngles;
                Vector3 rotationDiff = rotationToground - rotationTohit; // Difference between 'rotation to ground' and 'rotation to hit'

                rotationDiffList.Add(rotationDiff);

                // Debug (raycast visualization)
                Vector3 forwardToground = quaternionToground * Vector3.forward * result.HitRay.distance;
                Vector3 forwardTohit = quaternionTohit * Vector3.forward * result.HitRay.distance;
                Debug.DrawRay(result.DronePosition, forwardToground, Color.cyan, 10);
                Debug.DrawRay(result.DronePosition, forwardTohit, Color.red, 10);

                Debug.Log("Drone ID : " + result.DroneID);
                Debug.Log("Rotation to ground : " + rotationToground);
                Debug.Log("Rotation to hit : " + rotationTohit);
                Debug.Log("Rotation difference : " + rotationDiff);
                Debug.Log("-----------------------------------------------");
            }
            
            correctionRotation = new Vector3(
                rotationDiffList.Average(x => x.x),
                rotationDiffList.Average(x => x.y),
                rotationDiffList.Average(x => x.z));*/

            // #2 Quaternion correction method
            List<Quaternion> quaternionDiffList = new List<Quaternion>();

            foreach (var result in calcResult)
            {
                Quaternion quaternionToground = Quaternion.LookRotation(result.GroundPosition - result.DronePosition);
                Quaternion quaternionTohit = Quaternion.LookRotation(result.HitRay.point - result.DronePosition);

                Vector3 rotationToground = quaternionToground.eulerAngles;
                Vector3 rotationTohit = quaternionTohit.eulerAngles;
                Vector3 rotationDiff = rotationToground - rotationTohit; // Difference between 'rotation to ground' and 'rotation to hit'

                Quaternion quaternionDiff = Quaternion.Euler(rotationDiff); // Convert rotation to quaternion
                quaternionDiffList.Add(quaternionDiff);
            }
            
            Quaternion[] quaternions = new Quaternion[quaternionDiffList.Count];
            quaternions = quaternionDiffList.ToArray(); // Convert list to array

            int count = quaternions.Length;
            float weight = 1.0f / (float)count;
            Quaternion quaternionAvg = Quaternion.identity; // Initialize quaternion

            for (int i = 0; i < count; i++)
            {
                quaternionAvg *= Quaternion.Slerp(Quaternion.identity, quaternions[i], weight);
            }

            Debug.Log("Correction quaternion in Euler: " + quaternionAvg.eulerAngles);
            correctionQuaternion = quaternionAvg;
        }

        // (PRESS R) Initialize error correction vector
        if (Input.GetKeyDown(KeyCode.R))
        {
            correctionRotation = new Vector3(0, 0, 0);
            correctionQuaternion = new Quaternion(0, 0, 0, 0);

            Debug.Log(correctionQuaternion.eulerAngles);
        }

        // (PRESS V) Visualize caculated results
        if (Input.GetKeyDown(KeyCode.V))
        {
            this.transform.GetChild(0).gameObject.SetActive(false);
            GroundOBJ.gameObject.SetActive(false);
            HitOBJ.gameObject.SetActive(false);

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

        // Destroy all cloned game objects
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            this.transform.GetChild(0).gameObject.SetActive(true);
            GroundOBJ.gameObject.SetActive(true);
            HitOBJ.gameObject.SetActive(true);

            GameObject[] cloneObjects;
            cloneObjects = GameObject.FindGameObjectsWithTag("Clone");

            for (int num = 0; num < cloneObjects.Length; num++)
            {
                Destroy(cloneObjects[num]);
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
            double tempHeight = (double)GPScoord[count]["alt"];

            GPScoord[count]["lat"] = UTM.Easting - originEast;
            GPScoord[count]["long"] = UTM.Northing - originNorth;
            GPScoord[count]["alt"] = tempHeight - originHeight;

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
            Convert.ToSingle(Drone[ID]["alt"]),
            Convert.ToSingle(Drone[ID]["long"]));
        this.transform.eulerAngles = new Vector3(
            Convert.ToSingle(-Convert.ToSingle(Drone[ID]["pitch"])),
            Convert.ToSingle(Drone[ID]["head"]) + Convert.ToSingle(Drone[ID]["yaw"]),
            Convert.ToSingle(Drone[ID]["roll"]));
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
            HitOBJ.transform.position = hitRay.point;
            Debug.DrawRay(transform.position, forward, Color.red, 10);
            Debug.Log("Hitted Point Updated");
        }
        else
        {
            Debug.LogWarning("No Hitted Object");
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
        
        this.transform.Rotate(forwardRotation.eulerAngles + correctionQuaternion.eulerAngles);

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
            Debug.LogWarning("No Hitted Object");
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