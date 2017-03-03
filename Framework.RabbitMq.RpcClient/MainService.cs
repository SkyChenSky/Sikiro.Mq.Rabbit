using System;
using FrameWork.RabbitMq.RabbitMqProxyConfig;

namespace FrameWork.RabbitMq.RpcClient
{
    public class MainService
    {
        private readonly RabbitMqService _rabbitMqProxy;
        public MainService()
        {
            _rabbitMqProxy = new RabbitMqService(new MqConfig
            {
                AutomaticRecoveryEnabled = true,
                HeartBeat = 60,
                NetworkRecoveryInterval = new TimeSpan(60),
                Host = "localhost",
                UserName = "admin",
                Password = "admin"
            });
        }

        public bool Start()
        {
            _rabbitMqProxy.Subscribe<Log>(msg =>
            {
                Console.WriteLine("asdasd");
            });

            return true;
        }

        public bool Stop()
        {
            _rabbitMqProxy.Dispose();
            return true;
        }
    }


    [RabbitMqQueue("chengongTest3.QueueName", ExchangeName = "chengongTest3.ExchangeName", IsProperties = false)]
    public class Log
    {
        public string LogName { get; set; }

        public int Count { get; set; }

        public DateTime CreateDateTime { get; set; }
    }
}
