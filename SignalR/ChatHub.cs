using Microsoft.AspNetCore.SignalR;

namespace SignalRApp
{
    public class ChatHub : Hub
    {
        public async Task SendOfferData(string messageSide, string command)
        {
            await this.Clients.All.SendAsync("ReceiveOfferData", messageSide, command);
        }
        public async Task SendICECandidate(string messageSide, string candidate, int sdpMlineIndex, string sdpMid)
        {
            await this.Clients.All.SendAsync("ReceiveICECandidate", messageSide, candidate, sdpMlineIndex, sdpMid);
        }
    }
}
