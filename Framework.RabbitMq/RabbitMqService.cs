using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Framework.RabbitMq.RabbitMqProxyConfig;
using FrameWork.Extension;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ExchangeType = RabbitMQ.Client.ExchangeType;

namespace Framework.RabbitMq
{
    #region RabbitMQ.Client原生封装类
    /// <summary>
    /// RabbitMQ.Client原生封装类
    /// </summary>
    public class RabbitMqService : IDisposable
    {
        #region 初始化
        //RabbitMQ建议客户端线程之间不要共用Model，至少要保证共用Model的线程发送消息必须是串行的，但是建议尽量共用Connection。
        private static readonly ConcurrentDictionary<string, IModel> ModelDic =
            new ConcurrentDictionary<string, IModel>();

        private static RabbitMqAttribute _rabbitMqAttribute;

        private const string RabbitMqAttribute = "RabbitMqAttribute";

        private static IConnection _conn;

        private static readonly object LockObj = new object();

        private static void Open(MqConfig config)
        {
            if (_conn != null) return;
            lock (LockObj)
            {
                var factory = new ConnectionFactory
                {
                    //设置主机名
                    HostName = config.Host,

                    //设置心跳时间
                    RequestedHeartbeat = config.HeartBeat,

                    //设置自动重连
                    AutomaticRecoveryEnabled = config.AutomaticRecoveryEnabled,

                    //重连时间
                    NetworkRecoveryInterval = config.NetworkRecoveryInterval,

                    //用户名
                    UserName = config.UserName,

                    //密码
                    Password = config.Password
                };
                factory.AutomaticRecoveryEnabled = true;
                factory.NetworkRecoveryInterval = new TimeSpan(1000);
                _conn = _conn ?? factory.CreateConnection();
            }
        }

        private static RabbitMqAttribute GetRabbitMqAttribute<T>()
        {
            if (_rabbitMqAttribute.IsNull())
            {
                var typeOfT = typeof(T);
                _rabbitMqAttribute = typeOfT.GetAttribute<RabbitMqAttribute>();
            }

            return _rabbitMqAttribute;
        }

        public RabbitMqService(MqConfig config)
        {
            Open(config);
        }
        #endregion

        #region 交换器声明
        /// <summary>
        /// 交换器声明
        /// </summary>
        /// <param name="iModel"></param>
        /// <param name="exchange">交换器</param>
        /// <param name="type">交换器类型：
        /// 1、Direct Exchange – 处理路由键。需要将一个队列绑定到交换机上，要求该消息与一个特定的路由键完全
        /// 匹配。这是一个完整的匹配。如果一个队列绑定到该交换机上要求路由键 “dog”，则只有被标记为“dog”的
        /// 消息才被转发，不会转发dog.puppy，也不会转发dog.guard，只会转发dog
        /// 2、Fanout Exchange – 不处理路由键。你只需要简单的将队列绑定到交换机上。一个发送到交换机的消息都
        /// 会被转发到与该交换机绑定的所有队列上。很像子网广播，每台子网内的主机都获得了一份复制的消息。Fanout
        /// 交换机转发消息是最快的。
        /// 3、Topic Exchange – 将路由键和某模式进行匹配。此时队列需要绑定要一个模式上。符号“#”匹配一个或多
        /// 个词，符号“*”匹配不多不少一个词。因此“audit.#”能够匹配到“audit.irs.corporate”，但是“audit.*”
        /// 只会匹配到“audit.irs”。</param>
        /// <param name="durable">持久化</param>
        /// <param name="autoDelete">自动删除</param>
        /// <param name="arguments">参数</param>
        private static void ExchangeDeclare(IModel iModel, string exchange, string type = ExchangeType.Direct,
            bool durable = true,
            bool autoDelete = false, IDictionary<string, object> arguments = null)
        {
            exchange = exchange.IsNullOrWhiteSpace() ? "" : exchange.Trim();
            iModel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
        }
        #endregion

