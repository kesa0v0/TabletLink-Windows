using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;

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
        private bool isConnected = false;

        public enum PacketType : byte
        {
            DiscoverTabletServer = 0x01,
            PenInput = 0x02
        }

        public struct PenData
        {
            public float X;
            public float Y;
            public float pressure;
            public long timestamp;
        }

        public class ReceivedData
        {
            public PacketType Type { get; set; }
            public PenData? Data { get; set; }
        }

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

        public static ReceivedData ParsePacket(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                var typeByte = reader.ReadByte();
                var type = (PacketType)typeByte;

                PenData? data = null;
                if (stream.Position < stream.Length)
                {
                    int _X = reader.ReadInt32();
                    int _Y = reader.ReadInt32();
                    int _pressure = reader.ReadInt32();
                    byte[] longBytes = reader.ReadBytes(8);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(longBytes);
                    data = new PenData
                    {
                        X = _X,
                        Y = _Y,
                        pressure = _pressure,
                        timestamp = BitConverter.ToInt64(longBytes, 0)

                    };
                }

                return new ReceivedData
                {
                    Type = type,
                    Data = data
                };
            }
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
                        var packet = ParsePacket(data);
                        Console.WriteLine($"From client {clientEndPoint}: {packet.Type}");

                        if (packet.Type == PacketType.DiscoverTabletServer)
                        {
                            byte[] ack = Encoding.UTF8.GetBytes($"TABLET_SERVER_ACK:{GetLocalIPAddress()}:{receivePort}");
                            udpSend.Send(ack, ack.Length, new IPEndPoint(clientEndPoint.Address, clientEndPoint.Port));
                            isConnected = true;
                        }
                        else if (packet.Type == PacketType.PenInput && packet.Data != null)
                        {
                            var penData = packet.Data.Value;
                            Console.WriteLine($"stamp:{penData.timestamp} now:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
                            Console.WriteLine($"펜 입력: x={penData.X}, y={penData.Y}, pressure={penData.pressure}, latency={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - penData.timestamp}");
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

        public void SendData(byte[] data)
        {
            if (clientEndPoint == null) return;
            if (!isConnected) return;

            _sendQueue.Enqueue(() =>
            {
                try
                {
                    Console.WriteLine($"{RandomNumberGenerator.GetInt32(12321)}: Data to {clientEndPoint}: {data.Length} bytes");
                    udpSend.Send(data, data.Length, clientEndPoint);
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
            isConnected = false;
            udpSend?.Close();
            udpReceive?.Close();
            _sendQueue?.Stop();
            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread?.Join();
            Console.WriteLine("Server Closing");
        }
    }
}