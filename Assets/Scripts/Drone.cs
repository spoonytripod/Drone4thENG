using UnityEngine;
using UTMLatLngConverter;
using System;
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

    ////////////////////////////
    public GameObject PixelOBJ;
    ////////////////////////////

    [Header("Camera Property")]
    public float focalLength = 15f;
    public float pixelPitch = 3.28f;
    public int centerX = 2640;
    public int centerY = 1978;
    public float errorCorrection = 0;

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
        SetTargetPosition(ground, 0, 5);
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

        // Calculate and save RESULT
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if ((int)currentMode == 0)
            { RaycastUpdate(); }
            if ((int)currentMode == 1)
            { RaycastUpdatePixel(); }
            ResultUpdate();
        }

        // Calculate and save RESULT (Pixel Version)
        if (Input.GetKeyDown(KeyCode.X))
        {
            if ((int)currentMode == 0)
            { RaycastUpdate(); }
            if ((int)currentMode == 1)
            { RaycastUpdatePixel(); }
            ResultUpdate();
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            // 자동으로 모든 데이터에 대한 계산 결과 저장
            Debug.Log("Enter");

            for (droneID = 1; droneID < drone.Count; droneID++)
            {
                SetDronePosition(drone, droneID);
                groundID = (int)drone[droneID]["ground"] - 1;
                SetGroundPosition(ground, groundID);

                if ((int)currentMode == 0)
                { RaycastUpdate(); }
                if ((int)currentMode == 1)
                { RaycastUpdatePixel(); }
                ResultUpdate();
            }

            Debug.Log("Calculation is done");
        }

        // Visualization
        if (Input.GetKeyDown(KeyCode.Slash))
        {
            this.transform.GetChild(0).gameObject.SetActive(false);
            GroundOBJ.gameObject.SetActive(false);
            HitOBJ.gameObject.SetActive(false);

            foreach (var result in calcResult)
            {
                Debug.Log("Drone ID : " + result.DroneID + " / Ground ID : " + (result.GroundID));

                // Object rendering
                GameObject DemoDroneClone = Instantiate(DemoDroneOBJ, result.Position, result.Angle);
                DemoDroneClone.tag = "Clone";
                GameObject DemoGroundClone = Instantiate(DemoGroundOBJ, result.Ground, result.Angle); // Angle은 gameobject가 구체이므로 상관 없음
                DemoGroundClone.tag = "Clone";
                GameObject DemoHitClone = Instantiate(DemoHitOBJ, result.Hit.point, result.Angle); // Angle은 gameobject가 구체이므로 상관 없음
                DemoHitClone.tag = "Clone";

                // Line rendering
                LineRenderer LR = DemoDroneClone.GetComponent<LineRenderer>();
                LR.startColor = Color.red;
                LR.endColor = Color.red;
                LR.startWidth = 0.1f;
                LR.endWidth = 0.1f;
                LR.SetPosition(0, result.Position);
                LR.SetPosition(1, result.Hit.point);
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
        public RaycastHit Hit;
        public Vector3 Position;
        public Quaternion Angle;
        public Vector3 Forward;
        public Vector3 Ground;
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
            Convert.ToSingle(Drone[ID]["head"]) + Convert.ToSingle(Drone[ID]["yaw"]) + errorCorrection,
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
            Debug.Log("Point Update");
        }
        else
        {
            Debug.LogWarning("No Hitted Object");
        }
    }

    void RaycastUpdatePixel()
    {
        // Initialize drone position
        SetDronePosition(drone, droneID);

        int pixelX = (int)drone[droneID]["pixelx"];
        int pixelY = (int)drone[droneID]["pixely"];

        // Move raycast origin to principal point
        this.transform.position -= this.transform.forward * (focalLength / 1000);
        float imageX = (pixelX - centerX) * pixelPitch / 1000000;
        float imageY = (centerY - pixelY) * pixelPitch / 1000000;
        float imageZ = (focalLength / 1000);

        Vector3 forwardToPixel = new Vector3(imageX, imageY, imageZ); // Direction to designated pixel coordinates
        Quaternion forwardRotation = Quaternion.LookRotation(forwardToPixel);
        // Rotate raycast forward with designated pixel coordinate
        this.transform.Rotate(forwardRotation.eulerAngles);

        if (Physics.Raycast(this.transform.position, this.transform.forward, out hitRay))
        {
            Vector3 forward = transform.TransformDirection(Vector3.forward) * hitRay.distance;
            PixelOBJ.transform.position = hitRay.point;
            Debug.DrawRay(transform.position, forward, Color.blue, 10);
            Debug.Log("Point Update");
        }
        else
        {
            Debug.LogWarning("No Hitted Object");
        }
    }

    void ResultUpdate()
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

        CalculationResult result = new CalculationResult();
        result.DroneID = (int)drone[droneID]["number"];
        result.GroundID = (int)ground[groundID]["number"];
        result.Hit = hitRay;
        result.Position = this.transform.position;
        result.Angle = this.transform.rotation;
        result.Forward = this.transform.forward;
        result.Ground = GroundOBJ.transform.position;
        calcResult.Add(result);

        //Debug.Log("Target Latitude : " + hitGPS.Latitude);
        //Debug.Log("Target Longitude : " + hitGPS.Longitude);
        //Debug.Log("Drone Latitude : " + droneGPS.Latitude);
        //Debug.Log("Drone Longitude : " + droneGPS.Longitude);
        //Debug.Log("Drone Height : " + (droneUnity.y + offsetHeight));
        //Debug.Log("Drone UTM(E) : " + droneUTM.Easting);
        //Debug.Log("Drone UTM(N) : " + droneUTM.Northing);
    }
}