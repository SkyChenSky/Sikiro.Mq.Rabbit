using System;
using Framework.RabbitMq.Model;
using FrameWork.Extension;
using FrameWork.RabbitMq;
using FrameWork.RabbitMq.RabbitMqProxyConfig;

namespace Framework.RabbitMq.Subscribe
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
            _rabbitMqProxy.Subscribe<MessageModel>(msg =>
            {
                var json = msg.ToJson();
                Console.WriteLine(json);
            });

            return true;
        }

        public bool Stop()
        {
            _rabbitMqProxy.Dispose();
            return true;
        }
    }
}
