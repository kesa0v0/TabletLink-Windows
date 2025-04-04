using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace TabletLink_WindowsApp
{
    public class TaskQueue
    {
        private readonly BlockingCollection<Action> _taskQueue = new();
        private readonly Thread _workerThread;

        public TaskQueue()
        {
            _workerThread = new Thread(ProcessQueue)
            {
                IsBackground = true
            };
            _workerThread.Start();
        }

        public void Enqueue(Action task) => _taskQueue.Add(task);

        private void ProcessQueue()
        {
            foreach (var task in _taskQueue.GetConsumingEnumerable())
            {
                try
                {
                    task();
                }
                catch (Exception ex)
                {
                    // 예외 처리 로직
                    Console.WriteLine($"Task execution error: {ex.Message}");
                }
            }
        }

        public void Stop() => _taskQueue.CompleteAdding();
    }

    class UDPServer
    {
        public int sendPort = 12346;
        public int receivePort = 12345;
        public IPAddress host = IPAddress.Any;

        UdpClient udpSend;
        UdpClient udpReceive;
        IPEndPoint clientEndPoint;
        Thread receiveThread;
        TaskQueue _sendQueue = new();

        private bool isRunning = true;

        public UDPServer()
        {
            udpReceive = new UdpClient(receivePort);
            udpSend = new UdpClient();
        }


        public void StartServer()
        {
            Console.WriteLine("UDP 서버 실행 중...");

            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }


        private string GetLocalIPAddress()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }

        void ReceiveData()
        {
            while (isRunning)
            {
                try
                {
                    if (udpReceive.Available > 0)
                    {
                        var data = udpReceive.Receive(ref clientEndPoint);
                        string msg = Encoding.UTF8.GetString(data);
                        Console.WriteLine($"From client {clientEndPoint}: {msg}");

                        if (msg == "DISCOVER_TABLET_SERVER")
                        {
                            byte[] ack = Encoding.UTF8.GetBytes($"TABLET_SERVER_ACK:{GetLocalIPAddress()}");
                            udpSend.Send(ack, ack.Length, new IPEndPoint(clientEndPoint.Address, sendPort));
                        }
                    }   
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        //void ReceiveCallback(IAsyncResult ar)
        //{
        //    byte[] receivedData = udpReceive.EndReceive(ar, ref cl);
        //    string msg = Encoding.UTF8.GetString(receivedData);

        //    if (msg == "CONNECT")
        //    {
        //        Console.WriteLine($"연결 요청 수신: {targetIPEP}");
        //        // 이 이후로 이 targetIPEP로 화면 전송 가능
        //    }
        //    else
        //    {
        //        // 펜 입력 처리
        //        var x = BitConverter.ToSingle(receivedData, 0);
        //        var y = BitConverter.ToSingle(receivedData, 4);
        //        var pressure = BitConverter.ToSingle(receivedData, 8);
        //        var timestamp = BitConverter.ToInt64(receivedData, 12);

        //        Console.WriteLine($"펜 입력: x={x}, y={y}, pressure={pressure}, latency={DateTimeOffset.Now.ToUnixTimeMilliseconds() - timestamp}ms");
        //    }
        //}

        public void SendData(byte[] data)
        {
            if (clientEndPoint == null) return;

            _sendQueue.Enqueue(() =>
            {
                try
                {
                    Console.WriteLine($"Data to {clientEndPoint}: {data.Length} bytes");
                    udpSend.Send(data, data.Length, new IPEndPoint(clientEndPoint.Address, sendPort));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            });
        }

        ~UDPServer()
        {
            CloseServer();
        }


        public void CloseServer()
        {
            if (!isRunning)
                return;
            isRunning = false;
            udpSend?.Close();
            udpReceive?.Close();
            _sendQueue?.Stop();
            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread?.Join();
            Console.WriteLine("Server Closing");
        }
    }
}