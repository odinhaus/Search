using Altus.Suffūz;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;
using Common.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Common.Licensing
{
    [System.Serializable]
    public class XmlFileLicense : ILicense
    {
        public XmlFileLicense(string licensePath)
        {
            ParseLicense(licensePath);
        }

        public XmlFileLicense()
        { }

        private void ParseLicense(string licensePath)
        {
            LoadXml(File.ReadAllText(licensePath));
        }

        public void LoadXml(string xml)
        {
            Xml = xml;
            Validate();
        }

        private void Validate()
        {
            string key;
            if (!TryGetToken<string>("//Key", out key))
                return;
            this.Key = key;

            string validationCode;
            if (!TryGetToken<string>("//ValidationCode", out validationCode))
                return;
            this.ValidationCode = validationCode;

            string issuedString;
            if (!TryGetToken<string>("//IssuedDate", out issuedString))
                return;
            this.IssuedDate = DateTime.Parse(issuedString);

            string expiredString;
            if (!TryGetToken<string>("//ExpirationDate", out expiredString))
                return;
            this.ExpirationDate = DateTime.Parse(expiredString);

            if (CurrentTime.Now > this.ExpirationDate
                || CurrentTime.Now < this.IssuedDate)
                return;

            XmlElement xRoot = (XmlElement)this.XmlLicenseDoc.SelectSingleNode("//License");
            if (xRoot == null)
                return;

            xRoot.RemoveChild(xRoot.SelectSingleNode("//ValidationCode"));

            validationCode = CreateValidationCodePriv(xRoot.InnerXml);
            if (validationCode != this.ValidationCode)
                return;

            this.IsValid = true;
        }

        private string CreateValidationCodePriv(string licenseBody)
        {
            MD5 hasher = MD5.Create();
            return hasher.ComputeHash(UnicodeEncoding.Unicode.GetBytes(licenseBody)).ToBase16(32, 8);
        }

        public static string CreateValidationCode(string licenseXml)
        {
            XmlDocument xDoc = new XmlDocument();
            xDoc.LoadXml(licenseXml);
            XmlElement xRoot = (XmlElement)xDoc.SelectSingleNode("//License");
            if (xRoot == null)
                throw new InvalidOperationException("The license text does not appears to be in the correct format");
            xRoot.RemoveChild(xRoot.SelectSingleNode("//ValidationCode"));

            MD5 hasher = MD5.Create();
            return hasher.ComputeHash(UnicodeEncoding.Unicode.GetBytes(xRoot.InnerXml)).ToBase16(32, 8);
        }

        public bool TryGetToken<T>(string tokenName, out T tokenValue)
        {
            try
            {
                return OnGetXmlToken<T>(tokenName, out tokenValue);
            }
            catch
            {
                tokenValue = default(T);
                return false;
            }
        }

        public IEnumerable<T> GetTokens<T>(string tokenName)
        {
            try
            {
                return OnGetTokens<T>(tokenName);
            }
            catch
            {
                return new T[0];
            }
        }

        protected virtual IEnumerable<T> OnGetTokens<T>(string tokenPath)
        {
            XmlNodeList xList = this.XmlLicenseDoc.SelectSingleNode("//License").SelectNodes(tokenPath);
            if (xList != null)
            {
                SerializationContext.Instance.TextEncoding = ASCIIEncoding.ASCII;

                var s = SerializationContext.Instance; //Altus.Instance.Shell.GetComponent<SerializationContext>();
                ISerializer serializer = null;
                if (s != null)
                {
                    serializer = s.GetSerializer(typeof(T), StandardFormats.XML);
                }

                if ((s == null
                    || serializer == null)
                    &&
                    (typeof(T).IsPrimitive
                    || typeof(T).Equals(typeof(string))
                    || typeof(T).Equals(typeof(DateTime))))
                {
                    serializer = new PrimitiveSerializer<T>();
                }

                foreach (XmlNode xNod in xList)
                {
                    XmlElement xElm = (XmlElement)xNod;
                    T tokenValue = default(T);
                    bool doYield = false;
                    try
                    {
                        if (typeof(T) == typeof(string))
                        {
                            tokenValue = (T)(object)xElm.InnerXml;
                        }
                        else if (serializer.IsScalar)
                        {
                            tokenValue = (T)serializer.Deserialize(ASCIIEncoding.ASCII.GetBytes(xElm.InnerXml), typeof(T));
                        }
                        else
                        {
                            tokenValue = (T)serializer.Deserialize(ASCIIEncoding.ASCII.GetBytes(xElm.OuterXml), typeof(T));
                        }
                        doYield = true;
                    }
                    catch { }
                    if (doYield)
                        yield return tokenValue;
                }
            }
        }

        [System.NonSerialized]
        XmlDocument _xDoc;
        protected XmlDocument XmlLicenseDoc
        {
            get
            {
                if (_xDoc == null)
                {
                    XmlDocument xDoc = new XmlDocument();
                    xDoc.LoadXml(Xml);
                    _xDoc = xDoc;
                }
                return _xDoc;
            }
        }

        protected bool OnGetXmlToken<T>(string tokenPath, out T tokenValue)
        {
            XmlElement xElm = (XmlElement)this.XmlLicenseDoc.SelectSingleNode("//License").SelectSingleNode(tokenPath);

            if (typeof(T) == typeof(string))
            {
                tokenValue = (T)(object)xElm.InnerXml;
                return true;
            }

            if (xElm == null)
            {
                tokenValue = default(T);
                return false;
            }
            else
            {
                SerializationContext.Instance.TextEncoding = ASCIIEncoding.ASCII;

                var s = SerializationContext.Instance; //Altus.Instance.Shell.GetComponent<SerializationContext>();
                ISerializer serializer = null;
                if (s != null)
                {
                    serializer = s.GetSerializer(typeof(T), StandardFormats.XML);
                }

                if ((s == null
                    || serializer == null)
                    &&
                    (typeof(T).IsPrimitive
                    || typeof(T).Equals(typeof(string))
                    || typeof(T).Equals(typeof(DateTime))))
                {
                    serializer = new PrimitiveSerializer<T>();
                }

                if (serializer.IsScalar)
                {
                    tokenValue = (T)serializer.Deserialize(ASCIIEncoding.ASCII.GetBytes(xElm.InnerXml), typeof(T));
                }
                else
                {
                    tokenValue = (T)serializer.Deserialize(ASCIIEncoding.ASCII.GetBytes(xElm.OuterXml), typeof(T));
                }

                return true;
            }
        }

        public string Key
        {
            get;
            private set;
        }


        public string ValidationCode
        {
            get;
            private set;
        }

        public DateTime IssuedDate
        {
            get;
            private set;
        }

        public DateTime ExpirationDate
        {
            get;
            private set;
        }

        public bool IsValid
        {
            get;
            private set;
        }

        public string Xml
        {
            get;
            private set;
        }
    }
}
