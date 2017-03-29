using System;
using Framework.RabbitMq.Model;
using Framework.RabbitMq.RabbitMqProxyConfig;
using FrameWork.Extension;

namespace Framework.RabbitMq.RpcService
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
            _rabbitMqProxy.RpcService<RpcMsgModel>(msg =>
            {
                Console.WriteLine("接受信息：");
                Console.WriteLine(msg.ToJson());

                var input = Input();
                msg.CreateDateTime = DateTime.Now;
                msg.Msg = input;

                return msg;
            });

            return true;
        }

        public bool Stop()
        {
            _rabbitMqProxy.Dispose();
            return true;
        }

        private static string Input()
        {
            Console.WriteLine("请输入响应信息：");
            var input = Console.ReadLine();
            return input;
        }
    }
}
