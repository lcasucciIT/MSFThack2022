using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

namespace BlazorSignalRchat.Server.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            try
            {
                string model_id = "unitary/toxic-bert";
                string api_token = "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";

                string requestUrl = "https://api-inference.huggingface.co/models/" + model_id;

                HttpClient myClient = new HttpClient();

                HttpRequestMessage requestMessage = new HttpRequestMessage();
                requestMessage.Content = JsonContent.Create(message);            
                requestMessage.Headers.TryAddWithoutValidation("Authorization: Bearer ", api_token);

                HttpResponseMessage responseMessage = await myClient.PostAsync(requestUrl, requestMessage.Content);
                HttpContent content = responseMessage.Content;
                string result = await content.ReadAsStringAsync();

                await Clients.All.SendAsync("ReceiveMessage", user, message, result);
            }
            catch (HttpRequestException exception)
            {
                Console.WriteLine("An HTTP request exception occurred. {0}", exception.Message);
            }
        }
    }
}