        #region 队列声明
        /// <summary>
        /// 队列声明
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="queue">队列</param>
        /// <param name="durable">持久化</param>
        /// <param name="exclusive">排他队列，如果一个队列被声明为排他队列，该队列仅对首次声明它的连接可见，
        /// 并在连接断开时自动删除。这里需要注意三点：其一，排他队列是基于连接可见的，同一连接的不同信道是可
        /// 以同时访问同一个连接创建的排他队列的。其二，“首次”，如果一个连接已经声明了一个排他队列，其他连
        /// 接是不允许建立同名的排他队列的，这个与普通队列不同。其三，即使该队列是持久化的，一旦连接关闭或者
        /// 客户端退出，该排他队列都会被自动删除的。这种队列适用于只限于一个客户端发送读取消息的应用场景。</param>
        /// <param name="autoDelete">自动删除</param>
        /// <param name="arguments">参数</param>
        private static void QueueDeclare(IModel channel, string queue, bool durable = true, bool exclusive = false,
            bool autoDelete = false, IDictionary<string, object> arguments = null)
        {
            queue = queue.IsNullOrWhiteSpace() ? "UndefinedQueueName" : queue.Trim();
            channel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
        }
        #endregion

        #region 获取Model

        /// <summary>
        /// 获取Model
        /// </summary>
        /// <param name="exchange">交换机名称</param>
        /// <param name="queue">队列名称</param>
        /// <param name="routingKey"></param>
        /// <param name="isProperties">是否持久化</param>
        /// <returns></returns>
        private static IModel GetModel(string exchange, string queue, string routingKey, bool isProperties = false)
        {
            return ModelDic.GetOrAdd(queue, key =>
              {
                  var model = _conn.CreateModel();
                  ExchangeDeclare(model, exchange, ExchangeType.Fanout, isProperties);
                  QueueDeclare(model, queue, isProperties);
                  model.QueueBind(queue, exchange, routingKey);
                  ModelDic[queue] = model;
                  return model;
              });
        }

        /// <summary>
        /// 获取Model
        /// </summary>
        /// <param name="queue">队列名称</param>
        /// <param name="isProperties"></param>
        /// <returns></returns>
        private static IModel GetModel(string queue, bool isProperties = false)
        {
            return ModelDic.GetOrAdd(queue, value =>
             {
                 var model = _conn.CreateModel();
                 QueueDeclare(model, queue, isProperties);

                 //每次消费的消息数
                 model.BasicQos(0, 1, false);

                 ModelDic[queue] = model;

                 return model;
             });
        }
        #endregion

        #region 发布消息

        /// <summary>
        /// 发布消息
        /// </summary>
        /// <param name="command">指令</param>
        /// <returns></returns>
        public void Publish<T>(T command) where T : class
        {
            var queueInfo = GetRabbitMqAttribute<T>();

            if (queueInfo.IsNull())
                throw new ArgumentException(RabbitMqAttribute);

            var body = command.ToJson();
            var exchange = queueInfo.ExchangeName;
            var queue = queueInfo.QueueName;
            var routingKey = queueInfo.ExchangeName;
            var isProperties = queueInfo.IsProperties;

            Publish(exchange, queue, routingKey, body, isProperties);
        }

        /// <summary>
        /// 发布消息
        /// </summary>
        /// <param name="routingKey">路由键</param>
        /// <param name="body">队列信息</param>
        /// <param name="exchange">交换机名称</param>
        /// <param name="queue">队列名</param>
        /// <param name="isProperties">是否持久化</param>
        /// <returns></returns>
        public void Publish(string exchange, string queue, string routingKey, string body, bool isProperties = false)
        {
            var channel = GetModel(exchange, queue, routingKey, isProperties);

            try
            {
                channel.BasicPublish(exchange, routingKey, null, body.SerializeUtf8());
            }
            catch (Exception ex)
            {
                throw ex.GetInnestException();
            }
        }

