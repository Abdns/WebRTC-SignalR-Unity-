using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

public class SignalRConnection : IDisposable
{
    public delegate void RecieveOfferData(string messageSide, string offerData);
    public event RecieveOfferData ReciveOfferDataEvent;

    public delegate void RecieveICE(string messageSide, string candidate, int sdpMlineIndex, string sdpMid);
    public event RecieveICE RecieveICEvent;

    private HubConnection _chatHubConnection = null;
    private bool disposed = false;
    private int _timeToFirstReconnection, _timeToFollowingReconnection = 5;

    public async void SendOfferData(string messageSide, string offerData)
    {
        await _chatHubConnection.InvokeAsync("SendOfferData", messageSide, offerData);
    }

    public async void SendICECandidateData(string messageSide, string candidate, int sdpMlineIndex, string sdpMid)
    {
        await _chatHubConnection.InvokeAsync("SendICECandidate", messageSide, candidate, sdpMlineIndex, sdpMid);
    }

    public async Task StartConnectAsync()
    {
        _chatHubConnection = new HubConnectionBuilder()
           .WithUrl("https://localhost:7280/chat").WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(_timeToFirstReconnection), TimeSpan.FromSeconds(_timeToFollowingReconnection) })
           .Build();

       await _chatHubConnection.StartAsync();

       RegisterReceiveChatMetods();
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void RegisterReceiveChatMetods()
    {
        _chatHubConnection.On<string, string>("ReceiveOfferData", (messageSide, offerData) =>
        {
            ReciveOfferDataEvent.Invoke(messageSide, offerData);
        });

        _chatHubConnection.On<string, string, int, string>("ReceiveICECandidate", (messageSide, candidate, sdpMlineIndex, sdpMid) =>
        {
            RecieveICEvent.Invoke(messageSide, candidate, sdpMlineIndex, sdpMid);
        });

    }
    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;
        if (disposing)
        {
            _chatHubConnection.StopAsync();
            _chatHubConnection.DisposeAsync();
        }
        disposed = true;
    }

    ~SignalRConnection()
    {
        Dispose(false);
    }
}
