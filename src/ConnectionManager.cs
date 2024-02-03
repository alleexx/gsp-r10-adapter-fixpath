using System.Text.Json;
using System.Text.Json.Serialization;
using gspro_r10.OpenConnect;
using Microsoft.Extensions.Configuration;
using MediaFoundation;
using System;
using System.Net.Sockets;
using System.Text;

namespace gspro_r10
{
  public class ConnectionManager: IDisposable
  {
    private R10ConnectionServer? R10Server;
    private OpenConnectClient OpenConnectClient;
    private BluetoothConnection? BluetoothConnection { get; }
    internal HttpPuttingServer? PuttingConnection { get; }
    public event ClubChangedEventHandler? ClubChanged;
    public delegate void ClubChangedEventHandler(object sender, ClubChangedEventArgs e);
    public class ClubChangedEventArgs: EventArgs
    {
      public Club Club { get; set; }
    }

    private JsonSerializerOptions serializerSettings = new JsonSerializerOptions()
    {
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private int shotNumber = 0;
    private bool disposedValue;

    private bool fixpath = false;

    private bool lastClubPT = false;

    public ConnectionManager(IConfigurationRoot configuration)
    {
      OpenConnectClient = new OpenConnectClient(this, configuration.GetSection("openConnect"));
      OpenConnectClient.ConnectAsync();

      if (bool.Parse(configuration.GetSection("r10E6Server")["enabled"] ?? "false"))
      {
        R10Server = new R10ConnectionServer(this, configuration.GetSection("r10E6Server"));
        R10Server.Start();
      }

      if (bool.Parse(configuration.GetSection("bluetooth")["enabled"] ?? "false"))
        BluetoothConnection = new BluetoothConnection(this, configuration.GetSection("bluetooth"));

      if (bool.Parse(configuration.GetSection("putting")["enabled"] ?? "false"))
      {
        PuttingConnection = new HttpPuttingServer(this, configuration.GetSection("putting"));
        PuttingConnection.Start();
        // list all connected webcams by index and name
       

      }
      
      if (bool.Parse(configuration.GetSection("bluetooth")["fixpath"] ?? "false"))
      {
        fixpath = true;
        BaseLogger.LogMessage("R10 - Fixpath - Fixpath enabled", "Main", LogMessageType.Informational);
      }
    }
    
    public static void SendShotImpact(string message, string serverAddress, int port)
    {
        // Create a TCP client
        TcpClient client = new TcpClient(serverAddress, port);
        try
        {
            

            // Get a stream for writing data
            NetworkStream stream = client.GetStream();

            // Convert the message to a byte array
            byte[] bytesToSend = Encoding.UTF8.GetBytes(message);

            // Send the message to the server
            stream.Write(bytesToSend, 0, bytesToSend.Length);

            Console.WriteLine("Message sent to server: {0}", message);

            // Receive a response from the server (optional)
            // byte[] buffer = new byte[1024];
            // int bytesRead = stream.Read(buffer, 0, buffer.Length);
            // string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            // Console.WriteLine("Response from server: {0}", response);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error sending message: " + ex.Message);
        }
        finally
        {
            // Close the client connection
            client.Close();
        }
    }

    
    internal void SendShot(OpenConnect.BallData? ballData, OpenConnect.ClubData? clubData)
    {
      if (fixpath == true && clubData != null &&ballData != null)
      {
        if (clubData.Path >= 19 || clubData.Path <= -19 || clubData.FaceToTarget >=15 || clubData.FaceToTarget <=-15)
        {
          BaseLogger.LogMessage("R10 - Fixpath - ClubData possibly wrong: clubData.Path == 20 OR clubData.FaceToTarget >=15", "Main", LogMessageType.Informational);

          ballData.SideSpin = 0;
          ballData.SpinAxis = 0;
          
          clubData.Path = 0;
          clubData.FaceToTarget = 0;
        }
        if (ballData.HLA >= 20 && ballData.VLA == 0 && ballData.SpinAxis ==-0)
        {
          BaseLogger.LogMessage("R10 - Fixpath - BallData possibly wrong: VLA = 0 and HLA Off The Charts with -0 SpinAxis", "Main", LogMessageType.Informational);

          return;
        }
      }
      string openConnectMessage = JsonSerializer.Serialize(OpenConnectApiMessage.CreateShotData(
        shotNumber++,
        ballData,
        clubData
      ), serializerSettings);
      OpenConnectClient.SendAsync(openConnectMessage);
      try{
        
        SendShotImpact(openConnectMessage, "127.0.0.1", 9999);
      }
      catch (Exception ex){
        BaseLogger.LogMessage(ex.Message, "Main", LogMessageType.Error);
      }
    }

    public void ClubUpdate(Club club)
    {
      Task.Run(() => {
        ClubChanged?.Invoke(this, new ClubChangedEventArgs()
        {
          Club = club
        });
      });

    }

    internal void SendLaunchMonitorReadyUpdate(bool deviceReady)
    {
      OpenConnectClient.SetDeviceReady(deviceReady);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          R10Server?.Dispose();
          PuttingConnection?.Dispose();
          BluetoothConnection?.Dispose();
          OpenConnectClient?.DisconnectAndStop();
          OpenConnectClient?.Dispose();
        }
        disposedValue = true;
      }
    }

    public void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

  }
}