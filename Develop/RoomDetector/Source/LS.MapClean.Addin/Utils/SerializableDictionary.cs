using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace LS.MapClean.Addin.Utils
{
    [Serializable]
    [XmlRoot("dictionary")]
    public class SerializableDictionary<TKey, TValue>
        : Dictionary<TKey, TValue>, IXmlSerializable
    {
        readonly XmlSerializer KeySerializer = new XmlSerializer(typeof(TKey));
        readonly XmlSerializer ValueSerializer = new XmlSerializer(typeof(TValue));

        public SerializableDictionary()
            : base()
        {

        }

        public SerializableDictionary(IDictionary<TKey, TValue> fromDict)
            : base(fromDict)
        {

        }

        protected SerializableDictionary(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            throw new NotImplementedException();
        }

        #region IXmlSerializable Members

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(System.Xml.XmlReader reader)
        {
            bool wasEmpty = reader.IsEmptyElement;
            reader.Read();

            if (wasEmpty || reader.NodeType == XmlNodeType.EndElement)
                return;

            if (reader.LocalName == "dictionary")
            {
                // Skip over root element
                reader.ReadStartElement("dictionary");
                reader.MoveToContent();
            }

            while (reader.NodeType == System.Xml.XmlNodeType.Element && reader.LocalName == "item")
            {
                reader.ReadStartElement("item");

                reader.ReadStartElement("key");
                var key = (TKey)KeySerializer.Deserialize(reader);
                reader.ReadEndElement();

                reader.ReadStartElement("value");
                var value = (TValue)ValueSerializer.Deserialize(reader);
                reader.ReadEndElement();

                this.Add(key, value);

                reader.ReadEndElement();
                reader.MoveToContent();
            }
        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {
            // Root element
            writer.WriteStartElement("dictionary");

            foreach (TKey key in this.Keys)
            {
                writer.WriteStartElement("item");

                writer.WriteStartElement("key");
                KeySerializer.Serialize(writer, key);
                writer.WriteEndElement();

                writer.WriteStartElement("value");
                TValue value = this[key];
                ValueSerializer.Serialize(writer, value);
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            // End Root element
            writer.WriteEndElement();
        }

        #endregion
    }
}
