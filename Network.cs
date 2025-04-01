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
        public int port = 12345;
        public IPAddress host = IPAddress.Any;

        UdpClient udpServer;
        IPEndPoint myIPEP;
        IPEndPoint targetIPEP;
        Thread receiveThread;
        TaskQueue _sendQueue = new TaskQueue();

        private bool isMsgReceive;
        private bool isRunning = true;

        public UDPServer()
        {
            myIPEP = new IPEndPoint(host, 0);
            udpServer = new UdpClient(port); // 포트 12345로 바인딩
        }


        public void StartServer()
        {
            Console.WriteLine("UDP 서버 실행 중...");
            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        void ReceiveData()
        {
            while (isRunning)
            {
                try
                {
                    if (udpServer.Available > 0)
                    {
                        udpServer.BeginReceive(new AsyncCallback(ReceiveCallback), null);
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
            //try
            //{
            //    while (true)
            //    {
            //        byte[] receivedData = udpServer.Receive(ref remoteEP);
            //        string receivedMessage = Encoding.UTF8.GetString(receivedData);
            //        Console.WriteLine($"클라이언트({remoteEP.Address}): {receivedMessage}");

            //        // 응답 메시지 전송
            //        byte[] responseData = Encoding.UTF8.GetBytes("Hello from WPF!");
            //        udpServer.Send(responseData, responseData.Length, remoteEP);
            //    }
            //}

        }

        void ReceiveCallback(IAsyncResult ar)
        {
            byte[] receivedData = udpServer.EndReceive(ar, ref targetIPEP);
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
                    await udpServer.SendAsync(data, data.Length, targetIPEP));
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
            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread?.Join();
            udpServer?.Close();
            _sendQueue?.Stop();
            Console.WriteLine("Server Closing");
        }
    }
}