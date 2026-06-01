using System;
using System.Collections.Generic;
using System.Linq;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;

namespace LaptopForensics.Core.Orchestration;

public class ScannerFactory
{
    private readonly IScanProgressObserver _observer;

    public ScannerFactory(IScanProgressObserver observer)
    {
        _observer = observer;
    }

    public IEnumerable<IScanModule> Create(ScanMode mode, IEnumerable<IScanModule> all, string? singleModuleName = null)
    {
        IEnumerable<IScanModule> filtered;

        if (mode == ScanMode.Single)
        {
            if (string.IsNullOrWhiteSpace(singleModuleName))
            {
                throw new ArgumentException("singleModuleName must be provided for Single scan mode.");
            }
            
            filtered = all.Where(m => m.ModuleName.Equals(singleModuleName, StringComparison.OrdinalIgnoreCase));
        }
        else if (mode == ScanMode.Quick)
        {
            filtered = all.Where(m => m.ApplicableModes.HasFlag(ScanMode.Quick));
        }
        else
        {
            // Full or Default
            filtered = all;
        }

        // Wrap each in TimedScanModule
        return filtered.Select(m => new TimedScanModule(m, _observer)).ToList();
    }
}
