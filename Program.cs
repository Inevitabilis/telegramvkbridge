using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Npgsql;
using VkNet;
using telegramvkbridge;
using VkNet.Model;
using VkNet.Enums.Filters;

#region Initstuff
string botToken = System.IO.File.ReadAllText("E:\\prog\\TgBotToken.txt");
string sqlConnectionParams = System.IO.File.ReadAllText("E:\\prog\\sqlconnection.txt");
var datasource = NpgsqlDataSource.Create(sqlConnectionParams);

var botClient = new TelegramBotClient(botToken);
using CancellationTokenSource cts = new();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>() // receive all update types except ChatMember related updates
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Connection startup");

DatabaseCheck();

Console.ReadLine();
#endregion


#region shutdown
// Send cancellation request to stop bot
cts.Cancel();
#endregion


async void DatabaseCheck()
{
    await using (var cmd = datasource.CreateCommand("SELECT token FROM demodata WHERE dbid = 1"))
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
            Console.WriteLine(reader.GetString(0));
    }
} 



async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Message is not { } message)
        return;
    // Only process text messages
    if (message.Text is not { } messageText)
        return;

    var chatId = message.Chat.Id;
    StaticStuff.UserState userState = SqlOperations.GetUserState(chatId, datasource);

    switch(userState)
    {
        case StaticStuff.UserState.NoAuth:
        
            if (messageText == "/start") await botClient.SendTextMessageAsync(chatId,
                "This bot mirrors your messages to the chat you specified from your account\n" +
                "\n" +
                "Your credentials would be required to sign in\n" +
                "\n" +
                "We do not store your credentials, only limited access tokens\n" +
                "\n" +
                "Execute /login to start the procedure",
                cancellationToken: cancellationToken);
            else if (messageText == "/login")
            {
                await botClient.SendTextMessageAsync(chatId,
                "Please enter your login and, within the same message, on the next line, password", //This is a thing because I promised I won't store credentials, and I don't
                cancellationToken: cancellationToken);
                SqlOperations.SetUserState(chatId, StaticStuff.UserState.EnteringLogin, datasource);
            }
            break;
        case StaticStuff.UserState.EnteringLogin:            
            string[] response = messageText.Split('\n');
            if(response.Length == 2) //Only two lines are required. Everything else would be assumed as user trying to do bad stuff
            {
                try
                {
                    var api = new VkApi();
                    api.Authorize(new ApiAuthParams
                    {
                        ApplicationId = 51697198,
                        Login = response[0],
                        Password = response[1],
                        Settings = Settings.Offline | Settings.Messages
                    });
                    SqlOperations.SetUserToken(chatId, api.Token, datasource);
                    SqlOperations.SetUserState(chatId, StaticStuff.UserState.Connected, datasource);
                }
                catch(Exception e)  //Theoretically there were different exceptions according to docs, but no, couldn't find auth exception in the list
                {
                    Console.WriteLine(e);
                    await botClient.SendTextMessageAsync(chatId,
                    "Unknown error occured while attempting to request token",
                    cancellationToken: cancellationToken);
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId,
                    "The form of credentials was invalid. Only two strings are required",
                    cancellationToken: cancellationToken);
            }
            break;
            

    

    }


    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

    // Echo received message text
    Message sentMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: "You said:\n" + messageText,
        cancellationToken: cancellationToken);
    
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}