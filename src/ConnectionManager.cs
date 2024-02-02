using System.Text.Json;
using System.Text.Json.Serialization;
using gspro_r10.OpenConnect;
using Microsoft.Extensions.Configuration;
using MediaFoundation;
using System;

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