        /// <summary>
        /// 发布消息到死信队列
        /// </summary>
        /// <param name="body">死信信息</param>
        /// <param name="ex">异常</param>
        /// <param name="queue">死信队列名称</param>
        /// <returns></returns>
        private void PublishToDead<T>(string queue, string body, Exception ex) where T : class
        {
            var queueInfo = typeof(T).GetAttribute<RabbitMqAttribute>();
            if (queueInfo.IsNull())
                throw new ArgumentException(RabbitMqAttribute);

            var deadLetterExchange = queueInfo.ExchangeName;
            string deadLetterQueue = queueInfo.QueueName;
            var deadLetterRoutingKey = deadLetterExchange;
            var deadLetterBody = new DeadLetterQueue
            {
                Body = body,
                CreateDateTime = DateTime.Now,
                ExceptionMsg = ex.GetInnestException().Message,
                Queue = queue,
                RoutingKey = deadLetterExchange,
                Exchange = deadLetterRoutingKey
            };

            Publish(deadLetterExchange, deadLetterQueue, deadLetterRoutingKey, deadLetterBody.ToJson());
        }
        #endregion

        #region 订阅消息

        /// <summary>
        /// 接收消息
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler">消费处理</param>
        public void Subscribe<T>(Action<T> handler) where T : class
        {
            var queueInfo = GetRabbitMqAttribute<T>();
            if (queueInfo.IsNull())
                throw new ArgumentException(RabbitMqAttribute);

            var isDeadLetter = typeof(T) == typeof(DeadLetterQueue);

            Subscribe(queueInfo.QueueName, queueInfo.IsProperties, handler, isDeadLetter);
        }

