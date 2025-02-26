using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NamedPipeHandler
{
    /// <summary>
    /// アプリの送受信データ形式
    /// </summary>
    public struct DataBlock
    {
        public int type;
        public byte[]? data;
    }

    public class DataBlockHandler
    {


        public static DataBlock ConvertBytesToDataBlock(byte[] data)
        {
            DataBlock db;
            // 始めの４バイトはtype
            db.type = BitConverter.ToInt32(data, 0);
            // 次の４バイトはデータの長さ
            int datalength = BitConverter.ToInt32(data, 4);
            // 残りのバイトはデータ
            db.data = new byte[data.Length - 8];
            Array.Copy(data, 8, db.data, 0, datalength);
            return db;
        }

        public static DataBlock ConvertBytesToDataBlock(byte[] data, int dataLength)
        {
            DataBlock db;
            db.type = BitConverter.ToInt32(data, 0);
            db.data = new byte[dataLength];
            Array.Copy(data, 8, db.data, 0, dataLength);
            return db;
        }

        public static byte[] ConvertDataBlockToBytes(DataBlock db)
        {
            byte[] data = new byte[db.data!.Length + 8];
            Array.Copy(BitConverter.GetBytes(db.type), 0, data, 0, 4);
            Array.Copy(BitConverter.GetBytes(db.data.Length), 0, data, 4, 4);
            Array.Copy(db.data, 0, data, 8, db.data.Length);
            return data;
        }

    }
}
