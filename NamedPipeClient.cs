using System.Diagnostics;
using System.IO.Pipes;
using System.Management;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace NamedPipeHandler
{

    public class NamedPipeClient
    {
        // パイプに対して送信を行う処理
        // 1件送信するごとに、パイプ接続→切断するタイプ。
        public static async Task CreateAndSendAsync(string pipeName, DataBlock writeDataBlock, Action<string>? setStatus, Action<DataBlock>? onRecvResponse = default)
        {


            await Task.Run(async () =>
            {
                using (NamedPipeClientStream? pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation))
                {
                    int try_num = 0;
                    while (true)
                    {

                        try
                        {
                            pipeClient?.Connect(1000);
                            
                        }
                        catch (TimeoutException toe)
                        {
                            if (try_num < 10)
                            {
                                try_num++;
                                continue;
                            }
                            else
                            {
                                setStatus?.Invoke(toe.Message);
                                // 接続時にタイムアウトした場合はなにもしない
                                break;
                            }
                        }

                        if (pipeClient is null || !pipeClient.IsConnected)
                        {
                            throw new InvalidOperationException("PipeClientがnullもしくは未接続です");
                        }
                        else
                        {
                            try
                            {
                                using (var reader = new BinaryReader(pipeClient))
                                using (var writer = new BinaryWriter(pipeClient))
                                {
                                    using var cts = new CancellationTokenSource();
                                    cts.CancelAfter(TimeSpan.FromSeconds(5));

                                    // サーバーにメッセージを送信
                                    byte[] messageBytes = DataBlockHandler.ConvertDataBlockToBytes(writeDataBlock);
                                    //await pipeClient!.WriteAsync(messageBytes, 0, messageBytes.Length, cts.Token);
                                    writer.Write(messageBytes, 0, messageBytes.Length);
                                    writer.Flush();

                                    // サーバーからの応答を受け取る
                                    byte[] buffer = new byte[512];
                                    DataBlock receivedDataBlock = new DataBlock();
                                    int length = reader.Read(buffer, 0, buffer.Length);
                                    if (length > 0) 
                                    {
                                        setStatus?.Invoke("Server Responded:");
                                        receivedDataBlock = DataBlockHandler.ConvertBytesToDataBlock(buffer);
                                        onRecvResponse?.Invoke(receivedDataBlock);
                                    }
                                    else
                                    {
                                        setStatus?.Invoke("Server Not Responded:");
                                    }
                                    //int bytesRead = await pipeClient.ReadAsync(buffer, 0, buffer.Length);
                                    //DataBlock receivedDataBlock = DataBlockHandler.ConvertBytesToDataBlock(buffer);
                                }

                                break;
                            }
                            catch (TimeoutException te)
                            {
                                setStatus?.Invoke(te.Message);
                            }
                            catch (OperationCanceledException oce)
                            {
                                setStatus?.Invoke(oce.Message);
                            }
                            catch (IOException ioe)
                            {
                                setStatus?.Invoke(ioe.Message);
                            }
                        }
                    }
                }

            });
            
        }

        public static string FindNamedPipe(string pipeName)
        {
            try
            {
                string query = "SELECT * FROM Win32_NamedPipe";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"].ToString();
                    if (name != null && name.Contains(pipeName))
                    {
                        //MessageBox.Show($"Found named pipe: {name}", "Named Pipe Finder");
                        return name;
                    }
                }
                //MessageBox.Show("Named pipe not found.", "Named Pipe Finder");
            }
            catch (Exception ex) 
            {
                //MessageBox.Show($"Error while searching for named pipe: {ex.Message}", "Named Pipe Finder");
            }

            return string.Empty;
        }
    }
}