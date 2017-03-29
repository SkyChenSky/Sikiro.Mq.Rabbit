using System;
using Framework.RabbitMq.RabbitMqProxyConfig;

namespace Framework.RabbitMq.Model
{
    [RabbitMq("SkyChen.QueueName", ExchangeName = "SkyChen.ExchangeName", IsProperties = false)]
    public class MessageModel
    {
        public string Msg { get; set; }

        public DateTime CreateDateTime { get; set; }
    }
}
