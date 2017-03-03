using System.Collections.Generic;

namespace FrameWork.RabbitMq.IRabbitMqProxy
{
    public interface IEvent : IMessage
    {
        List<ICommand> Commands { get; set; }
    }
}