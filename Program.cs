using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Npgsql;
using SharpVk;


#region Initstuff
string botToken = System.IO.File.ReadAllText("E:\\prog\\TgBotToken.txt");


var botClient = new TelegramBotClient(botToken);
using CancellationTokenSource cts = new();


// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Connection startup");

Update();

Console.ReadLine();
#endregion


#region shutdown
// Send cancellation request to stop bot
cts.Cancel();
#endregion


async void Update()
{
    string sqlConnectionParams = System.IO.File.ReadAllText("E:\\prog\\sqlconnection.txt");
    await using var conn = new NpgsqlConnection(sqlConnectionParams);
    await conn.OpenAsync();
    await using (var cmd = new NpgsqlCommand("SELECT token FROM demodata WHERE dbid = 2", conn))
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