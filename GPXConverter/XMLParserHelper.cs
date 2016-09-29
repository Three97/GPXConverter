using System;
using System.Xml;

public static class XMLParserHelper
{
    public static XmlNamespaceManager Manager;

    // TODO: Using these static fields is not thread-safe...
    private static decimal dTryParse;
    private static int iTryParse;
    private static DateTime dtTryParse;
    private static XmlNode workNode;
    private static XmlAttribute workAttr;

    public static string SelectSingleTextString(XmlNode node, string selector, string defaultValue = null)
    {
        workNode = node.SelectSingleNode(selector, Manager);
        return workNode?.InnerText ?? defaultValue;
    }

    public static string SelectSingleAttributeString(XmlNode node, string name, string defaultValue = null)
    {
        if (node?.Attributes == null)
        {
            return defaultValue;
        }

        workAttr = node.Attributes[name];
        return workAttr != null ? workAttr.Value : defaultValue;
    }

    public static int? SelectSingleTextInt(XmlNode node, string selector, int? defaultValue = null)
    {
        workNode = node.SelectSingleNode(selector, Manager);
        if (workNode != null && int.TryParse(workNode.InnerText, out iTryParse))
        {
            return iTryParse;
        }

        return defaultValue;
    }
    public static int? SelectSingleAttributeInt(XmlNode node, string name, int? defaultValue = null)
    {
        if (node?.Attributes == null)
        {
            return defaultValue;
        }

        workAttr = node.Attributes[name];
        if (workAttr != null && int.TryParse(workAttr.Value, out iTryParse))
        {
            return iTryParse;
        }

        return defaultValue;
    }

    public static decimal? SelectSingleTextDecimal(XmlNode node, string selector, decimal? defaultValue = null)
    {
        workNode = node.SelectSingleNode(selector, Manager);
        if (workNode != null && decimal.TryParse(workNode.InnerText, out dTryParse))
        {
            return dTryParse;
        }

        return defaultValue;
    }
    public static decimal? SelectSingleAttributeDecimal(XmlNode node, string name, decimal? defaultValue = null)
    {
        if (node?.Attributes == null)
        {
            return defaultValue;
        }

        workAttr = node.Attributes[name];
        if (workAttr != null && decimal.TryParse(workAttr.Value, out dTryParse))
        {
            return dTryParse;
        }

        return defaultValue;
    }

    public static DateTime? SelectSingleTextDateTime(XmlNode node, string selector, DateTime? defaultValue = null)
    {
        workNode = node.SelectSingleNode(selector, Manager);
        if (workNode != null && DateTime.TryParse(workNode.InnerText, out dtTryParse))
        {
            return dtTryParse;
        }

        return defaultValue;
    }
    public static DateTime? SelectSingleAttributeDateTime(XmlNode node, string name, DateTime? defaultValue = null)
    {
        if (node?.Attributes == null)
        {
            return defaultValue;
        }

        workAttr = node.Attributes[name];
        if (workAttr != null && DateTime.TryParse(workAttr.Value, out dtTryParse))
        {
            return dtTryParse;
        }

        return defaultValue;
    }
}