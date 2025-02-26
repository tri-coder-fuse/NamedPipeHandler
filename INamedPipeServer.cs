
namespace NamedPipeHandler
{
    public interface INamedPipeServer
    {
        Task ServerReceiveAsync(string pipeName, Action<DataBlock> onRecv, Action<string> setStatus, CancellationToken ct = default);
        Task SendDatablock(DataBlock sendData);
        bool IsRunning();
    }
}
