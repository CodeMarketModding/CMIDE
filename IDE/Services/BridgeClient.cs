using System.IO;
using System.Text.Json;
using CodeMarket.IDE.Models;
namespace CodeMarket.IDE.Services;
public sealed class BridgeClient
{
    private readonly string folder = Environment.GetEnvironmentVariable("CODEMARKET_IDE_BRIDGE_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodeMarketLiveIDE", "bridge");
    private readonly SemaphoreSlim gate = new(1,1);
    public string? Session { get; private set; }
    public int World { get; private set; }
    public string? Language { get; private set; }
    public bool Connected => Session is not null;
    public void Disconnect() { Session=null; World=0; Language=null; }
    public async Task<BridgeResponse> ConnectAsync()
    {
        var response = await SendAsync(new { command="list" });
        Ensure(response); Session=response.Session; World=response.World; Language=response.Language;
        if (string.IsNullOrEmpty(Session)) throw new IOException("The bridge did not provide a session ID.");
        return response;
    }
    public Task<BridgeResponse> ReadAsync(string path) => CheckedAsync(new { command="read",session=Session,world=World,path });
    public Task<BridgeResponse> WriteAsync(string path,string text,string expected) => CheckedAsync(new { command="write",session=Session,world=World,path,text,expected });
    public Task<BridgeResponse> CompleteAsync(string path,string text,int caret,bool forced) => CheckedAsync(new { command="complete",session=Session,world=World,path,text,caret,forced });
    public Task<BridgeResponse> CreateAsync(string folder,string name) => CheckedAsync(new { command="create",session=Session,world=World,path=folder,name });
    public Task<BridgeResponse> CreateFolderAsync(string folder,string name) => CheckedAsync(new { command="createFolder",session=Session,world=World,path=folder,name });
    public Task<BridgeResponse> ReferenceIndexAsync() => CheckedAsync(new { command="referenceIndex",session=Session,world=World });
    public Task<BridgeResponse> ReferencePageAsync(string id) => CheckedAsync(new { command="referencePage",session=Session,world=World,path=id });
    public Task<BridgeResponse> RenameAsync(string path,string name,string expected) => CheckedAsync(new { command="rename",session=Session,world=World,path,name,expected });
    public Task<BridgeResponse> DeleteAsync(string path,string expected) => CheckedAsync(new { command="delete",session=Session,world=World,path,expected });
    private async Task<BridgeResponse> CheckedAsync(object request) { var r=await SendAsync(request);Ensure(r);return r; }
    private static void Ensure(BridgeResponse r) { if (!r.Ok) throw new IOException(r.Error ?? "The game rejected the request."); }
    private async Task<BridgeResponse> SendAsync(object request)
    {
        await gate.WaitAsync();
        string? input=null, output=null, temp=null;
        try
        {
            Directory.CreateDirectory(folder);
            string id=Guid.NewGuid().ToString("N");input=Path.Combine(folder,id+".request");output=Path.Combine(folder,id+".response");temp=Path.Combine(folder,id+".tmp");
            await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(request));File.Move(temp,input);temp=null;
            using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(6));
            try { while (!File.Exists(output)) await Task.Delay(40,cts.Token); }
            catch (OperationCanceledException) { throw new IOException("The game bridge did not respond. Open a world and check that the mod is loaded."); }
            return JsonSerializer.Deserialize<BridgeResponse>(await File.ReadAllTextAsync(output)) ?? throw new IOException("Empty bridge response.");
        }
        finally
        {
            foreach(var p in new[]{input,output,temp}) if(p is not null) try { File.Delete(p); } catch(IOException) { } catch(UnauthorizedAccessException) { }
            gate.Release();
        }
    }
}
