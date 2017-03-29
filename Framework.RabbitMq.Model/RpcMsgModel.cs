using System;
using Framework.RabbitMq.RabbitMqProxyConfig;

namespace Framework.RabbitMq.Model
{
    [RabbitMq("SkyChen.Rpc.QueueName", ExchangeName = "SkyChen.Rpc.ExchangeName", IsProperties = false)]
    public class RpcMsgModel
    {
        public string Msg { get; set; }

        public DateTime CreateDateTime { get; set; }
    }
}
