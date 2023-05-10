using UnityEngine;
using System.Collections.Generic;
using System;

public class Target : MonoBehaviour
{
    static GameObject TargetOBJ; // object를 inspector 창에서 정의할 수 없어서 생기는 오류 같은데 어떻게 해야할지 모르겠음
    public static GameObject T01;
    public static GameObject T06;
    public static GameObject T26;
    public static GameObject T11;

    public static void Position(List<Dictionary<string, object>> Ground)
    {
        Debug.Log(Ground[0]["easting"]);
        Debug.Log(Ground[0]["height"]);
        Debug.Log(Ground[0]["northing"]);

        // Set control point objects in position
        T01.transform.position = new Vector3(
            Convert.ToSingle(Ground[0]["easting"]),
            Convert.ToSingle(Ground[0]["height"]),
            Convert.ToSingle(Ground[0]["northing"]));
        T06.transform.position = new Vector3(
            Convert.ToSingle(Ground[5]["easting"]),
            Convert.ToSingle(Ground[5]["height"]),
            Convert.ToSingle(Ground[5]["northing"]));
        T26.transform.position = new Vector3(
            Convert.ToSingle(Ground[25]["easting"]),
            Convert.ToSingle(Ground[25]["height"]),
            Convert.ToSingle(Ground[25]["northing"]));
        T11.transform.position = new Vector3(
            Convert.ToSingle(Ground[10]["easting"]),
            Convert.ToSingle(Ground[10]["height"]),
            Convert.ToSingle(Ground[10]["northing"]));

        // Set target object in position
        TargetOBJ.transform.position = new Vector3(
            Convert.ToSingle(Ground[0]["easting"]),
            Convert.ToSingle(Ground[0]["height"]),
            Convert.ToSingle(Ground[0]["northing"]));
        TargetOBJ.transform.LookAt(new Vector3(
            Convert.ToSingle(Ground[10]["easting"]),
            Convert.ToSingle(Ground[10]["height"]),
            Convert.ToSingle(Ground[10]["northing"])));
    }
}