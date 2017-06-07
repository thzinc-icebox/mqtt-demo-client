using System;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace app
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new MqttClient("iot.eclipse.org");
            client.MqttMsgPublishReceived += (sender, e) =>
            {
                Console.WriteLine(System.Text.Encoding.UTF8.GetString(e.Message));
            };
            var clientId = Guid.NewGuid().ToString();
            Console.WriteLine($"Connecting with client ID {clientId}");
            var connectResult = client.Connect(clientId);
            if (connectResult == MqttMsgConnack.CONN_ACCEPTED)
            {
                Console.WriteLine("Connected!");
                client.Subscribe(new[] { "/demo/console" }, new[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
            }
            else
            {
                Console.Error.WriteLine("Could not connect!");
            }
        }
    }
}
