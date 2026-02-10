using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<ChatHub>("/chatHub");
app.Run();

public class ChatHub : Hub
{
    // מילון שזוכר מי מחובר כרגע (נמחק בניתוק)
    private static ConcurrentDictionary<string, string> CodeToConnectionId = new();
    private static ConcurrentDictionary<string, string> ConnectionIdToCode = new();
    
    // מילון שזוכר הערות (נמחק בניתוק כרגע)
    private static ConcurrentDictionary<string, List<string>> UserNotes = new();

    // *** החדש: מילון שזוכר שריון מספרים (לא נמחק בניתוק!) ***
    // מפתח: ID, ערך: יום הולדת/סיסמה
    private static ConcurrentDictionary<string, string> ReservedUsers = new();

    public override async Task OnConnectedAsync()
    {
        string userCode = GenerateUniqueCode();
        CodeToConnectionId.TryAdd(userCode, Context.ConnectionId);
        ConnectionIdToCode.TryAdd(Context.ConnectionId, userCode);
        UserNotes.TryAdd(userCode, new List<string>());

        await Clients.Caller.SendAsync("ReceiveMyCode", userCode);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionIdToCode.TryRemove(Context.ConnectionId, out string code))
        {
            CodeToConnectionId.TryRemove(code, out _);
            // אם המשתמש לא שריין את המספר, נמחק את ההערות שלו
            if (!ReservedUsers.ContainsKey(code))
            {
                UserNotes.TryRemove(code, out _);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    // פונקציית שריון (SAVE)
    public async Task ProcessSave(string birthday)
    {
        if (ConnectionIdToCode.TryGetValue(Context.ConnectionId, out string? myCode))
        {
            if (ReservedUsers.ContainsKey(myCode))
            {
                await Clients.Caller.SendAsync("ErrorMessage", "ID already secured");
                return;
            }

            ReservedUsers.TryAdd(myCode, birthday);
            await Clients.Caller.SendAsync("SuccessMessage", "ID Secured with birthday!");
        }
    }

    // פונקציית כניסה למספר קיים (LOGIN)
    public async Task ProcessLogin(string targetCode, string birthday)
    {
        if (ReservedUsers.TryGetValue(targetCode, out string? savedBirthday) && savedBirthday == birthday)
        {
            // החלפת ה-ID הנוכחי של המשתמש ל-ID הישן שלו
            if (ConnectionIdToCode.TryRemove(Context.ConnectionId, out string oldTempCode))
            {
                CodeToConnectionId.TryRemove(oldTempCode, out _);
            }

            CodeToConnectionId[targetCode] = Context.ConnectionId;
            ConnectionIdToCode[Context.ConnectionId] = targetCode;

            await Clients.Caller.SendAsync("ReceiveMyCode", targetCode);
            await Clients.Caller.SendAsync("SuccessMessage", "Logged in to your ID!");
        }
        else
        {
            await Clients.Caller.SendAsync("ErrorMessage", "Invalid ID or Birthday");
        }
    }

    // שאר הפונקציות שלך נשארות אותו דבר...
    public async Task CheckUserExists(string targetCode)
    {
        if (CodeToConnectionId.ContainsKey(targetCode))
        {
            await Clients.Caller.SendAsync("UserFound", targetCode);
            if (UserNotes.TryGetValue(targetCode, out List<string>? notes))
                await Clients.Caller.SendAsync("ReceiveUserNotes", targetCode, notes);
        }
        else
        {
            await Clients.Caller.SendAsync("ErrorMessage", "User not available");
        }
    }

    public async Task SendMessageToUser(string targetCode, string message)
    {
        if (CodeToConnectionId.TryGetValue(targetCode, out string? targetConnId))
        {
            string myCode = ConnectionIdToCode[Context.ConnectionId];
            await Clients.Client(targetConnId).SendAsync("ReceiveMessage", myCode, message);
        }
    }

    public async Task UpdateMyNotes(string newNoteContent)
    {
        if (ConnectionIdToCode.TryGetValue(Context.ConnectionId, out string? myCode))
        {
            if (!UserNotes.ContainsKey(myCode)) UserNotes[myCode] = new List<string>();
            UserNotes[myCode].Insert(0, newNoteContent);
            await Clients.Caller.SendAsync("ReceiveUserNotes", myCode, UserNotes[myCode]);
        }
    }

    public async Task GetUserNotes(string targetCode)
    {
        if (UserNotes.TryGetValue(targetCode, out List<string>? notes))
            await Clients.Caller.SendAsync("ReceiveUserNotes", targetCode, notes);
    }

    private string GenerateUniqueCode()
    {
        Random rnd = new Random();
        string code;
        do
        {
            int length = rnd.Next(4, 7);
            char[] digits = new char[length];
            for (int i = 0; i < length; i++) digits[i] = (char)('0' + rnd.Next(0, 6));
            code = new string(digits);
        } while (CodeToConnectionId.ContainsKey(code) || ReservedUsers.ContainsKey(code));
        return code;
    }
}
