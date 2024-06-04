namespace NetEscapades.EnumGenerators;

public readonly record struct EnumToGenerate
{
    public readonly string ExtensionName; //👈 New field
    public readonly string Name;
    public readonly EquatableArray<string> Values;

    public EnumToGenerate(string name, string extensionName, List<string> values )
    {
        Name = name;
        Values = new(values.ToArray());
        ExtensionName = extensionName;
    }
}