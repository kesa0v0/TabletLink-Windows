using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace TabletLink_WindowsApp;

public class UdpPenReceiver
{
    private UdpClient udpClient;
    private CancellationTokenSource cancellationTokenSource;

    // 이벤트: 안드로이드 기기 정보(화면 크기)를 수신했을 때 발생
    public event Action<int, int> OnDeviceInfoReceived;
    // 펜 데이터 수신 이벤트 (원본 좌표 전달)
    public event Action<UdpPenData> OnPenDataReceived;


    // 안드로이드 기기의 주소를 저장하여 응답을 보낼 때 사용
    private IPEndPoint androidEndpoint;

    // 패킷 타입 상수 (Android 코드와 동일하게 유지)
    private const byte PACKET_TYPE_PEN_DATA_MIN = 0;
    private const byte PACKET_TYPE_PEN_DATA_MAX = 3;
    private const byte PACKET_TYPE_DEVICE_INFO_REQ = 255;
    private const byte PACKET_TYPE_DEVICE_INFO_ACK = 254;
    private const byte PACKET_TYPE_HEARTBEAT_PING = 253;
    private const byte PACKET_TYPE_HEARTBEAT_PONG = 252;

    public void StartListening(int port = 9999)
    {
        if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested) return;

        udpClient = new UdpClient(port);
        cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        Task.Run(() => ListenLoop(token), token);
    }

    public void StopListening()
    {
        cancellationTokenSource?.Cancel();
        udpClient?.Close();
    }

    private void ListenLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                // remoteEP를 매번 새로 생성하여 어떤 기기에서든 데이터를 받을 수 있게 함
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] receivedBytes = udpClient.Receive(ref remoteEP);

                // 처음 유효한 패킷을 보낸 기기의 주소를 저장
                if (androidEndpoint == null || !androidEndpoint.Equals(remoteEP))
                {
                    androidEndpoint = remoteEP;
                }

                ProcessPacket(receivedBytes);
            }
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted || token.IsCancellationRequested)
        {
            // 리스닝이 중단될 때 발생하는 예외는 정상 종료이므로 무시
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

        // 펜 데이터 패킷 처리 (Bit-packed 형식)
        if (data.Length == 17 && packetType >= PACKET_TYPE_PEN_DATA_MIN && packetType <= PACKET_TYPE_PEN_DATA_MAX)
        {
            ProcessPenDataPacket(data);
        }
        // 기기 정보 요청(Handshake 시작) 패킷 처리
        else if (data.Length == 13 && packetType == PACKET_TYPE_DEVICE_INFO_REQ)
        {
            ProcessDeviceInfoPacket(data);
            // Handshake 응답(ACK) 전송
            SendControlPacket(PACKET_TYPE_DEVICE_INFO_ACK);
        }
        // Heartbeat Ping 패킷 처리
        else if (data.Length == 1 && packetType == PACKET_TYPE_HEARTBEAT_PING)
        {
            // Heartbeat 응답(Pong) 전송
            SendControlPacket(PACKET_TYPE_HEARTBEAT_PONG);
        }
    }

    private void ProcessPenDataPacket(byte[] data)
    {
        byte actionAndFlags = data[0];
        byte action = (byte)(actionAndFlags & 0x0F); // 하위 4비트
        bool isBarrelPressed = (actionAndFlags & (1 << 4)) != 0; // 5번째 비트

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
        // float refreshRate = BitConverter.ToSingle(data, 9); // 필요 시 사용

        // 이벤트를 통해 메인 UI 스레드로 기기 정보를 전달
        OnDeviceInfoReceived?.Invoke(width, height);
    }

    private void SendControlPacket(byte packetType)
    {
        if (androidEndpoint == null) return;

        byte[] packet = { packetType };
        udpClient.Send(packet, packet.Length, androidEndpoint);
    }
}

// 펜 데이터 구조체 정의
public class UdpPenData
{
    public byte Action { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Pressure { get; set; }
    public bool IsBarrelPressed { get; set; }
    public short TiltX { get; set; }
    public short TiltY { get; set; }
}