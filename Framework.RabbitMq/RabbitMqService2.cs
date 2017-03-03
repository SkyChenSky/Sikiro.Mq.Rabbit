using System;
using EasyNetQ;
using EasyNetQ.FluentConfiguration;
using FrameWork.Extension;

namespace FrameWork.RabbitMq
{
    /// <summary>
    /// 
    /// </summary>
    public class RabbitMqService
    {
        #region EasyNetQ的封装
        private readonly string _connString = "RabbitMq".ValueOfAppSetting();
        private IBus _iBus;

        public RabbitMqService()
        {
            _iBus = RabbitHutch.CreateBus(_connString);
        }

        public void Publish<T>(T message) where T : class
        {
            _iBus.Publish(message);
        }

        public void Subscribe<T>(string subscriptionId, Action<T> onMessage, ushort? prefetchCount = null,
            string topic = null, bool? autoDelete = null,
            int? priority = null
        ) where T : class
        {
            Action<ISubscriptionConfiguration> config = x =>
            {
                if (!topic.IsNullOrEmpty())
                    x.WithTopic(topic);

                if (autoDelete.HasValue)
                    x.WithAutoDelete(autoDelete.Value);

                if (priority.HasValue && prefetchCount.HasValue)
                    x.WithPrefetchCount(prefetchCount.Value);

                if (autoDelete.HasValue)
                    x.WithAutoDelete(autoDelete.Value);
            };

            _iBus.Subscribe(subscriptionId, onMessage, config);
        }

        public void Dispose()
        {
            if (_iBus.IsNull())
                return;

            _iBus.Dispose();
            _iBus = null;
        }
        #endregion
    }
}
