using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TabletLink_WindowsApp
{
    /// <summary>
    /// Receives pen data and device info from an Android device over UDP.
    /// </summary>
    public class UdpPenReceiver
    {
        private UdpClient _udpClient;
        private CancellationTokenSource _cancellationTokenSource;

        // Event for when device info (screen size) is received.
        public event Action<int, int> OnDeviceInfoReceived;
        // Event for when pen data is received.
        public event Action<UdpPenData> OnPenDataReceived;

        // Stores the endpoint of the Android device to send responses back.
        private IPEndPoint _androidEndpoint;

        // Packet type constants (must match the Android app).
        private const byte PACKET_TYPE_PEN_DATA_MIN = 0;
        private const byte PACKET_TYPE_PEN_DATA_MAX = 3;
        private const byte PACKET_TYPE_DEVICE_INFO_REQ = 255;
        private const byte PACKET_TYPE_DEVICE_INFO_ACK = 254;
        private const byte PACKET_TYPE_HEARTBEAT_PING = 253;
        private const byte PACKET_TYPE_HEARTBEAT_PONG = 252;

        public void StartListening(int port = 9999)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested) return;

            _udpClient = new UdpClient(port);
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            Task.Run(() => ListenLoop(token), token);
        }

        public void StopListening()
        {
            _cancellationTokenSource?.Cancel();
            _udpClient?.Close();
        }

        private void ListenLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // This allows receiving data from any device.
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] receivedBytes = _udpClient.Receive(ref remoteEP);

                    if (_androidEndpoint == null || !_androidEndpoint.Equals(remoteEP))
                    {
                        _androidEndpoint = remoteEP;
                    }

                    ProcessPacket(receivedBytes);
                }
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted || token.IsCancellationRequested)
            {
                // This exception is expected when StopListening is called, so we can ignore it.
            }
            catch (Exception e)
            {
                Console.WriteLine($"Listening Error: {e.Message}");
            }
        }

        private void ProcessPacket(byte[] data)
        {
            if (data.Length == 0) return;

            byte packetType = data[0];

            // Pen Data Packet (17 bytes)
            if (data.Length == 17 && packetType >= PACKET_TYPE_PEN_DATA_MIN && packetType <= PACKET_TYPE_PEN_DATA_MAX)
            {
                ProcessPenDataPacket(data);
            }
            // Device Info Request Packet (13 bytes)
            else if (data.Length == 13 && packetType == PACKET_TYPE_DEVICE_INFO_REQ)
            {
                ProcessDeviceInfoPacket(data);
                SendControlPacket(PACKET_TYPE_DEVICE_INFO_ACK);
            }
            // Heartbeat Ping Packet (1 byte)
            else if (data.Length == 1 && packetType == PACKET_TYPE_HEARTBEAT_PING)
            {
                SendControlPacket(PACKET_TYPE_HEARTBEAT_PONG);
            }
        }

        private void ProcessPenDataPacket(byte[] data)
        {
            /*
             * Pen Data Packet Structure (17 bytes, Little Endian)
             * --------------------------------------------------
             * Offset | Length | Description
             * --------------------------------------------------
             * 0      | 1      | Action (lower 4 bits), IsBarrelPressed (5th bit)
             * 1      | 4      | X (float)
             * 5      | 4      | Y (float)
             * 9      | 4      | Pressure (float)
             * 13     | 2      | TiltX (short)
             * 15     | 2      | TiltY (short)
             */
            byte actionAndFlags = data[0];
            OnPenDataReceived?.Invoke(new UdpPenData
            {
                Action = (byte)(actionAndFlags & 0x0F), // Lower 4 bits for action
                IsBarrelPressed = (actionAndFlags & (1 << 4)) != 0, // 5th bit for barrel button
                X = BitConverter.ToSingle(data, 1),
                Y = BitConverter.ToSingle(data, 5),
                Pressure = BitConverter.ToSingle(data, 9),
                TiltX = BitConverter.ToInt16(data, 13),
                TiltY = BitConverter.ToInt16(data, 15)
            });
        }

        private void ProcessDeviceInfoPacket(byte[] data)
        {
            int width = BitConverter.ToInt32(data, 1);
            int height = BitConverter.ToInt32(data, 5);
            OnDeviceInfoReceived?.Invoke(width, height);
        }

        private void SendControlPacket(byte packetType)
        {
            if (_androidEndpoint == null) return;

            byte[] packet = { packetType };
            _udpClient.Send(packet, packet.Length, _androidEndpoint);
        }
    }

    /// <summary>
    /// A struct to hold parsed pen data from a UDP packet.
    /// Using a struct avoids heap allocation for each packet.
    /// </summary>
    public struct UdpPenData
    {
        public byte Action { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Pressure { get; set; }
        public bool IsBarrelPressed { get; set; }
        public short TiltX { get; set; }
        public short TiltY { get; set; }
    }
}
