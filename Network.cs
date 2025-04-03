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
        private readonly BlockingCollection<Action> _taskQueue = new BlockingCollection<Action>();
        private readonly Thread _workerThread;

        public TaskQueue()
        {
            _workerThread = new Thread(ProcessQueue)
            {
                IsBackground = true
            };
            _workerThread.Start();
        }

        public void Enqueue(Action task)
        {
            _taskQueue.Add(task);
        }

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

        public void Stop()
        {
            _taskQueue.CompleteAdding();
        }
    }

    class UDPServer
    {
        public int sendPort = 12346;
        public int receivePort = 12345;
        public IPAddress host = IPAddress.Any;

        UdpClient udpSend;
        UdpClient udpReceive;
        IPEndPoint myIPEP;
        IPEndPoint targetIPEP;
        Thread receiveThread;
        TaskQueue _sendQueue = new TaskQueue();

        private bool isMsgReceive;
        private bool isRunning = true;

        public UDPServer()
        {
            myIPEP = new IPEndPoint(host, 0);
            udpSend = new UdpClient();
            //udpReceive = new UdpClient(receivePort);
            targetIPEP = new IPEndPoint(IPAddress.Parse("10.0.2.16"), sendPort); 
        }


        public void StartServer()
        {
            Console.WriteLine("UDP 서버 실행 중...");
            //receiveThread = new Thread(ReceiveData);
            //receiveThread.IsBackground = true;
            //receiveThread.Start();
        }

        void ReceiveData()
        {
            while (isRunning)
            {
                try
                {
                    if (udpReceive.Available > 0)
                    {
                        udpReceive.BeginReceive(new AsyncCallback(ReceiveCallback), null);
                        isMsgReceive = false;
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

        void ReceiveCallback(IAsyncResult ar)
        {
            byte[] receivedData = udpReceive.EndReceive(ar, ref targetIPEP);
            string receivedMessage = Encoding.UTF8.GetString(receivedData);
            Console.WriteLine($"클라이언트({targetIPEP?.Address}): {receivedMessage}");
            isMsgReceive = true;
            //evtRecieveData?.Invoke(receivedData);
        }

        public void SendData(byte[] data)
        {
            _sendQueue.Enqueue(() =>
            {
                try
                {
                    Task.Run(async () =>
                    await udpSend.SendAsync(data, data.Length, targetIPEP));
                    Console.WriteLine($"Send Data to {targetIPEP.Address}");
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