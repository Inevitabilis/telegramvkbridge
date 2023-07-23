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
    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}."); //Debug purposes. I really need to remove it before any sensitive data leaks...
    StaticStuff.UserState userState = SqlOperations.GetUserState(chatId, datasource);
    var api = new VkApi();

    switch (userState)//state machine yay!
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
                SqlOperations.SetUserState(chatId, StaticStuff.UserState.EnteringCredentials, datasource);
            }
            break;
        case StaticStuff.UserState.EnteringCredentials:            
            string[] response = messageText.Split('\n');
            if(response.Length == 2) //Only two lines are required. Everything else would be assumed as user trying to do bad stuff
            {
                try
                {
                    api.Authorize(new ApiAuthParams
                    {
                        ApplicationId = 51697198,
                        Login = response[0],
                        Password = response[1],
                        Settings = Settings.Offline | Settings.Messages
                    });
                    SqlOperations.SetUserToken(chatId, api.Token, datasource);
                    SqlOperations.SetUserState(chatId, StaticStuff.UserState.ChatChoice, datasource);
                    var convos = api.Messages.GetConversations(new GetConversationsParams()
                    {
                        Offset = 0,
                        Count = 10,
                        Extended = false,
                    });

                    string LastConvos = "";
                    foreach (var conversationAndLastMessage in convos.Items)
                    {
                        LastConvos += conversationAndLastMessage.Conversation.ChatSettings.Title + "\n";
                    }
                    await botClient.SendTextMessageAsync(chatId,
                            LastConvos +
                            "Here are the last ten chats I got. Type a part of the group name (not necessarily within the list) to connect to the desired chat",
                            cancellationToken: cancellationToken);
                }
                catch(Exception e)  //Theoretically there were different exceptions according to docs, but no, couldn't find auth exception in the list
                {
                    Console.WriteLine(e);
                    await botClient.SendTextMessageAsync(chatId,
                    "Unknown error occured while attempting to request token",
                    cancellationToken: cancellationToken);
                }
                finally
                {
                    api.Dispose();
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId,
                    "The form of credentials was invalid. Only two strings are required",
                    cancellationToken: cancellationToken);
            }
            break;
        case StaticStuff.UserState.ChatChoice:
            api.Authorize(new ApiAuthParams
            {
                AccessToken = SqlOperations.GetUserToken(chatId, datasource)
            });
            var convos2 = api.Messages.GetConversations(new GetConversationsParams()
            {
                Offset = 0,
                Count = 100,
                Extended = false,
            });
            ConversationAndLastMessage desiredConversation = null;
            foreach (var conversationAndLastMessage in convos2.Items)
            {
                if(conversationAndLastMessage.Conversation.ChatSettings.Title.Contains(messageText))
                {
                    await botClient.SendTextMessageAsync(chatId,
                    "Connected to the chat \"" + conversationAndLastMessage.Conversation.ChatSettings.Title + "\"\nID = " + conversationAndLastMessage.Conversation.Peer.Id,
                    cancellationToken: cancellationToken);
                    desiredConversation = conversationAndLastMessage;
                    break;
                }
            }
            if(desiredConversation == null) await botClient.SendTextMessageAsync(chatId,
                    "Couldn't find specified chat",
                    cancellationToken: cancellationToken);
            else
            {
                SqlOperations.SetUserChatID(chatId, desiredConversation.Conversation.Peer.Id, datasource);
                SqlOperations.SetUserState(chatId, StaticStuff.UserState.Connected, datasource);
            }
            api.Dispose();
            break;
        case StaticStuff.UserState.Connected:
            api.Authorize(new ApiAuthParams
            {
                AccessToken = SqlOperations.GetUserToken(chatId, datasource)
            });
            api.Messages.Send(new MessagesSendParams()
            {
                UserId = api.UserId,
                RandomId = 0,
                PeerId = SqlOperations.GetUserChatID(chatId, datasource),
                Message = messageText
            });

            break;
    }
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