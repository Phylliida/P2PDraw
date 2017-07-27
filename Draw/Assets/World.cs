
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityP2P;

public class World : MonoBehaviour {
    public Peer player;
    public Marker marker;

    HashSet<string> players;

    void Start()
    {
        player.OnConnection += Player_OnConnection;
        player.OnDisconnection += Player_OnDisconnection;
        player.OnGetID += Player_OnGetID;
        player.OnTextFromPeer += Player_OnMessageFromPeer;
        players = new HashSet<string>();
    }

    public void SendSegment(string segmentId, float[] segment, string specificUser=null)
    {
        byte[] idBytes = ASCIIEncoding.ASCII.GetBytes(segmentId);
        byte[] resBytes = new byte[1 + idBytes.Length + segment.Length * sizeof(float)];
        resBytes[0] = (byte)'a';
        Buffer.BlockCopy(idBytes, 0, resBytes, 1, 36);
        Buffer.BlockCopy(segment, 0, resBytes, 37, segment.Length * sizeof(float));
        SendBytes(resBytes, specificUser);
    }

    public void RemoveSegment(string segmentId, string specificUser=null)
    {
        byte[] resBytes = new byte[1 + 36];
        byte[] idBytes = ASCIIEncoding.ASCII.GetBytes(segmentId);
        Buffer.BlockCopy(idBytes, 0, resBytes, 1, 36);
        resBytes[0] = (byte)'r';
        SendBytes(resBytes, specificUser);
    }

    private void Player_OnMessageFromPeer(string id, string message)
    {
        try
        {
            byte[] receivedBytes = System.Convert.FromBase64String(message);
            bool isRemoving = receivedBytes[0] == 'r';
            if (receivedBytes.Length < 37+8)
            {
                return;
            }
            string segmentId = System.Convert.ToBase64String(receivedBytes, 1, 36);
            if (isRemoving)
            {
                marker.RemovePublicSegment(segmentId);
            }
            else
            {
                float[] data = new float[(receivedBytes.Length - 37)/sizeof(float)];
                Buffer.BlockCopy(receivedBytes, 37, data, 0, data.Length * sizeof(float));
                marker.AddSegment(data, segmentId, false);
            }
        }
        catch (Exception e)
        {
            Debug.Log("failed to parse message: " + e);
        }
    }

    // Modified https://stackoverflow.com/a/11743162/2924421
    public static string Base64Encode(float[] values)
    {
        byte[] bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return System.Convert.ToBase64String(bytes);
    }

    public static float[] Base64Decode(string base64EncodedData)
    {
        byte[] bytes = System.Convert.FromBase64String(base64EncodedData);
        if (bytes.Length < sizeof(float) || bytes.Length % sizeof(float) != 0)
        {
            return new float[0];
        }
        float[] res = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, res, 0, bytes.Length);
        return res;
    }

    public void SendBytes(byte[] arr, string specificUser=null)
    {
        string message = System.Convert.ToBase64String(arr);
        if (specificUser == null)
        {
            foreach (string id in players)
            {
                player.Send(id, message);
                //player.Send(id, "hello");
            }
        }
        else
        {
            if (players.Contains(specificUser))
            {
                //player.Send(specificUser, "hello");
                player.Send(specificUser, message);
            }
        }
    }
    public void SendFloats(float[] arr)
    {
        string message = Base64Encode(arr);
        foreach (string id in players)
        {
            player.Send(id, message);
        }
    }

    void Player_OnGetID(string id)
    {
        
    }

    void Player_OnDisconnection(string id)
    {
        Debug.Log("disconnected from: " + id);
        if (!players.Contains(id))
        {
            players.Remove(id);
        }
    }

    void Player_OnConnection(string id)
    {
        Debug.Log("connected to: " + id);
        if (!players.Contains(id))
        {
            players.Add(id);
        }
        foreach (KeyValuePair<string, float[]> segment in marker.mySegments)
        {
            SendSegment(segment.Key, segment.Value, id);
        }
    }
}
