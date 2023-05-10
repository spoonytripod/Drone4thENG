using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class MakeCSV : MonoBehaviour
{
    public static void Save(List<double> data)
    {
        FileStream fs = new FileStream("Assets/result.csv", FileMode.Append, FileAccess.Write);
        StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.Unicode);

        // NOTATION : Image ID, Ground ID, Hit x, Hit y, Hit z, Ground x, Ground y, Ground z, DroneUTM (N), DroneUTM (E)
        // Unity z to Ground y, Unity y to Ground z
        sw.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}",
            data[0], data[1], data[2], data[4], data[3], data[5], data[7], data[6], data[8], data[9]);
        sw.Close();
    }
}