        /// <summary>
        /// 接收消息
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queue">队列名称</param>
        /// <param name="isProperties"></param>
        /// <param name="handler">消费处理</param>
        /// <param name="isDeadLetter"></param>
        public void Subscribe<T>(string queue, bool isProperties, Action<T> handler, bool isDeadLetter) where T : class
        {
            //队列声明
            var channel = GetModel(queue, isProperties);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body;
                var msgStr = SerializeExtension.DeserializeUtf8(body);
                var msg = msgStr.FromJson<T>();
                try
                {
                    handler(msg);
                }
                catch (Exception ex)
                {
                    ex.GetInnestException().WriteToFile("队列接收消息", "RabbitMq");
                    if (!isDeadLetter)
                        PublishToDead<DeadLetterQueue>(queue, msgStr, ex);
                }
                finally
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                }
            };
            channel.BasicConsume(queue, false, consumer);
        }

        #endregion

        #region 获取消息

        /// <summary>
        /// 获取消息
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler">消费处理</param>
        public void Pull<T>(Action<T> handler) where T : class
        {
            var queueInfo = GetRabbitMqAttribute<T>();
            if (queueInfo.IsNull())
                throw new ArgumentException("RabbitMqAttribute");

            Pull(queueInfo.ExchangeName, queueInfo.QueueName, queueInfo.ExchangeName, handler);
        }

        /// <summary>
        /// 获取消息
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="exchange"></param>
        /// <param name="queue"></param>
        /// <param name="routingKey"></param>
        /// <param name="handler">消费处理</param>
        private void Pull<T>(string exchange, string queue, string routingKey, Action<T> handler) where T : class
        {
            var channel = GetModel(exchange, queue, routingKey);

            var result = channel.BasicGet(queue, false);
            if (ObjectExtension.IsNull(result))
                return;

            var msg = SerializeExtension.DeserializeUtf8(result.Body).FromJson<T>();
            try
            {
                handler(msg);
            }
            catch (Exception ex)
            {
                ex.GetInnestException().WriteToFile("队列接收消息", "RabbitMq");
            }
            finally
            {
                channel.BasicAck(result.DeliveryTag, false);
            }
        }

        #endregion

        #region RPC

        #region RPC客户端
        /// <summary>
        /// RPC客户端
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command"></param>
        /// <returns></returns>
        public string RpcClient<T>(T command) where T : class
        {
            var queueInfo = GetRabbitMqAttribute<T>();

            if (queueInfo.IsNull())
                throw new ArgumentException("RabbitMqAttribute");

            var body = command.ToJson();
            var exchange = queueInfo.ExchangeName;
            var queue = queueInfo.QueueName;
            var routingKey = queueInfo.ExchangeName;
            var isProperties = queueInfo.IsProperties;

            return RpcClient(exchange, queue, routingKey, body, isProperties);
        }

        /// <summary>
        /// RPC客户端
        /// </summary>
        /// <param name="exchange"></param>
        /// <param name="queue"></param>
        /// <param name="routingKey"></param>
        /// <param name="body"></param>
        /// <param name="isProperties"></param>
        /// <returns></returns>
        public string RpcClient(string exchange, string queue, string routingKey, string body, bool isProperties = false)
        {
            var channel = GetModel(exchange, queue, routingKey, isProperties);

            var consumer = new QueueingBasicConsumer(channel);
            channel.BasicConsume(queue, true, consumer);

            try
            {
                var correlationId = Guid.NewGuid().ToString();
                var basicProperties = channel.CreateBasicProperties();
                basicProperties.ReplyTo = queue;
                basicProperties.CorrelationId = correlationId;

                channel.BasicPublish(exchange, routingKey, basicProperties, body.SerializeUtf8());

                var sw = Stopwatch.StartNew();
                while (true)
                {
                    var ea = consumer.Queue.Dequeue();
                    if (ea.BasicProperties.CorrelationId == correlationId)
                    {
                        return SerializeExtension.DeserializeUtf8(ea.Body);
                    }

                    if (sw.ElapsedMilliseconds > 30000)
                        throw new Exception("等待响应超时");
                }
            }
            catch (Exception ex)
            {
                throw ex.GetInnestException();
            }
        }
        #endregion

        #region RPC服务端
        /// <summary>
        /// RPC服务端
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        public void RpcService<T>(Func<T, T> handler) where T : class
        {
            var queueInfo = GetRabbitMqAttribute<T>();
            if (queueInfo.IsNull())
                throw new ArgumentException("RabbitMqAttribute");

            var isDeadLetter = typeof(T) == typeof(DeadLetterQueue);

            RpcService(queueInfo.ExchangeName, queueInfo.QueueName, queueInfo.IsProperties, handler, isDeadLetter);
        }

        /// <summary>
        /// RPC服务端
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="exchange"></param>
        /// <param name="queue"></param>
        /// <param name="isProperties"></param>
        /// <param name="handler"></param>
        /// <param name="isDeadLetter"></param>
        public void RpcService<T>(string exchange, string queue, bool isProperties, Func<T, T> handler, bool isDeadLetter)
        {
            //队列声明
            var channel = GetModel(queue, isProperties);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body;
                var msgStr = SerializeExtension.DeserializeUtf8(body);
                var msg = msgStr.FromJson<T>();

                var props = ea.BasicProperties;
                var replyProps = channel.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;

                try
                {
                    msg = handler(msg);
                }
                catch (Exception ex)
                {
                    ex.GetInnestException().WriteToFile("队列接收消息", "RabbitMq");
                }
                finally
                {
                    channel.BasicPublish(exchange, props.ReplyTo, replyProps, msg.ToJson().SerializeUtf8());
                    channel.BasicAck(ea.DeliveryTag, false);
                }
            };
            channel.BasicConsume(queue, false, consumer);
        }
        #endregion

        #endregion

        #region 释放资源
        /// <summary>
        /// 执行与释放或重置非托管资源关联的应用程序定义的任务。
        /// </summary>
        public void Dispose()
        {
            foreach (var item in ModelDic)
            {
                item.Value.Dispose();
            }
            _conn.Dispose();
        }
        #endregion
    }
    #endregion
}
