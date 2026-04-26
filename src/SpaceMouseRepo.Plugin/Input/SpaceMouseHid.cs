using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BepInEx.Logging;
using HidLibrary;
using SpaceMouseRepo.Core.Input;

namespace SpaceMouseRepo.Input;

public sealed class SpaceMouseHid : IDisposable
{
    private const int VendorId = 0x256F;
    private static readonly int[] DefaultProductIds = { 0xC62E, 0xC62F, 0xC652 };

    private readonly ManualLogSource _log;
    private readonly SpaceMouseReportParser _parser;
    private readonly HidDevice? _device;
    private readonly Thread? _readThread;
    private volatile bool _running;

    public SpaceMouseHid(ManualLogSource log, IEnumerable<int> extraProductIds, float translationDeadzone, float rotationDeadzone)
    {
        _log = log;
        _parser = new SpaceMouseReportParser(translationDeadzone, rotationDeadzone);

        var allowed = new HashSet<int>(DefaultProductIds);
        foreach (var p in extraProductIds) allowed.Add(p);

        var found = HidDevices.Enumerate(VendorId).ToList();
        if (found.Count == 0)
        {
            _log.LogWarning($"No 3Dconnexion HID devices found (vendor 0x{VendorId:X4}). Plugin will be inactive.");
            return;
        }

        // Diagnostic: log every 3Dconnexion device, even non-matching ones — helps users add new ProductIds via config.
        foreach (var dev in found)
            _log.LogInfo($"Found 3Dconnexion HID device: VID=0x{dev.Attributes.VendorId:X4} PID=0x{dev.Attributes.ProductId:X4} {dev.Description}");

        _device = found.FirstOrDefault(d => allowed.Contains(d.Attributes.ProductId));
        if (_device == null)
        {
            _log.LogWarning("3Dconnexion device(s) found but none matched known SpaceMouse product IDs. Add the PID to ExtraProductIds in config.");
            return;
        }

        _device.OpenDevice();
        _running = true;
        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "SpaceMouseHidRead" };
        _readThread.Start();
        _log.LogInfo($"Opened SpaceMouse PID=0x{_device.Attributes.ProductId:X4}");
    }

    public SpaceMouseState State => _parser.State;
    public bool IsActive => _device != null && _device.IsOpen && _running;

    private void ReadLoop()
    {
        while (_running && _device != null && _device.IsConnected)
        {
            // ReadReport returns HidReport; status is the ReadStatus property (not Status).
            var report = _device.ReadReport(100);
            if (report.ReadStatus == HidDeviceData.ReadStatus.Success && report.Data.Length > 0)
            {
                // HidLibrary strips the leading report ID into ReportId; reinsert for the parser.
                var bytes = new byte[report.Data.Length + 1];
                bytes[0] = report.ReportId;
                Buffer.BlockCopy(report.Data, 0, bytes, 1, report.Data.Length);
                _parser.Feed(bytes);
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _readThread?.Join(500);
        if (_device != null && _device.IsOpen) _device.CloseDevice();
    }
}
