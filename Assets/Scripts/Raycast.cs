using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UTMLatLngConverter;
using InfiniteLoopDetector;
using System;

public class Raycast : MonoBehaviour
{
    RaycastHit hitInfo;
    public GameObject target;
    public float Lat; // set drone's latitude
    public float Long; // set drone's longitude
    public float Height; // set drone's height
    // public TextAsset textFile; 좌표 정보를 텍스트파일로 받을 때
    private int count = 0;
    private double[,] ground = new double[,]
        {
            // { location, latitude, longitude, altitude }
            { 0187, 37.218985, 127.184125, 125.064 },
            { 0189, 37.219098, 127.183944, 125.023 },
            { 0190, 37.218857, 127.183460, 124.84 },
            { 0191, 37.218714, 127.183591, 125.097 },
        };

    void Awake()
    {
        Application.targetFrameRate = 60;        
    }

    // Start is called before the first frame update
    void Start()
    {
        var utm_ground = new double[4, 3];                

        while (count < 4)
        {
            var latLng = new LatLngCoords(ground[count, 1], ground[count, 2]);
            var conv_utm = CoordsConverter.ToUTM(latLng);

            utm_ground[count, 0] = conv_utm.Easting - 338857.038826874;
            utm_ground[count, 1] = conv_utm.Northing - 4120702.74191967;
            utm_ground[count, 2] = ground[count, 3];

            count++;
            Debug.Log("Count : " + count);
            Debug.Log("No." + count + " Easting : " + conv_utm.Easting.ToString());
            Debug.Log("No." + count + " Northing : " + conv_utm.Northing.ToString());

            InfiniteLoopDetector.InfiniteLoopDetector.Run();
        }

        var latLng_drone = new LatLngCoords(Lat, Long);
        var utm_drone = CoordsConverter.ToUTM(latLng_drone);
        
        AlignTarget();
        PointUpdate();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PointUpdate();

            var utm_target = new UTMCoords(hitInfo.point.x + 338857.038826874, hitInfo.point.z + 4120702.74191967, 'S', 52);
            var latLng_target = CoordsConverter.ToLatLng(utm_target);

            Debug.Log("Target Latitude : " + latLng_target.Latitude);
            Debug.Log("Target Longitude : " + latLng_target.Longitude);

            
        }

        // Debug.DrawRay(transform.position, hitInfo.point, Color.red);
    }
     
    void PointUpdate()
    {
        if (Physics.Raycast(this.transform.position, this.transform.forward, out hitInfo))
        {
            Debug.Log("Drone position : " + transform.position);
            Debug.Log("Point : " + hitInfo.point);
            Debug.Log("Distance : " + hitInfo.distance);
            Debug.Log("Colliding object : " + hitInfo.collider);

            // hitInfo.transform.gameObject.GetComponent();
        }
        else
        {
            Debug.Log("No Colliding Object");
        }
    }

    void Position()
    {

    }
    
    void AlignTarget()
    {

        var tr = target.GetComponent<Transform>();


        tr.position = new Vector3(0, 0, 0);
        // tr.LookAt(align.position);
    }
       

    }
