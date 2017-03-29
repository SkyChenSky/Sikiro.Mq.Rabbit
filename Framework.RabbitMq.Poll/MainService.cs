using System;
using Framework.RabbitMq.Model;
using Framework.RabbitMq.RabbitMqProxyConfig;
using FrameWork.Extension;

namespace Framework.RabbitMq.Pull
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
                NetworkRecoveryInterval = new TimeSpan(600),
                Host = "localhost",
                UserName = "admin",
                Password = "admin"
            });
        }

        public bool Start()
        {
            var input = Input();
            while (input.ToLower() != "n")
            {
                _rabbitMqProxy.Pull<MessageModel>(msg =>
                {
                    Console.WriteLine(msg.ToJson());
                });

                input = Input();
            }

            return true;
        }


        public bool Stop()
        {
            _rabbitMqProxy.Dispose();
            return true;
        }

        private static string Input()
        {
            Console.WriteLine("是否从队列取一条数据：Y/N");
            var input = Console.ReadLine();
            return input;
        }
    }
}
