# Azure IOT Edge Build, Deploy, Test, and Monitor 

This is sample to step by step Azure iot Edge module development and release process.
The repo have multiple solutions

1) An IOT edge module that implements a direct "HeartBeat" to be used for smoke test and health check post deploymentmethod.

2) An azure function that levarge the c# Azure IOT service SDK to Invoke the "HeartBeat" method and validate the device to hub bi directional connectivity.

The Edge module uses the Filter Module Sample and implements an extra Direct Method "HeartBeat" that gets invoked by the azure function on demand. The"HeartBeat" method triggers sends a heart beat message to upstream, the IOT Hub, to report the module heatr beat was invoked.

The Azure function is http triggered function. It is called in the Deployment pipeline after deploying the Edge module to the device to ensure the new deployed module on a device is up and reachable from the IOT hub and that the device can send connect/commnucate and send messages to the iot hub post the deployment.

The Azure function can be called outside of the Deployment pipeline as well, You can run the bash script that triggers the azure function on a time based and you will get a timely based health checker running against your device and you can like the result to any altering system you have.

## How to deploy the HeartBeat Azure function.

1) Clone and build the EdgeModuleHeartBeat project
   
2) Deploy the EdgeModuleHeartBeat to azure. follow instructions [Here](https://blogs.msdn.microsoft.com/benjaminperkins/2018/04/05/deploy-an-azure-function-created-from-visual-studio/) 
   
3) From the Zure portal, Get the Functin URL to be used later

4) your HeartBest function is ready to be called.

## How to implement Heart beat direct method in your Edge Module code
1)Add the Heart Beat Direct method to your Edge Module Program.cs.

```{r}
 private static Task<MethodResponse> HeartBeat(MethodRequest methodRequest, object userContext) => Task.Run(() =>
        {
            var heartBeatMessage = new Message(Encoding.UTF8.GetBytes(string.Format("Device [{0}], Module [YourMODULEID] Running",System.Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID"))));
            heartBeatMessage.Properties.Add("MessageType", "heartBeat");
            ioTHubModuleClient.SendEventAsync("heartbeat", heartBeatMessage);
            return new MethodResponse(200);

        });

```

2)Update the Main function in your EdgeModule program.cs to handle the HeartBeat Direct method invocation

```{r}
  await ioTHubModuleClient.SetMethodHandlerAsync("HearBeat", HeartBeat, null);
  Console.WriteLine("Set Heartbeat Method Handler:HeartBeat.");
```

3)Update the "edgeHub" routes section in the deployment.template.json file to add a route for your module to send the message to the hub.

   
```{r}
 "$edgeHub": {
      "properties.desired": {
        "routes": {
          "EventHubReaderModuleToIoTHub": "FROM /messages/modules/YOUMODULENAME/outputs/heartbeat INTO $upstream"
        }
      }

```

4)Build your module code. and Deploy to your EdgeDevice.Your heart beat changes should be deployed to your device.


## How to run Heart beat direct Test.
Since the HeartBeat Azure function does not store any secrets. You need to pass the needed values for it in the call.

Here is a sample bash script the can be called from your CD pipeline after deployment or on time based scheduled task to check the health check of your deployed module on a device

```{r}

# IOThub connection string, Edge device Id, your Edge Module Id
deviceId="YOUREDGEDEVICEID"
moduleId="YOUREDGEMODULEID"
iotHub="YOUIOTHUBCONNECTIONSTRING"

# Name of the direct method  you added to your Edge module earlier
methodName="HeartBeat"

#can be retirevied by azure cli cmd [az iot hub show --query properties.eventHubEndpoints.events.path --name {your IoT Hub name}]
eventHubsCompatiblePath=""

#Get it by azure cLI cmd[az iot hub show --query properties.eventHubEndpoints.events.endpoint --name {your IoT Hub name}]
eventHubsCompatibleEndpoint=""


#the URI of the HeartBeatTest azure function you deployed earlier
fnUri=""


echo "Start heart beat test"
uri=$fnUri"deviceId="$deviceId"&moduleId="$moduleId"&methodName="$methodName"&eventHubsCompatiblePath="$eventHubsCompatiblePath"&eventHubsCompatibleEndpoint="$eventHubsCompatibleEndpoint"&iotHub="$iotHub"


response=$(curl --request GET $uri)
if [[ $response == *"Hub to device Validated :True, and Device to hub Validated:True"* ]]; then
  echo "Heart beat test passed:"$response
  exit 0
fi
echo "Heart beat test failed:"$response
exit 1

```
