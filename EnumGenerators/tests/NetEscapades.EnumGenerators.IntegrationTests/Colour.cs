using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetEscapades.EnumGenerators.IntegrationTests;


[EnumExtensions]
[Flags]
public enum Colour
{
    Red = 1,
    Blue = 2,
    Green = 4,
}
