using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TabletLink_WindowsApp
{
    /// <summary>
    /// Listens for UDP packets from the Android device, parses them,
    /// and raises events for pen data and device information.
    /// </summary>
    public class UdpPenReceiver
    {
        private UdpClient udpClient;
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Fired when device information (screen size) is received.
        /// Provides width and height.
        /// </summary>
        public event Action<int, int, IPEndPoint> OnDeviceInfoReceived;

        /// <summary>
        /// Fired when pen data is received.
        /// Provides a UdpPenData object with the raw coordinates and state.
        /// </summary>
        public event Action<UdpPenData> OnPenDataReceived;

        /// <summary>
        /// 연결이 끊겼을 때 발생합니다.
        /// </summary>
        public event Action OnConnectionLost;


        private IPEndPoint androidEndpoint;
        private Timer heartbeatTimer;
        private const int HeartbeatTimeout = 5000; // 5초

        // Packet type constants must match the client implementation.
        // Using byte for unsigned values 0-255.
        private const byte PACKET_TYPE_PEN_DATA_MIN = 0;
        private const byte PACKET_TYPE_PEN_DATA_MAX = 3;
        private const byte PACKET_TYPE_DEVICE_INFO_REQ = 255;
        private const byte PACKET_TYPE_DEVICE_INFO_ACK = 254;
        private const byte PACKET_TYPE_HEARTBEAT_PING = 253;
        private const byte PACKET_TYPE_HEARTBEAT_PONG = 252;

        /// <summary>
        /// Starts listening for UDP packets on a background thread.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        public void StartListening(int port = 9999)
        {
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested) return;

            udpClient = new UdpClient(port);
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            heartbeatTimer = new Timer(CheckHeartbeat, null, Timeout.Infinite, Timeout.Infinite);

            Task.Run(() => ListenLoop(token), token);
        }

        /// <summary>
        /// Stops listening and closes the UDP client.
        /// </summary>
        public void StopListening()
        {
            cancellationTokenSource?.Cancel();
            udpClient?.Close();
            heartbeatTimer?.Dispose();
        }

        private void ListenLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    IPEndPoint remoteEP = new(IPAddress.Any, 0);
                    byte[] receivedBytes = udpClient.Receive(ref remoteEP);

                    if (androidEndpoint == null || !androidEndpoint.Equals(remoteEP))
                    {
                        androidEndpoint = remoteEP;
                        Logger.Log($"New Android endpoint established: {androidEndpoint}");
                    }
                    // 하트비트 타이머 리셋
                    heartbeatTimer.Change(HeartbeatTimeout, Timeout.Infinite);

                    ProcessPacket(receivedBytes);
                }
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted || token.IsCancellationRequested)
            {
                // Ignore exceptions caused by stopping the listener.
            }
            catch (Exception e)
            {
                Console.WriteLine($"Listening Error: {e.Message}");
            }
        }

        private void CheckHeartbeat(object state)
        {
            Logger.Log("Connection timed out.");
            androidEndpoint = null;
            OnConnectionLost?.Invoke();
            heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite); // 타이머 비활성화
        }


        private void ProcessPacket(byte[] data)
        {
            if (data.Length == 0) return;

            byte packetHeader = data[0];
            byte actionType = (byte)(packetHeader & 0x0F);

            if (data.Length == 17 && actionType >= PACKET_TYPE_PEN_DATA_MIN && actionType <= PACKET_TYPE_PEN_DATA_MAX)
            {
                ProcessPenDataPacket(data);
            }
            else if (data.Length == 13 && packetHeader == PACKET_TYPE_DEVICE_INFO_REQ)
            {
                ProcessDeviceInfoPacket(data);
                SendControlPacket(PACKET_TYPE_DEVICE_INFO_ACK);
            }
            else if (data.Length == 1 && packetHeader == PACKET_TYPE_HEARTBEAT_PING)
            {
                SendControlPacket(PACKET_TYPE_HEARTBEAT_PONG);
            }
        }

        private void ProcessPenDataPacket(byte[] data)
        {
            byte actionAndFlags = data[0];
            byte action = (byte)(actionAndFlags & 0x0F); // Lower 4 bits for action
            bool isBarrelPressed = (actionAndFlags & (1 << 4)) != 0; // 5th bit for barrel button

            float x = BitConverter.ToSingle(data, 1);
            float y = BitConverter.ToSingle(data, 5);
            float pressure = BitConverter.ToSingle(data, 9);
            short tiltX = BitConverter.ToInt16(data, 13);
            short tiltY = BitConverter.ToInt16(data, 15);

            OnPenDataReceived?.Invoke(new UdpPenData
            {
                Action = action,
                X = x,
                Y = y,
                Pressure = pressure,
                IsBarrelPressed = isBarrelPressed,
                TiltX = tiltX,
                TiltY = tiltY
            });
        }

        private void ProcessDeviceInfoPacket(byte[] data)
        {
            int width = BitConverter.ToInt32(data, 1);
            int height = BitConverter.ToInt32(data, 5);
            OnDeviceInfoReceived?.Invoke(width, height, androidEndpoint);
        }

        private void SendControlPacket(byte packetType)
        {
            if (androidEndpoint == null) return;

            byte[] packet = [packetType];
            udpClient.Send(packet, packet.Length, androidEndpoint);
        }
    }

    /// <summary>
    /// Data structure for received pen events.
    /// </summary>
    public class UdpPenData
    {
        public byte Action { get; set; } // 0:Down, 1:Move, 2:Up, 3:Hover
        public float X { get; set; }
        public float Y { get; set; }
        public float Pressure { get; set; }
        public bool IsBarrelPressed { get; set; }
        public short TiltX { get; set; }
        public short TiltY { get; set; }
    }
}

