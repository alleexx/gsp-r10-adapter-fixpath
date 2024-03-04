using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Timers;
using Google.Protobuf.WellKnownTypes;
using gspro_r10.OpenConnect;
using Microsoft.Extensions.Configuration;
using HttpClient = System.Net.Http.HttpClient;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Extensions.Primitives;
using System.Globalization;
using gspro_r10.R10;

namespace gspro_r10
{

class MiniClient : HttpClient
{
    private System.Timers.Timer _timer;
    public bool InitiallyConnected { get; private set; }
    public ConnectionManager ConnectionManager { get; set; }
    private bool _stop;
    private static String _host;
    private static int _port;
    private static String _shotdata;
    private static double _mph_in_ms = 2.23694;

    public MiniClient(ConnectionManager connectionManager, IConfigurationSection configuration)
      : base()
    {
      _host = configuration["mini-ip"] ?? "127.0.0.1";
      _port = int.Parse(configuration["mini-port"] ?? "80");
      _timer = new System.Timers.Timer(1000);
      ConnectionManager = connectionManager;
      _shotdata = "";
    }

    public void Start()
    {
            
        MiniConnectLogger.LogMiniInfo($"Mini HTTP Client reading from IP: {_host} Port: {_port}...");

        _timer.Elapsed += OnTimedEvent;
        _timer.AutoReset = true;
        _timer.Enabled = true;
               
           
    }

    private async void OnTimedEvent(Object source, ElapsedEventArgs e)
    {

        _timer.Enabled = false;



        String response = await Connect();
        if (response != null) 
            { 

            
            if (response != _shotdata) 
                {
                    MiniConnectLogger.LogMiniInfo($"New Shot: {response}");

                    _shotdata = response;

                    // clean json

                    JObject jsonObj = JObject.Parse(response);
                    JObject cleanedObj = (JObject)RemoveWhitespaces(jsonObj);

                    MiniShotData shotdatajson = JsonSerializer.Deserialize<MiniShotData>(cleanedObj.ToString());
                    
                    OpenConnect.BallData? BallData = BallDataFromMiniBallData(shotdatajson);
                    OpenConnect.ClubData? ClubData = ClubDataFromMiniClubData(shotdatajson);

                    ConnectionManager.SendShot(BallData, ClubData);

                }
            }


        _timer.Enabled = true;
    }

    private async Task<String> Connect()
    {
        // Call asynchronous network methods in a try/catch block to handle exceptions.
        try
        {
            using HttpResponseMessage response = await GetAsync($"http://{_host}:{_port}/SCAMIMG/CURRENT/shotinfo.json");
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            // Above three lines can be replaced with new helper method below
            // string responseBody = await client.GetStringAsync(uri);
            // MiniConnectLogger.LogMiniIncoming(responseBody);
                // Console.WriteLine(responseBody);

            return responseBody;
            }
        catch (HttpRequestException e)
        {
            MiniConnectLogger.LogMiniError($"Message: {e.Message}");
            return null;
        }
    }



    public static OpenConnect.BallData? BallDataFromMiniBallData(MiniShotData shotdata)
    {
        if (shotdata == null) return null;
            return new OpenConnect.BallData()
            {
                Speed = double.Parse(shotdata.DATA.ballspeed, NumberStyles.Number, CultureInfo.InvariantCulture) * _mph_in_ms,
                BackSpin = double.Parse(shotdata.DATA.backspin, NumberStyles.Number, CultureInfo.InvariantCulture),
                SideSpin = double.Parse(shotdata.DATA.sidespin, NumberStyles.Number, CultureInfo.InvariantCulture),
                HLA = double.Parse(shotdata.DATA.azimuth, NumberStyles.Number, CultureInfo.InvariantCulture),
                VLA = double.Parse(shotdata.DATA.incline, NumberStyles.Number, CultureInfo.InvariantCulture)
            };
    }

    public static OpenConnect.ClubData? ClubDataFromMiniClubData(MiniShotData shotdata)
    {
        if (shotdata == null) return null;
        return new OpenConnect.ClubData()
        {
            Speed = double.Parse(shotdata.DATA.clubspeed, NumberStyles.Number, CultureInfo.InvariantCulture) * _mph_in_ms,
            SpeedAtImpact = double.Parse(shotdata.DATA.clubspeed, NumberStyles.Number, CultureInfo.InvariantCulture) * _mph_in_ms,
            Path = double.Parse(shotdata.DATA.clubpath, NumberStyles.Number, CultureInfo.InvariantCulture),
            FaceToTarget = double.Parse(shotdata.DATA.clubfaceangle, NumberStyles.Number, CultureInfo.InvariantCulture),
            AngleOfAttack = double.Parse(shotdata.DATA.clubattackangle, NumberStyles.Number, CultureInfo.InvariantCulture),
            // HorizontalFaceImpact = double.Parse(shotdata.DATA.clubfaceimpactHorizontal, NumberStyles.Number, CultureInfo.InvariantCulture),
            // VerticalFaceImpact = double.Parse(shotdata.DATA.clubfaceimpactVertical, NumberStyles.Number, CultureInfo.InvariantCulture)

        };
    }



    private static JToken RemoveWhitespaces(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.String:
                return new JValue(((string)token).Trim());
            case JTokenType.Object:
                JObject obj = (JObject)token;
                return new JObject(obj.Properties().Select(p => new JProperty(p.Name, RemoveWhitespaces(p.Value))));
            case JTokenType.Array:
                JArray arr = (JArray)token;
                return new JArray(arr.Select(RemoveWhitespaces));
            default:
                return token;
        }
    }


    }
    public class MiniShotData
    {
        public ShotData DATA { get; set; }

    }

    public class ShotData
    {
        public string ballspeed { get; set; }
        public string backspin { get; set; }
        public string sidespin { get; set; }
        public string azimuth { get; set; }
        public string incline { get; set; }
        public string clubspeed { get; set; }

        public string clubpath { get; set; }
        public string clubfaceangle { get; set; }
        public string clubattackangle { get; set; }
        public string clubfaceimpactHorizontal { get; set; }
        public string clubfaceimpactVertical { get; set; }
        
    }

public static class MiniConnectLogger
  {
    public static void LogMiniInfo(string message) => LogMiniMessage(message, LogMessageType.Informational);
    public static void LogMiniError(string message) => LogMiniMessage(message, LogMessageType.Error);
    public static void LogMiniOutgoing(string message) => LogMiniMessage(message, LogMessageType.Outgoing);
    public static void LogMiniIncoming(string message) => LogMiniMessage(message, LogMessageType.Incoming);
    public static void LogMiniMessage(string message, LogMessageType type) => BaseLogger.LogMessage(message, "Mini", type, ConsoleColor.Green);

  }
}