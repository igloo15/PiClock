using Iot.Device.Common;
using Iot.Device.DHTxx;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Subscribing;
using Timer = System.Timers.Timer;
using MQTTnet;
using MQTTnet.Client.Options;
using System.Linq;
using System.Text;
using UnitsNet;
using UnitsNet.Units;
using System.Text.Json;
using System.Collections.Generic;

namespace PiWebService
{
    public class PiStatusService : IHostedService
    {
        //private Timer _timer;
        private ILogger _logger;

        private IHostApplicationLifetime _lifetime;
        private IMqttClient _client;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private int _tempIndex = 0;
        private int _humIndex = 0;
        private List<PiCommand> _commands = new List<PiCommand>();

        public PiStatus Status { get; private set; } = new PiStatus();

        public PiStatusService(ILogger<PiStatusService> logger, IHostApplicationLifetime lifetime)
        {
            Status = new PiStatus();
            Status.Clocks = new [] {
                new PiClockStatus {
                    Name = "work",
                    DisplayName = "Work Time",
                    Status = "stopped"
                },
                new PiClockStatus {
                    Name = "personal",
                    DisplayName = "Personal Time",
                    Status = "stopped"
                }
            };
            _lifetime = lifetime;
            _logger = logger;
            _client = new MqttFactory().CreateMqttClient();
            _client.UseApplicationMessageReceivedHandler(HandleMessage);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            MqttClientAuthenticateResult result = await _client.ConnectAsync(new MqttClientOptionsBuilder().WithClientId("PiClock").WithTcpServer("mqtt.igloo15").Build());
            if (result.ResultCode != MqttClientConnectResultCode.Success)
            {
                _logger?.LogError("Failed to connect to mqtt. Reason: {result}", result.ReasonString);
                _lifetime.StopApplication();
            }
            else
            {
                _logger?.LogInformation("Connected to mqtt server");
                var subResult = await _client.SubscribeAsync("office/+");
                if (!subResult.Items.All(i =>
                    i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS0 ||
                    i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS1 ||
                    i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS2))
                {
                    _logger.LogError("Failed to subscribe to topic");
                    _lifetime.StopApplication();
                }
                var subTwoResult = await _client.SubscribeAsync("igloo15/commands/#");
                if (!subTwoResult.Items.All(i =>
                    i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS0 ||
                    i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS1 ||
                    i.ResultCode == MqttClientSubscribeResultCode.GrantedQoS2))
                {
                    _logger.LogError("Failed to subscribe to topic");
                    _lifetime.StopApplication();
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.DisconnectAsync();
        }

        public async Task PublishClock(PiClockStatus status)
        {
            var clock = Status.Clocks.FirstOrDefault(s => s.Name == status.Name);
            clock?.Update(status);
            await _client.PublishAsync($"igloo15/clocks/{status.Name}", JsonSerializer.Serialize(status));
        }

        public PiCommand[] GetCommands()
        {
            var allCommands = _commands.ToArray();
            _commands.Clear();
            return allCommands;
        }

        private async Task HandleMessage(MqttApplicationMessageReceivedEventArgs args)
        {
            await args.AcknowledgeAsync(_cts.Token);
            try
            {
                if (args.ApplicationMessage.Topic.Contains("temp"))
                {
                    var temp = ParseNumber(args.ApplicationMessage.Payload);
                    Status.Temp = temp;
                    _tempIndex++;
                    _logger.LogInformation("Temp Updated to {temp}", Status.Temp);
                    UpdateExtraData();
                }
                else if (args.ApplicationMessage.Topic.Contains("humidity"))
                {
                    var result = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
                    var hum = ParseNumber(args.ApplicationMessage.Payload);
                    Status.Humidity = hum;
                    _logger.LogInformation("Humidity Updated to {hum}", Status.Humidity);
                    UpdateExtraData();
                }
                else
                {
                    _logger.LogInformation("Received message on topic: {topic}", args.ApplicationMessage.Topic);
                    ProcessCommandMessages(args.ApplicationMessage);
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation(e, "Failed to parse mqtt message on topic {topic}", args.ApplicationMessage.Topic);
            }
        }

        private void UpdateExtraData()
        {
            var temp = new Temperature(Status.Temp, TemperatureUnit.DegreeFahrenheit);
            var hum = new RelativeHumidity(Status.Humidity, RelativeHumidityUnit.Percent);
            Status.HeatIndex = WeatherHelper.CalculateHeatIndex(temp, hum).DegreesFahrenheit;
            _logger.LogInformation("Heat Index Updated to {hi}", Status.HeatIndex);
            Status.DewPoint = WeatherHelper.CalculateDewPoint(temp, hum).DegreesFahrenheit;
            _logger.LogInformation("Dew Point Updated to {dew}", Status.DewPoint);
            Status.Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private double ParseNumber(byte[] bytes)
        {
            var result = Encoding.UTF8.GetString(bytes);
            return ParseNumber(result);
        }

        private double ParseNumber(string data)
        {
            return Double.Parse(data);
        }

        private void ProcessCommandMessages(MqttApplicationMessage message)
        {
            var topicArr = message.Topic.Split('/');
            if(topicArr.Length > 3) {
                if(topicArr[2] == "clock") {
                    _commands.Add(new PiCommand() {
                        Name = topicArr[3],
                        Data = JsonDocument.Parse(Encoding.UTF8.GetString(message.Payload))
                    });
                }
            }
        }

        private Dht22 GetSensor()
        {
            GpioDriver driver = new RaspberryPi3Driver();
            GpioController controller = new GpioController(PinNumberingScheme.Logical, driver);
            return new Dht22(4, gpioController: controller);
        }

        private void PiStatusCheck(object sender, ElapsedEventArgs args)
        {
            try
            {
                using (var sensor = GetSensor())
                {
                    var temp = sensor.Temperature;
                    var hum = sensor.Humidity;
                    if (sensor.IsLastReadSuccessful && CheckDelta(temp, hum))
                    {
                        Status.Temp = temp.DegreesFahrenheit;
                        Status.Humidity = hum.Percent;
                        Status.HeatIndex = WeatherHelper.CalculateHeatIndex(temp, hum).DegreesFahrenheit;
                        Status.DewPoint = WeatherHelper.CalculateDewPoint(temp, hum).DegreesFahrenheit;
                        Status.ErrorsSinceLastUpdate = 0;
                        Status.TimeSinceLastUpdate = 0;

                        _logger?.LogInformation(
                            $"Temperature: {temp.DegreesFahrenheit}\u00B0F, Relative humidity: {hum.Percent}%,"
                            + $" Heat index: {Status.HeatIndex:0.#}\u00B0F, Dew point: {Status.DewPoint:0.#}\u00B0F");
                    }
                    else
                    {
                        Status.ErrorsSinceLastUpdate++;
                        Status.TimeSinceLastUpdate = Status.ErrorsSinceLastUpdate * 2;
                        if (Status.ErrorsSinceLastUpdate % 30 == 0)
                        {
                            _logger.LogError($"{Status.ErrorsSinceLastUpdate} Errors have occured");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Exception Occurred While Reading Sensor");
                _lifetime?.StopApplication();
            }
        }

        private bool CheckDelta(UnitsNet.Temperature temp, UnitsNet.RelativeHumidity hum)
        {
            if (Status.Temp == 0 || Status.Humidity == 0)
            {
                return true;
            }

            var tempDelta = Math.Abs(temp.DegreesFahrenheit - Status.Temp);
            var humDelta = Math.Abs(hum.Percent - Status.Humidity);

            if (tempDelta < 10 && humDelta < 10)
            {
                return true;
            }

            return false;
        }
    }
}
