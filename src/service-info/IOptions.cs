using PeanutButter.EasyArgs.Attributes;

namespace ServiceInfo;

public interface IOptions
{
    [ShortName('n')]
    public bool ByName { get; set; }

    [ShortName('p')]
    public bool ByPath { get; set; }
}