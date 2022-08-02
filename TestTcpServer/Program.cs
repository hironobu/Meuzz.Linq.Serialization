using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using Meuzz.Linq.Serialization;
using System.Diagnostics;

namespace ServerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.IPv6Any, 9999);

            server.Start();

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();

                NetworkStream ns = client.GetStream();

                byte[] hello = new byte[100];
                hello = Encoding.Default.GetBytes("hello world");

                ns.Write(hello, 0, hello.Length);

                while (client.Connected)
                {
                    byte[] msg = new byte[1024 * 64];
                    ns.Read(msg, 0, msg.Length);

                    var s = Encoding.UTF8.GetString(msg);

                    var ff = JsonNetSerializer.Deserialize<Func<string, bool>>(s);

                    Debug.WriteLine(ff("hogehoge"));
                    Debug.WriteLine(s);
                }
            }

        }
    }
}