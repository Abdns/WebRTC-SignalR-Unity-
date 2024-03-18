using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

public class WebRTCStreamer : MonoBehaviour
{
    [SerializeField] private Camera _sourceCamera;
    [SerializeField] private RawImage _sourceImage;

    private SignalRConnection _signalingConnection;
    private RTCPeerConnection _peerConnection;
    private VideoStreamTrack _videoStreamTrack;
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

        StartCoroutine(WebRTC.Update());
        StartCoroutine(SetupPeerConnection());
        AddTracks();
    }
    private IEnumerator SendICECandidate(RTCIceCandidate candidate)
    {
        CancellationToken token = _cancelTokenSource.Token;
        var task = System.Threading.Tasks.Task.Run(() =>
        {
            while (_peerConnection.IceConnectionState != RTCIceConnectionState.Connected)
            {
                _signalingConnection.SendICECandidateData("IceStreamer", candidate.Candidate, (int)candidate.SdpMLineIndex, candidate.SdpMid);
                Thread.Sleep(_messageCooldown);

                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
            }
        }, token);

        yield return new WaitUntil(() => task.IsCompleted);
    }

    private IEnumerator SetupPeerConnection()
    {
        RTCConfiguration configuration = GetSelectedSdpSemantics();
        _peerConnection = new RTCPeerConnection(ref configuration);

        RTCRtpTransceiverInit transceiverInit = new RTCRtpTransceiverInit();
        transceiverInit.direction = RTCRtpTransceiverDirection.SendOnly;

        _peerConnection.AddTransceiver(TrackKind.Video, transceiverInit);

        _peerConnection.OnIceCandidate = candidate =>
        {
            StartCoroutine(SendICECandidate(candidate));
        };

        yield return StartCoroutine(GrabCamera());
    }
    private static RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } };

        return config;
    }
    private IEnumerator GrabCamera()
    {
        _videoStreamTrack = _sourceCamera.CaptureStreamTrack(WebRTCSettings.StreamSize.x, WebRTCSettings.StreamSize.y);
        _sourceImage.texture = _sourceCamera.targetTexture;

        yield return StartCoroutine(PeerNegotiationNeeded());
    }

    private void AddTracks()
    {
        RTCRtpSender videoSender = _peerConnection.AddTrack(_videoStreamTrack);

        if (WebRTCSettings.UseVideoCodec != null)
        {
            RTCRtpCodecCapability[] codecs = new[] { WebRTCSettings.UseVideoCodec };
            RTCRtpTransceiver transceiver = _peerConnection.GetTransceivers().First(t => t.Sender == videoSender);
            transceiver.SetCodecPreferences(codecs);
        }
    }

    private IEnumerator PeerNegotiationNeeded()
    {
        RTCSessionDescriptionAsyncOperation sessionDescription = _peerConnection.CreateOffer();
        yield return sessionDescription;

        if (sessionDescription.IsError)
        {
            Debug.Log("CreateOffer hasError = " + sessionDescription.IsError);
            yield break;
        }

        yield return StartCoroutine(OnCreateOfferSucess(sessionDescription.Desc));
    }

    private IEnumerator OnCreateOfferSucess(RTCSessionDescription offer)
    {
        RTCSetSessionDescriptionAsyncOperation sessionDescription = _peerConnection.SetLocalDescription(ref offer);
        yield return sessionDescription;

        yield return StartCoroutine(ExchangeSDP(offer.sdp));
    }

    private IEnumerator ExchangeSDP(string offer)
    {
        CancellationToken token = _cancelTokenSource.Token;
        offer = offer.Replace("a=setup:actpass", "a=setup:active");

        var task = System.Threading.Tasks.Task<string>.Run(() =>
        {
            while (_sdpData == null)
            {
                _signalingConnection.SendOfferData("streamer", (offer).ToString());
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
            Debug.Log($"WebRTC: Exchange SDP failed, err is {task.Exception.ToString()}");
            yield break;
        }
        StartCoroutine(OnGotAnswerSuccess(task.Result));
    }

    IEnumerator OnGotAnswerSuccess(string answer)
    {
        RTCSessionDescription desc = new RTCSessionDescription();
        desc.type = RTCSdpType.Answer;
        desc.sdp = answer;
        var sessionDescription = _peerConnection.SetRemoteDescription(ref desc);
        yield return sessionDescription;

        Debug.Log($"WebRTC: Answer done={sessionDescription.IsDone}, hasError={sessionDescription.IsError}");
        yield break;
    }

    private void ReactOnRecieveButtonCommand(string messageSide, string command)
    {
        if (messageSide == "reciver")
        {
            _sdpData = command;
        }
    }

    private void RecieveICECandidate(string name, string candidate, int sdpMlineIndex, string sdpMid)
    {
        if (name == "IceReciever" && (_peerConnection.IceConnectionState != RTCIceConnectionState.Completed))
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
        _signalingConnection.RecieveICEvent -= RecieveICECandidate;

        _signalingConnection.Dispose();
    }
}
