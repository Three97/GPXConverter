using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Text;
//using Dynastream;

public static class XMLParserHelper
{
    public static XmlNamespaceManager Manager;
    private static decimal dTryParse;
    private static int iTryParse;
    private static DateTime dtTryParse;
    private static XmlNode WorkNode;
    private static XmlAttribute WorkAttr;

    public static string SelectSingleTextString(XmlNode Node, string Selector, string Default = null)
    {
        WorkNode = Node.SelectSingleNode(Selector, Manager);
        if (WorkNode != null)
        {
            return WorkNode.InnerText;
        }
        else
        {
            return Default;
        }
    }
    public static string SelectSingleAttributeString(XmlNode Node, string Name, string Default = null)
    {
        WorkAttr = Node.Attributes[Name];
        if (WorkAttr != null)
        {
            return WorkAttr.Value;
        }
        else
        {
            return Default;
        }
    }

    public static int? SelectSingleTextInt(XmlNode Node, string Selector, int? Default = null)
    {
        WorkNode = Node.SelectSingleNode(Selector, Manager);
        if (WorkNode != null && int.TryParse(WorkNode.InnerText, out iTryParse))
        {
            return iTryParse;
        }
        else
        {
            return Default;
        }
    }
    public static int? SelectSingleAttributeInt(XmlNode Node, string Name, int? Default = null)
    {
        WorkAttr = Node.Attributes[Name];
        if (WorkAttr != null && int.TryParse(WorkAttr.Value, out iTryParse))
        {
            return iTryParse;
        }
        else
        {
            return Default;
        }
    }

    public static decimal? SelectSingleTextDecimal(XmlNode Node, string Selector, decimal? Default = null)
    {
        WorkNode = Node.SelectSingleNode(Selector, Manager);
        if (WorkNode != null && decimal.TryParse(WorkNode.InnerText, out dTryParse))
        {
            return dTryParse;
        }
        else
        {
            return Default;
        }
    }
    public static decimal? SelectSingleAttributeDecimal(XmlNode Node, string Name, decimal? Default = null)
    {
        WorkAttr = Node.Attributes[Name];
        if (WorkAttr != null && decimal.TryParse(WorkAttr.Value, out dTryParse))
        {
            return dTryParse;
        }
        else
        {
            return Default;
        }
    }

    public static DateTime? SelectSingleTextDateTime(XmlNode Node, string Selector, DateTime? Default = null)
    {
        WorkNode = Node.SelectSingleNode(Selector, Manager);
        if (WorkNode != null && DateTime.TryParse(WorkNode.InnerText, out dtTryParse))
        {
            return dtTryParse;
        }
        else
        {
            return Default;
        }
    }
    public static DateTime? SelectSingleAttributeDateTime(XmlNode Node, string Name, DateTime? Default = null)
    {
        WorkAttr = Node.Attributes[Name];
        if (WorkAttr != null && DateTime.TryParse(WorkAttr.Value, out dtTryParse))
        {
            return dtTryParse;
        }
        else
        {
            return Default;
        }
    }
}