using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Finger
{
    public class MYAES
    {
        public static void AesEncrypt(string filepath, string key)
        {
            //读出文件流字节数组
            byte[] data;
            FileStream fs = new FileStream(filepath, FileMode.OpenOrCreate);
            long len = fs.Seek(0, SeekOrigin.End);
            fs.Seek(0, SeekOrigin.Begin);
            data = new byte[len];
            fs.Read(data, 0, (int)len);
            //fs.Flush();

            //对读出的data进行加密
            MD5 d5 = MD5.Create();
            d5.ComputeHash(Encoding.UTF8.GetBytes(key));
            Aes aesAlg = Aes.Create();
            aesAlg.IV = d5.Hash;
            aesAlg.Padding = PaddingMode.Zeros;
            aesAlg.Key = Encoding.UTF8.GetBytes(BitConverter.ToString(d5.Hash).Replace("-", ""));
            ICryptoTransform encryptor = aesAlg.CreateEncryptor();

            //加密完成保存
            byte[] enBuffer = encryptor.TransformFinalBlock(data, 0, data.Length);
            fs.Seek(0, SeekOrigin.Begin);

            byte[] test = BitConverter.GetBytes((int)len);
            fs.Write(test, 0, 4);
            fs.Write(enBuffer, 0, enBuffer.Length);
            
            //fs.Flush();
            fs.Close();
        }
        
        public static void AesDecrypt(string filepath, string key)
        {
            //读出文件流字节数组
            byte[] data;
            FileStream fs = new FileStream(filepath, FileMode.OpenOrCreate);
            long len = fs.Seek(0, SeekOrigin.End);
            fs.Seek(0, SeekOrigin.Begin);
            data = new byte[len-4];
            
            byte[] test = new byte[4];
            fs.Read(test, 0, 4);
            int reallen = BitConverter.ToInt32(test,0);

            fs.Read(data, 0, (int)len-4);
            //fs.Flush();

            //对读出的data进行解密
            MD5 d5 = MD5.Create();
            d5.ComputeHash(Encoding.UTF8.GetBytes(key));
            Aes aesAlg = Aes.Create();
            aesAlg.IV = d5.Hash;
            aesAlg.Padding = PaddingMode.Zeros;
            aesAlg.Key = Encoding.UTF8.GetBytes(BitConverter.ToString(d5.Hash).Replace("-", ""));
            ICryptoTransform decryptor = aesAlg.CreateDecryptor();

            //解密完成保存
            byte[] deBuffer = decryptor.TransformFinalBlock(data, 0, data.Length);
            fs.Seek(0, SeekOrigin.Begin);
            fs.Write(deBuffer, 0, reallen);
            //fs.Flush();
            fs.Close();
        }
    }
}
