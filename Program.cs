
using MTConnect.Devices;

var _paths = new List<string>()
{
    "Device[Name=device1]/Availability",
    "Device[Name=device1]/Controller[Name=Test]/Path/Execution",
    "Device[Name=device1]/Controller[Name=Test]/Path/System",
    "Device[Name=device1]/Axes/Linear[Name=X]/Position[SubType=Actual]"
};

var _devices = new Dictionary<string, Device>();

foreach (var path in _paths)
{
    Console.WriteLine($"Processing Path: {path}");
    
    var parts = new Parts(path);

    Device device = null;
    IComponent nextComponent = null;
    
    for(int i = 0; i < parts.Count; i++)
    {
        var part = parts[i];
        
        Console.WriteLine($"\tProcessing Part: {part.Name}");
        
        if(i == 0) // Device expected
        {
            part.Attributes.TryGetValue("Name", out var deviceName);
            var deviceExists = _devices.TryGetValue(deviceName, out device);
            if (!deviceExists)
            {
                device = new Device();
                _devices[deviceName] = device;
                foreach (var attribute in part.Attributes)
                {
                    Console.WriteLine($"\t\tProcessing Attribute: {attribute.Key}={attribute.Value}");
                    var property = device.GetType().GetProperty(attribute.Key);
                    property.SetValue(device, attribute.Value);
                }
            }
            nextComponent = device;
        }
        else if (i == parts.Count - 1) // DataItem expected
        {
            var dataItem = nextComponent.GetDataItemByType($"{part.Name}DataItem", SearchType.Child);
            if (dataItem is null)
            {
                dataItem = DataItem.Create($"{part.Name}DataItem");
                foreach (var attribute in part.Attributes)
                {
                    Console.WriteLine($"\t\tProcessing Attribute: {attribute.Key}={attribute.Value}");
                    var property = dataItem.GetType().GetProperty(attribute.Key);
                    property.SetValue(dataItem, attribute.Value);
                }
                
                try
                {
                    ((Component)nextComponent).AddDataItem(dataItem);
                }
                catch
                {
                    ((Device)nextComponent).AddDataItem(dataItem);
                }
            }
        }
        else // Component expected
        {
            var childComponent = nextComponent.GetComponent($"{part.Name}Component", searchType: SearchType.Child);
            if (childComponent is null)
            {
                childComponent = Component.Create($"{part.Name}Component");
            }
            
            foreach (var attribute in part.Attributes)
            {
                Console.WriteLine($"\t\tProcessing Attribute: {attribute.Key}={attribute.Value}");
                var property = childComponent.GetType().GetProperty(attribute.Key);
                property.SetValue(childComponent, attribute.Value);
            }

            try
            {
                ((Component)nextComponent).AddComponent(childComponent);
                nextComponent = childComponent;
            }
            catch
            {
                ((Device)nextComponent).AddComponent(childComponent);
                nextComponent = childComponent;
            }
        }
    }

    try
    {
        var xmlBytes = MTConnect.Devices.Xml.XmlDevice.ToXml(device);
        var xmlString = System.Text.Encoding.UTF8.GetString(xmlBytes);
    
        Console.WriteLine($"\tDevice XML: {xmlString}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"\tDevice XML: FAILED!");
    }
}


Console.ReadKey();


public class Part
{
    public string Name { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
}

public class Parts : List<Part>
{
    public Parts() : base() { }

    public Parts(IEnumerable<Part> collection) : base(collection) { }

    // Find first part by name
    public Part FindByName(string name)
    {
        return this.FirstOrDefault(p => p.Name == name);
    }

    // Find all parts by name
    public Parts FindAllByName(string name)
    {
        return new Parts(this.Where(p => p.Name == name));
    }

    // Find parts with specific attribute
    public Parts FindByAttribute(string key, string value)
    {
        return new Parts(this.Where(p => 
            p.Attributes != null && 
            p.Attributes.TryGetValue(key, out var attributeValue) && 
            attributeValue == value));
    }

    // Parse device path directly in constructor
    public Parts(string devicePath) : base()
    {
        // Split the path into segments
        var segments = devicePath.Split('/');
        
        // Parse each segment into a Part
        foreach (var segment in segments)
        {
            Add(ParsePartSegment(segment));
        }
    }

    // Keep the parsing method from previous example
    private Part ParsePartSegment(string segment)
    {
        int bracketStart = segment.IndexOf('[');
        
        Part part = new Part();
        
        if (bracketStart == -1)
        {
            part.Name = segment;
        }
        else
        {
            part.Name = segment.Substring(0, bracketStart);
            
            string attributeString = segment.Substring(bracketStart + 1, 
                segment.Length - bracketStart - 2);
            
            part.Attributes = attributeString.Split(',')
                .Select(attr => attr.Split('='))
                .ToDictionary(
                    attr => attr[0].Trim(), 
                    attr => attr[1].Trim()
                );
        }
        
        return part;
    }

    // Convenience method to get parts at a specific index with null check
    public Part GetPartOrDefault(int index)
    {
        return index >= 0 && index < Count ? this[index] : null;
    }

    // Pretty print all parts
    public void PrintParts()
    {
        foreach (var part in this)
        {
            System.Console.WriteLine($"Name: {part.Name}");
            if (part.Attributes != null)
            {
                foreach (var attr in part.Attributes)
                {
                    System.Console.WriteLine($"  {attr.Key}: {attr.Value}");
                }
            }
        }
    }
}