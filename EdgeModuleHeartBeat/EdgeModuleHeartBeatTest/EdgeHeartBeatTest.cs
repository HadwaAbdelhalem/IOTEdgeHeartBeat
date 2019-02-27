using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.Devices;
using System;
using System.Threading;
using Microsoft.Azure.EventHubs;
using System.Text;

namespace EdgeModuleHeartBeatTest
{
    public static class EdgeHeartBeatTest
    {
        [FunctionName("HeartBeat")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");
            // parse query parameter
            string iotHub = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "iotHub", true) == 0)
                .Value;

            string deviceId = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "deviceId", true) == 0)
                .Value;

            string moduleId = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "moduleId", true) == 0)
                .Value;

            string methodName = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "methodName", true) == 0)
                .Value;

            string eventHubsCompatibleEndpoint = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "eventHubsCompatibleEndpoint", true) == 0)
                .Value;

            string eventHubsCompatiblePath = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "eventHubsCompatiblePath", true) == 0)
                .Value;

            // Get request body
            dynamic data = await req.Content.ReadAsAsync<object>();

            if (iotHub == null) iotHub = data?.iotHub;
            if (deviceId == null) deviceId = data?.deviceId;
            if (moduleId == null) moduleId = data?.moduleId;
            if (methodName == null) methodName = data?.methodName;
            if (eventHubsCompatibleEndpoint == null) eventHubsCompatibleEndpoint = data?.eventHubsCompatibleEndpoint;
            if (eventHubsCompatiblePath == null) eventHubsCompatiblePath = data?.eventHubsCompatiblePath;

            log.Info($"iotHub [{iotHub}]");
            log.Info($"deviceId [{deviceId}]");
            log.Info($"moduleId [{moduleId}]");
            log.Info($"methodName [{methodName}]");
            log.Info($"eventHubsCompatibleEndpoint [{eventHubsCompatibleEndpoint}]");
            log.Info($"eventHubsCompatiblePath [{eventHubsCompatiblePath}]");

            // Extarct shared access key
            var iotHubSasKey = iotHub.Split(';').First(e => e.Contains("SharedAccessKey=")).Substring("SharedAccessKey=".Length);
            var iotHubSasKeyName = iotHub.Split(';').First(e => e.Contains("SharedAccessKeyName=")).Substring("SharedAccessKeyName=".Length);

            log.Info($"iotHubSasKey [{iotHubSasKey}]");

            // Create an EventHubClient instance to connect to the IoT Hub Event Hubs-compatible endpoint
            EventHubsConnectionStringBuilder connectionString = new EventHubsConnectionStringBuilder(new Uri(eventHubsCompatibleEndpoint), eventHubsCompatiblePath, iotHubSasKeyName, iotHubSasKey);
            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(connectionString.ToString());

            // Create a PartitionReciever for each partition on the hub.
            var runtimeInfo = await eventHubClient.GetRuntimeInformationAsync();
            var d2cPartitions = runtimeInfo.PartitionIds;

            var invokeTime = DateTime.Now;
            var hubToDevice = await HubToDeviceTest(iotHub, deviceId, moduleId, methodName);
            log.Info($"hub to device tested : result  [{hubToDevice.Key} --{hubToDevice.Value}]");

            try
            {
                foreach (string partition in d2cPartitions)
                {
                    var eventHubReceiver = eventHubClient.CreateReceiver("$Default", partition, EventPosition.FromEnqueuedTime(invokeTime));
                    var timer = DateTime.UtcNow.AddSeconds(10);
                    while (DateTime.UtcNow < timer)
                    {
                        var events = await eventHubReceiver.ReceiveAsync(300);
                        if (events == null) continue;
                        foreach (EventData eventData in events)
                        {
                            string deatiledEventData = Encoding.UTF8.GetString(eventData.Body.Array);
                            foreach (var prop in eventData.Properties)
                            {
                                if (prop.Key.ToLower() == "messagetype" && ((string)prop.Value).ToLower() == "heartbeat")
                                    if (deatiledEventData.Contains(deviceId) && deatiledEventData.Contains(moduleId))
                                    {
                                        return req.CreateErrorResponse(HttpStatusCode.OK, $"Hub to device Validated :{hubToDevice.Key}, and Device to hub Validated:True");
                                    }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.Error($"Failed to monitor iot hub evetns,Error:{e.Message}");
                return req.CreateErrorResponse(HttpStatusCode.InternalServerError, e.Message);
            }
            return req.CreateErrorResponse(HttpStatusCode.OK, $"Hub to device Validated :{hubToDevice.Key}, and Device to hub Validated:False");

        }


        private static async Task<KeyValuePair<bool, string>> HubToDeviceTest(string iotHub, string deviceId, string moduleId, string methodName)
        {
            var serviceClient = ServiceClient.CreateFromConnectionString(iotHub);

            var methodInvocation = new CloudToDeviceMethod(methodName) { ResponseTimeout = TimeSpan.FromSeconds(10) };

            var response = await serviceClient.InvokeDeviceMethodAsync(deviceId, moduleId, methodInvocation);

            return new KeyValuePair<bool, string>(response.Status.ToString() == "200", response.ToString());

        }
    }
}
