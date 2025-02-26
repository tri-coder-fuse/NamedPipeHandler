using System.IO.Pipes;

namespace NamedPipeHandler
{
    public class NamedPipeServer : IDisposable, INamedPipeServer
    {
        // 送信、受信にそれぞれ名前付きパイプを作成
        // （NamedPipeServerStreamにStreamWriterを生成して利用すると、パイプが壊れるため @Window11 24H2）
        // 同じパイプに対しての接続は1件まで
        private static readonly int RecvPipeThreadMax = 10;
        private CancellationTokenSource _lifetimeCts = new CancellationTokenSource();
        private bool disposedValue;
        private NamedPipeServerStream? _serverStream;
        private bool isRunning = true;

        public bool IsRunning()
        {
            return isRunning;
        }

        // パイプから受信を行う処理
        public Task ServerReceiveAsync(string pipeName, Action<DataBlock> onRecv, Action<string> setStatus, CancellationToken ct = default)
        {
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeCts.Token);

            return Task.Run(async () =>
            {
                while (isRunning)
                {
                    try
                    {
                        using( _serverStream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, RecvPipeThreadMax, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
                        {
                            // クライアントの接続待ち
                            setStatus.Invoke($"未受信：クライアントの接続待ち開始");
                            await _serverStream.WaitForConnectionAsync(combinedCts.Token);

                            // 受信処理
                            var _ = HandleClient(_serverStream, onRecv, setStatus);
                        }
                    }
                    catch (IOException ofex)
                    {
                        // クライアントが切断
                        setStatus.Invoke($"未受信：クライアント側が切断しました：{ofex.Message}");
                        isRunning = false;
                    }
                    catch (OperationCanceledException oce)
                    {
                        // パイプサーバーのキャンセル要求(OperationCanceledExceptionをthrowしてTaskが終わると、Taskは「Cancel」扱いになる)
                        setStatus.Invoke($"未受信：パイプサーバーのキャンセル要求がきました。{oce.GetType()}");
                        isRunning = false;
                        throw;
                    }
                    finally
                    {
                        setStatus.Invoke("未受信：パイプ終了");

                        if (_serverStream != null )
                        {
                            _serverStream.Dispose();
                        }

                        await Task.Delay(1); // 1ms待機

                    }
                }
            });
        }


        // 送信処理（主にクライアント受信時の応答の送信を想定）
        //public async Task SendString(string sendData)
        //{
        //    CancellationTokenSource cts = new CancellationTokenSource();
        //    cts.CancelAfter(TimeSpan.FromSeconds(5));
        //    // クライアントに応答を送信
        //    byte[] responseBytes = Encoding.UTF8.GetBytes(sendData);
        //    await _serverStream!.WriteAsync(responseBytes, 0, responseBytes.Length, cts.Token);
        //}

        // 送信処理（主にクライアント受信時の応答の送信を想定）
        public async Task SendDatablock(DataBlock sendData)
        {
            using (var writer = new BinaryWriter(_serverStream))
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                // クライアントに応答を送信
                byte[] responseBytes = DataBlockHandler.ConvertDataBlockToBytes(sendData);
                writer.Write(responseBytes, 0, responseBytes.Length);
                writer.Flush();
            }

        }

        private async Task HandleClient(NamedPipeServerStream client_connection, Action<DataBlock> onRecv, Action<string> setStatus)
        {
            try
            {
                while (isRunning && client_connection != null && client_connection.IsConnected)
                {
                    try
                    {
                        using (var reader = new BinaryReader(client_connection))
                        {
                            byte[] buffer = new byte[512];
                            int bytesRead = reader.Read(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                setStatus.Invoke($"受信：");

                                DataBlock db = DataBlockHandler.ConvertBytesToDataBlock(buffer);
                                onRecv?.Invoke(db);
                            }
                            else
                            {
                                throw new IOException("Connection lost");
                            }
                        }

                    }
                    catch (IOException ex)
                    {
                        break;                        
                        // 接続が切れた場合、ループを抜ける
                    }
                }
            }
            finally
            {
                client_connection?.Dispose();
                // クライアントの接続を閉じる
            }
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                    _lifetimeCts.Cancel();
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~PipeConnect()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}