using System;
using System.Collections;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

public class WebRTCReciever : MonoBehaviour
{
    [SerializeField] private RawImage _receiveImage;

    private SignalRConnection _signalingConnection;
    private RTCPeerConnection _peerConnection;
    private MediaStream _receiveStream;
    private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();

    private string _sdpData = null;
    private int _messageCooldown = 8000;

    private async void Start()
    {
        Application.runInBackground = true;

        _signalingConnection = new SignalRConnection();
        await _signalingConnection.StartConnectAsync();

        _signalingConnection.ReciveOfferDataEvent += ReactOnRecieveButtonCommand;
        _signalingConnection.RecieveICEvent += RecieveICECandidate;

        _receiveStream = new MediaStream(); 

        StartCoroutine(WebRTC.Update());
        StartCoroutine(SetupPeerConnection());
    }

    IEnumerator SendICECandidate(RTCIceCandidate candidate)
    {
        CancellationToken token = _cancelTokenSource.Token;
        var task = System.Threading.Tasks.Task.Run(() =>
        {
            while (_peerConnection.IceConnectionState != RTCIceConnectionState.Connected)
            {
                _signalingConnection.SendICECandidateData("IceReciever", candidate.Candidate, (int)candidate.SdpMLineIndex, candidate.SdpMid);
                Thread.Sleep(_messageCooldown);

                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
            }
        }, token);

        yield return new WaitUntil(() => task.IsCompleted);
    }


    IEnumerator SetupPeerConnection()
    {
        RTCConfiguration configuration = GetSelectedSdpSemantics();
        _peerConnection = new RTCPeerConnection(ref configuration);

        RTCRtpTransceiverInit transceiverInit = new RTCRtpTransceiverInit();
        transceiverInit.direction = RTCRtpTransceiverDirection.RecvOnly;

        _peerConnection.AddTransceiver(TrackKind.Video, transceiverInit);

        _peerConnection.OnIceCandidate = candidate =>
        {
            StartCoroutine(SendICECandidate(candidate));      
            Debug.Log($"WebRTC: OnIceCandidate {candidate.ToString()}");
        };

        _peerConnection.OnTrack = e =>
        {
            _receiveStream.AddTrack(e.Track);
        };

        _receiveStream.OnAddTrack = e =>
        {
            if (e.Track is VideoStreamTrack videoTrack)
            {
                videoTrack.OnVideoReceived += tex =>
                {
                    Debug.Log($"WebRTC: OnVideoReceived {videoTrack.ToString()}, tex={tex.width}x{tex.height}");
                    _receiveImage.texture = tex;

                    int width = tex.width < 1280 ? tex.width : 1280;
                    int height = tex.width > 0 ? width * tex.height / tex.width : 720;
                    _receiveImage.rectTransform.sizeDelta = new Vector2(width, height);
                };
            }
        };
      

        yield return StartCoroutine(PeerNegotiationNeeded());
    }

    private static RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } };

        return config;
    }

    IEnumerator PeerNegotiationNeeded()
    {
        RTCSessionDescriptionAsyncOperation sessionDescription = _peerConnection.CreateOffer();
        yield return sessionDescription;

        if (sessionDescription.IsError)
            yield break;

        yield return StartCoroutine(OnCreateOfferSuccess(sessionDescription.Desc));
    }

    IEnumerator OnCreateOfferSuccess(RTCSessionDescription offer)
    {
        RTCSetSessionDescriptionAsyncOperation sessionDescription = _peerConnection.SetLocalDescription(ref offer);
        _peerConnection.CreateAnswer();

        yield return sessionDescription;
        yield return StartCoroutine(ExchangeSDP(offer.sdp));
    }

    IEnumerator ExchangeSDP(string offer)
    {
        CancellationToken token = _cancelTokenSource.Token;
        offer = offer.Replace("a=setup:actpass", "a=setup:passive");

        var task = System.Threading.Tasks.Task<string>.Run(() =>
        {
            while (_sdpData == null)
            {
                _signalingConnection.SendOfferData("reciver", (offer).ToString());
                Thread.Sleep(_messageCooldown);

                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
            }
            return _sdpData;
        }, token);

        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            yield break;
        }
       
        StartCoroutine(OnGotAnswerSuccess(task.Result));
    }

    IEnumerator OnGotAnswerSuccess(string answer)
    {
        RTCSessionDescription desc = new RTCSessionDescription();
        desc.type = RTCSdpType.Answer;
        desc.sdp = answer;
        RTCSetSessionDescriptionAsyncOperation sessionDescription = _peerConnection.SetRemoteDescription(ref desc);
        yield return sessionDescription;

        Debug.Log($"WebRTC: Answer done={sessionDescription.IsDone}, hasError={sessionDescription.IsError}");

        yield break;
    }

    public void ReactOnRecieveButtonCommand(string messageSide, string data)
    {
       if(messageSide == "streamer")
       {
            _sdpData = data;
       }     
    }

    public void RecieveICECandidate(string name, string candidate, int sdpMlineIndex, string sdpMid)
    {
        if (name == "IceStreamer" && (_peerConnection.IceConnectionState != RTCIceConnectionState.Completed))
        {
            RTCIceCandidateInit iceInit = new RTCIceCandidateInit()
            {
                candidate = candidate,
                sdpMid = sdpMid,
                sdpMLineIndex = sdpMlineIndex
            };
            RTCIceCandidate newIce = new RTCIceCandidate(iceInit);
            _peerConnection.AddIceCandidate(newIce);
        }
    }

    private void OnDestroy()
    {
        _cancelTokenSource.Cancel();

        _peerConnection?.Close();
        _peerConnection?.Dispose();
        _peerConnection = null;

        _signalingConnection.ReciveOfferDataEvent -= ReactOnRecieveButtonCommand;

        _signalingConnection.Dispose();
    }

}
