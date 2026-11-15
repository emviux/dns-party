using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

public class Program
{
    public static void Main()
    {
        var serializer = new JavaScriptSerializer();
        var obj = serializer.DeserializeObject("{"arr": [1, 2, 3]}");
        var dict = (Dictionary<string, object>)obj;
        Console.WriteLine(dict["arr"].GetType().Name);
    }
}
