using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using PeanutButter.EasyArgs;
using PeanutButter.Utils;
using PeanutButter.WindowsServiceManagement;
using ServiceInfo;

var opts = args.ParseTo<IOptions>(out var uncollected);
if (!opts.ByName && !opts.ByPath)
{
    opts.ByPath = opts.ByName = true;
}

var allServices = QueryAll();

foreach (var item in uncollected)
{
    var found = false;
    if (opts.ByName)
    {
        var nameMatch = allServices.FirstOrDefault(
            o => (o.ServiceName ?? "").Equals(item, StringComparison.OrdinalIgnoreCase)
        );
        if (nameMatch is not null)
        {
            found = true;
            Dump(nameMatch);
            continue;
        }

        var descMatch =
            allServices.FirstOrDefault(
                o => (o.DisplayName ?? "").Equals(
                    item,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        if (descMatch is not null)
        {
            found = true;
            Dump(descMatch);
        }
    }

    if (opts.ByPath)
    {
        var path = item.ResolvePath();
        var match = allServices.FirstOrDefault(
            o => (o.ServiceExe ?? "").Equals(path, StringComparison.OrdinalIgnoreCase)
        );
        if (match is not null)
        {
            found = true;
            Dump(match);
        }
    }

    if (!found)
    {
        DumpSuggestions(item, opts);
    }
}

void Dump(IWindowsServiceUtil util)
{
    Console.WriteLine($"{util.DisplayName}  ({util.ServiceName})");
    Console.WriteLine($"  executable: {util.ServiceExe}");
    Console.WriteLine($"  pid:        {util.ServicePID}");
    Console.WriteLine($"  state:      {util.State}");
}

void DumpSuggestions(string needle, IOptions options)
{
    var anySuggestions = false;
    if (options.ByName)
    {
        var nameSuggestions = allServices.Where(
            o => (o.ServiceName ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                (o.DisplayName ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                (o.ServiceExe ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase)
        ).ToArray();
        anySuggestions = nameSuggestions.Any();
        DumpSuggestionsFor("name", needle, nameSuggestions);
    }

    if (options.ByPath)
    {
        var pathSuggestions = allServices.Where(
            o => (o.ServiceExe ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase)
        ).ToArray();
        anySuggestions = anySuggestions || pathSuggestions.Any();
        DumpSuggestionsFor("path", needle, pathSuggestions);
    }

    if (!anySuggestions)
    {
        Console.WriteLine($"No matches found for '{needle}'");
    }
}

void DumpSuggestionsFor(string context, string needle, WindowsServiceUtil[] utils)
{
    if (utils.Length == 0)
    {
        return;
    }

    Console.WriteLine($"Unable to find an exact match for {context} '{needle}'");
    Console.WriteLine($"  did you mean{(utils.Length > 1 ? "one of" : "")}:");
    var needsNewline = false;
    foreach (var suggestion in utils)
    {
        if (needsNewline)
        {
            Console.WriteLine("");
        }

        needsNewline = true;

        Dump(suggestion);
    }
}

WindowsServiceUtil[] QueryAll()
{
    var collection = new ConcurrentBag<WindowsServiceUtil>();
    var serviceControlInterface = new ServiceControlInterface();
    var threads = new List<Thread>();
    var stopwatch = new Stopwatch();
    Console.Out.Write("Enumerating services...");
    stopwatch.Start();
    var allServiceNames = serviceControlInterface.ListAllServices()
        .ToArray();
    foreach (var name in allServiceNames)
    {
        var t = new Thread(
            () =>
            {
                collection.Add(
                    new WindowsServiceUtil(name)
                );
            }
        );
        t.Start();
        threads.Add(t);
    }

    foreach (var t in threads)
    {
        t.Join();
    }

    stopwatch.Stop();
    Console.WriteLine($" ok! ({collection.Count} services in {stopwatch.Elapsed})");
    return collection.OrderBy(o => o.DisplayName).ToArray();
}