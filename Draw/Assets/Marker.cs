using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using System.IO;

public class Marker : MonoBehaviour
{
    public World world;
    public float[] curSegment;
    public bool[] isConnectedToPrevious;
    public int curSegmentLength = 0;
    public int initialCapacity = 100;
    public object modifyLock = new object();

    public Transform leftHand;
    public Transform hand;

    string headerPath = "header.txt";
    string dataPath = "data.txt";

    public Transform markerHolder;
    Dictionary<string, float[]> segments;
    public Dictionary<string, float[]> mySegments;

    // Use this for initialization
    void Start()
    {
        curSegmentLength = 0;
        curSegment = new float[initialCapacity*3];
        segments = new Dictionary<string, float[]>();
        mySegments = new Dictionary<string, float[]>();
        CreateLineMaterial();
        LoadData(headerPath, dataPath);
        CreateLineMaterial();
    }

    // From http://docs.unity3d.com/ScriptReference/GL.html

    static Material lineMaterial;
    static void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            // Unity has a built-in shader that is useful for drawing
            // simple colored things.
            var shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            // Turn on alpha blending
            //lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            //lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // Turn backface culling off
            //lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            // Turn off depth writes
            //lineMaterial.SetInt("_ZWrite", 0);
        }
    }

    float timeBetween = 0.01f;
    float curPos = 0;
    public float moveSpeed = 0.5f;
    public Vector3 offsetFromFace;

    float curTimeWaiting = 0.0f;

    public bool goingForward = false;

    public Transform dummyTransform;

    public bool lost = false;

    public bool didTurn = false;

    // Update is called once per frame
    void Update()
    {
        if (!UnityEngine.VR.VRSettings.enabled)
        {
            return;
        }


        Vector3 rightPos = Vector3.zero;
        Vector3 leftPos = Vector3.zero;

        bool lostLeftHand = UnityEngine.VR.InputTracking.GetLocalPosition(UnityEngine.VR.VRNode.LeftHand) == Vector3.zero;
        bool lostRightHand = UnityEngine.VR.InputTracking.GetLocalPosition(UnityEngine.VR.VRNode.RightHand) == Vector3.zero;

        if (!lostLeftHand)
        {
            leftHand.transform.parent = markerHolder.transform;
            leftHand.transform.localPosition = UnityEngine.VR.InputTracking.GetLocalPosition(UnityEngine.VR.VRNode.LeftHand);
            leftPos = leftHand.transform.position;
            leftHand.transform.GetComponent<Renderer>().enabled = true;
        }
        else
        {
            leftHand.transform.GetComponent<Renderer>().enabled = false;
        }

        if (!lostRightHand)
        {
            hand.transform.parent = markerHolder.transform;
            hand.transform.localPosition = UnityEngine.VR.InputTracking.GetLocalPosition(UnityEngine.VR.VRNode.RightHand);
            rightPos = hand.transform.position;
            hand.transform.GetComponent<Renderer>().enabled = true;
        }
        else
        {
            hand.transform.GetComponent<Renderer>().enabled = false;
        }

        
        if (Input.GetAxis("Rotate") > 0.9f)
        {
            if (!didTurn)
            {
                didTurn = true;
                markerHolder.transform.RotateAround(Camera.main.transform.position, Vector3.up, 50.0f);
            }
        }
        else if (Input.GetAxis("Rotate") < -0.9f)
        {
            if (!didTurn)
            {
                didTurn = true;
                markerHolder.transform.RotateAround(Camera.main.transform.position, Vector3.up, -50.0f);
            }
        }
        else
        {
            didTurn = false;

        }

        if (UnityEngine.VR.VRSettings.loadedDeviceName == "Oculus")
        {
            if (Input.GetKeyDown(KeyCode.JoystickButton1) && !lostRightHand)
            {
                ClearList(hand.transform.position);
            }
            if (Input.GetKeyDown(KeyCode.JoystickButton0) && !lostRightHand)
            {
                AddToList(rightPos, false);
            }
            if (Input.GetKey(KeyCode.JoystickButton0) && !lostRightHand)
            {
                if (rightPos != Vector3.zero)
                {
                    AddToList(rightPos, true);
                }
            }

            if (Input.GetKeyUp(KeyCode.JoystickButton0) && !lostRightHand)
            {
                EndCurrentSegment();
            }



            if (Input.GetKeyDown(KeyCode.JoystickButton2) && !lostLeftHand)
            {
                dummyTransform.parent = markerHolder.transform;
                dummyTransform.localRotation = UnityEngine.VR.InputTracking.GetLocalRotation(UnityEngine.VR.VRNode.LeftHand);
                markerHolder.transform.position += dummyTransform.forward / 2.0f;
            }


        }

        else
        {
            if (Input.GetKeyDown(KeyCode.JoystickButton14) && !lostRightHand)
            {
                ClearList(hand.transform.position);
            }
            if (Input.GetKeyDown(KeyCode.JoystickButton15) && !lostRightHand)
            {
                AddToList(rightPos, false);
            }
            if (Input.GetKey(KeyCode.JoystickButton15) && !lostRightHand)
            {
                if (rightPos != Vector3.zero)
                {
                    AddToList(rightPos, true);
                }
            }

            if (Input.GetKeyUp(KeyCode.JoystickButton15) && !lostRightHand)
            {
                EndCurrentSegment();
            }



            if (Input.GetKeyDown(KeyCode.JoystickButton8) && !lostLeftHand)
            {
                dummyTransform.parent = markerHolder.transform;
                dummyTransform.localRotation = UnityEngine.VR.InputTracking.GetLocalRotation(UnityEngine.VR.VRNode.LeftHand);
                markerHolder.transform.position += dummyTransform.forward / 2.0f;
            }

        }

    }

    bool modified = true;

    public void RemovePublicSegment(string segmentId)
    {
        if (segments.ContainsKey(segmentId) && !mySegments.ContainsKey(segmentId))
        {
            segments.Remove(segmentId);
            modified = true;
        }
    }

    void ClearList(Vector3 position)
    {
        float posX = position.x;
        float posY = position.y;
        float posZ = position.z;

        string closestSegment = "";
        float closestDist = float.MaxValue;
        foreach (KeyValuePair<string, float[]> segment in mySegments)
        {
            float[] curSegment = segment.Value;
            for (int j = 0; j < curSegment.Length; j += 3)
            {
                float dx = posX - curSegment[j];
                float dy = posY - curSegment[j + 1];
                float dz = posZ - curSegment[j + 2];
                // We don't need to square root since we are only comparing to other distances that also aren't square rooted
                float dist = dx * dx + dy * dy + dz * dz;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestSegment = segment.Key;
                }
            }
        }

        if (mySegments.Count > 0 && closestSegment != "" && closestDist < 0.01)
        {
            modified = true;
            segments.Remove(closestSegment);
            mySegments.Remove(closestSegment);
            world.RemoveSegment(closestSegment);
        }
    }

    public void EndCurrentSegment()
    {
        if (curSegmentLength != 0)
        {
            float[] resSegment = new float[curSegmentLength * 3];
            Buffer.BlockCopy(curSegment, 0, resSegment, 0, sizeof(float) * curSegmentLength * 3);
            AddSegment(resSegment, "", true);
        }
        curSegment = new float[initialCapacity * 3];
        curSegmentLength = 0;
    }

    public void AddSegment(float[] segment, string segmentId, bool fromMe)
    {
        if (segmentId == "")
        {
            modified = true;
            segmentId = Guid.NewGuid().ToString();

            if (fromMe)
            {
                mySegments[segmentId.Trim()] = segment;
            }
            segments[segmentId.Trim()] = segment;
        }
        else if (!segments.ContainsKey(segmentId.Trim()))
        {
            modified = true;
            if (fromMe)
            {
                mySegments[segmentId.Trim()] = segment;
            }
            segments[segmentId.Trim()] = segment;
        }
        if (fromMe)
        {
            modified = true;
            SaveData(headerPath, dataPath);
            world.SendSegment(segmentId, segment);
        }
    }

    void SaveData(string headerPath, string dataPath)
    {
        if (mySegments.Count == 0)
        {
            return;
        }

        int numBytes = 0;
        foreach (KeyValuePair<string, float[]> curSegment in mySegments)
        {
            numBytes += curSegment.Value.Length * sizeof(float);
        }
        byte[] resBytes = new byte[numBytes];
        int curOffset = 0;
        string[] lines = new string[mySegments.Count];
        int i = 0;
        foreach (KeyValuePair<string, float[]> curSegment in mySegments)
        {
            Buffer.BlockCopy(curSegment.Value, 0, resBytes, curOffset, curSegment.Value.Length * sizeof(float));
            lines[i] = curSegment.Key + " " + curSegment.Value.Length + " " + curOffset;
            curOffset += curSegment.Value.Length * sizeof(float);
            i++;
        }
        File.WriteAllLines(headerPath, lines);
        File.WriteAllBytes(dataPath, resBytes);
    }

    void LoadData(string headerPath, string dataPath)
    {
        if (!File.Exists(headerPath) || !File.Exists(dataPath))
        {
            Debug.Log("saved data doesn't exist with header path: " + headerPath + " and data path: " + dataPath);
            return;
        }

        byte[] data = File.ReadAllBytes(dataPath);
        string[] headers = File.ReadAllLines(headerPath);

        for (int i = 0; i < headers.Length; i++)
        {
            string curHeader = headers[i].Trim();
            if (curHeader == "")
            {
                continue;
            }

            string[] pieces = curHeader.Split(new char[] { ' ' });
            if (pieces.Length != 3)
            {
                continue;
            }
            string segmentId = pieces[0].Trim();
            int segmentLen = int.Parse(pieces[1].Trim());
            int offset = int.Parse(pieces[2].Trim());
            float[] segmentData = new float[segmentLen];
            Buffer.BlockCopy(data, offset, segmentData, 0, segmentData.Length * sizeof(float));
            mySegments[segmentId] = segmentData;
            segments[segmentId] = segmentData;
            modified = true;
        }
    }

    void AddToList(Vector3 newPoint, bool connectedToPrevious)
    {
        if (curSegmentLength == 0)
        {
            modified = true;
        }
        if (!connectedToPrevious)
        {
            EndCurrentSegment();
        }
        if (curSegmentLength >= curSegment.Length/3-1)
        {
            float[] newSegment = new float[curSegmentLength*3*2];
            Buffer.BlockCopy(curSegment, 0, newSegment, 0, sizeof(float) * curSegmentLength * 3);
            curSegment = newSegment;
        }


        curSegment[curSegmentLength * 3] = newPoint.x;
        curSegment[curSegmentLength * 3 + 1] = newPoint.y;
        curSegment[curSegmentLength * 3 + 2] = newPoint.z;
        curSegmentLength++;
    }

    private void OnApplicationQuit()
    {

        if (segmentDatasBuffer != null)
        {
            segmentDatasBuffer.Dispose();
        }
        if (notConnectedToPrevBuffer != null)
        {
            notConnectedToPrevBuffer.Dispose();
        }
    }

    void OnRenderObject()
    {
        drawThings();
    }

    public float pointSize = 1.0f;
    


    float[] segmentDatas;
    int[] notConnectedToPrev;

    ComputeBuffer segmentDatasBuffer;
    ComputeBuffer notConnectedToPrevBuffer;


    public Material lineShaderMaterial;

    public void drawThings()
    {
        if (segments == null)
        {
            return;
        }
        if (modified && segments.Count > 0)
        {
            int allSegmentsLen = 0;
            int maxLen = 0;
            foreach (KeyValuePair<string, float[]> segment in segments)
            {
                allSegmentsLen += segment.Value.Length;
                maxLen = Math.Max(segment.Value.Length, maxLen);
            }
            segmentDatas = new float[allSegmentsLen];
            notConnectedToPrev = new int[allSegmentsLen / 3]; // We have 3 floats per point

            if (allSegmentsLen > 6)
            {

                int curOffset = 0;
                int connectedToPrevOffset = 0;
                foreach (KeyValuePair<string, float[]> segment in segments)
                {
                    Buffer.BlockCopy(segment.Value, 0, segmentDatas, curOffset, sizeof(float) * segment.Value.Length);
                    notConnectedToPrev[connectedToPrevOffset] = 1;
                    connectedToPrevOffset += segment.Value.Length / 3;
                    curOffset += segment.Value.Length * sizeof(float);
                }

                if (segmentDatasBuffer != null)
                {
                    segmentDatasBuffer.Dispose();
                }
                if (notConnectedToPrevBuffer != null)
                {
                    notConnectedToPrevBuffer.Dispose();
                }

                segmentDatasBuffer = new ComputeBuffer(allSegmentsLen / 3, sizeof(float) * 3);
                notConnectedToPrevBuffer = new ComputeBuffer(allSegmentsLen / 3, sizeof(int));

                segmentDatasBuffer.SetData(segmentDatas);
                notConnectedToPrevBuffer.SetData(notConnectedToPrev);

                lineShaderMaterial.SetBuffer("segmentDatas", segmentDatasBuffer);
                lineShaderMaterial.SetBuffer("notConnectedToPrev", notConnectedToPrevBuffer);

                modified = false;
            }
        }

        if (segments.Count > 0 && segmentDatasBuffer != null && notConnectedToPrevBuffer != null)
        {
            lineShaderMaterial.SetPass(0);
            Graphics.DrawProcedural(MeshTopology.Lines, notConnectedToPrev.Length * 2 - 1, 0);
        }


        // Apply the line material
        lineMaterial.SetPass(0);

        GL.PushMatrix();

        if (curSegmentLength > 1)
        {
            GL.Begin(GL.LINES);
            GL.Color(new Color(1.0f, 1.0f, 1.0f));

            GL.Vertex3(curSegment[0], curSegment[1], curSegment[2]);
            int i = 3;
            for (; i < (curSegmentLength - 1) * 3; i += 3)
            {
                GL.Vertex3(curSegment[i], curSegment[i + 1], curSegment[i + 2]);
                GL.Vertex3(curSegment[i], curSegment[i + 1], curSegment[i + 2]);
            }
            if (curSegment.Length < i + 2)
            {
                GL.Vertex3(curSegment[i], curSegment[i + 1], curSegment[i + 2]);
            }

            GL.End();
        }

        GL.PopMatrix();
    }


}
