using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace NamedPipeHandler
{
    public interface INamedPipeServer
    {
        Task ServerReceiveAsync(string pipeName, Action<string> onRecv, Action<string> setStatus, CancellationToken ct = default);
        Task SendString(string sendData);
    }
}
