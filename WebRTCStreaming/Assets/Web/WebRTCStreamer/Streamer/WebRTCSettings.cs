using Unity.WebRTC;
using UnityEngine;

internal static class WebRTCSettings
{
    public const int DefaultStreamWidth = 1280;
    public const int DefaultStreamHeight = 720;

    private static Vector2Int _StreamSize = new Vector2Int(DefaultStreamWidth, DefaultStreamHeight);
    private static RTCRtpCodecCapability _useVideoCodec = null;

    public static Vector2Int StreamSize
    {
        get { return _StreamSize; }
        set { _StreamSize = value; }
    }

    public static RTCRtpCodecCapability UseVideoCodec
    {
        get { return _useVideoCodec; }
        set { _useVideoCodec = value; }
    }
}
