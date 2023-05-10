using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Align : MonoBehaviour
{
    public Transform target;
    // Start is called before the first frame update
    void Start()
    {
        // position의 경우 Vector3에 따라 움직이고,
        // Vector3는 int 타입의 데이터만 사용 가능하고 double은 사용 불가함
        transform.position = new Vector3(0, 0, 0); 
        transform.LookAt(target.position);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
