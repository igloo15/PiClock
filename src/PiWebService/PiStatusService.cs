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
using Timer = System.Timers.Timer;

namespace PiWebService
{
    public class PiStatusService : IHostedService
    {
        private Timer _timer;
        private ILogger _logger;
        private IHostApplicationLifetime _lifetime;

        public PiStatus Status { get; private set; } = new PiStatus();

        public PiStatusService(ILogger<PiStatusService> logger, IHostApplicationLifetime lifetime)
        {
            _lifetime = lifetime;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(2000);
            _timer.Elapsed += PiStatusCheck;
            _timer.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.Stop();
            return Task.CompletedTask;
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
