using Altus.Suffūz.Serialization.Binary;
using Data.Core;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suffuz.Data
{
    [Model("AppToken")]
    [TypeConverter(typeof(ModelTypeConverter))]
    public interface IAppToken : IModel<long>
    {
        [BinarySerializable(10)]
        [Searchable]
        string Token { get; set; }
        [BinarySerializable(11)]
        [Searchable]
        string TeamName { get; set; }
        [BinarySerializable(12)]
        [Searchable]
        string TeamId { get; set; }
        [BinarySerializable(13)]
        string Scope { get; set; }
        [BinarySerializable(14)]
        IWebHook IncomingWebHook { get; set; }
        [BinarySerializable(15)]
        IBotUserToken BotUserToken { get; set; }
        [BinarySerializable(16)]
        string UserId { get; set; }
    }
    [Model("WebHook")]
    [TypeConverter(typeof(ModelTypeConverter))]
    public interface IWebHook : ISubModel
    {
        [BinarySerializable(10)]
        string Url { get; set; }
        [BinarySerializable(11)]
        string Channel { get; set; }
        [BinarySerializable(12)]
        string ConfigurationUrl { get; set; }
    }
    [Model("BotUserToken")]
    [TypeConverter(typeof(ModelTypeConverter))]
    public interface IBotUserToken : ISubModel
    {
        [BinarySerializable(10)]
        string Token { get; set; }
        [BinarySerializable(11)]
        string UserId { get; set; }
    }
}
