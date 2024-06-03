//HintName: EnumExtensions.MyTestEnums.Colour.g.cs

namespace NetEscapades.EnumGenerators
{
    public static partial class EnumExtensions
    {
            public static string ToStringFast(this MyTestEnums.Colour value)
                => value switch
                {
            MyTestEnums.Colour.Red => nameof(MyTestEnums.Colour.Red),
            MyTestEnums.Colour.Blue => nameof(MyTestEnums.Colour.Blue),
                _ => value.ToString(),
            };

    }
}