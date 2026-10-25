using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public readonly struct LaunchpadDevice
{
    internal LaunchpadDevice(uint id, string name, string deviceKey) { Id = id; Name = name; DeviceKey = deviceKey; }
    public uint Id { get; }
    public string Name { get; }
    /// <summary>Windows device interface identity; port-specific fallback if unavailable.</summary>
    public string DeviceKey { get; }
}

public readonly struct LaunchpadPadPress
{
    internal LaunchpadPadPress(LaunchpadDevice device, int note, bool isControlChange, bool isPressed)
    {
        Device = device;
        Note = note;
        IsControlChange = isControlChange;
        IsPressed = isPressed;
    }

    public LaunchpadDevice Device { get; }
    public int Note { get; }
    public bool IsControlChange { get; }
    public bool IsPressed { get; }
}

/// <summary>Receives button presses and controls LEDs for Launchpad X and MK2 on Windows.</summary>
public sealed class LaunchpadMidiInput : MonoBehaviour
{
    private const uint MIM_DATA = 0x3C3;
    private const uint CALLBACK_FUNCTION = 0x00030000;
    private const byte Blue = 45;
    private const byte Red = 5;

    private sealed class Connection
    {
        public LaunchpadDevice Device;
        public IntPtr InputHandle;
        public IntPtr OutputHandle;
        public uint? OutputId;
        public string OutputKey;
    }

    private readonly List<Connection> connections = new();
    private readonly ConcurrentDictionary<IntPtr, Connection> inputLookup = new();
    private readonly ConcurrentQueue<LaunchpadPadPress> pressedPads = new();
    private MidiInProc midiCallback;
    private float nextDeviceRefreshAt;
    private const float DeviceRefreshInterval = 0.5f;

    public bool TryConsumeButtonPress(out LaunchpadDevice device)
    {
        while (TryConsumePadPress(out LaunchpadPadPress press))
        {
            if (!press.IsPressed)
                continue;
            device = press.Device;
            return true;
        }

        device = default;
        return false;
    }

    public bool TryConsumePadPress(out LaunchpadPadPress press)
    {
        while (pressedPads.TryDequeue(out press))
            if (IsConnected(press.Device)) return true;
        return false;
    }

    public bool IsConnected(LaunchpadDevice device) => connections.Exists(item => item.Device.DeviceKey == device.DeviceKey);
    public bool IsDeviceConnected(string deviceKey) => connections.Exists(item => item.Device.DeviceKey == deviceKey);

    public void SetPadLed(LaunchpadDevice device, int note, byte paletteColour, bool pulsing = false)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        Connection connection = connections.Find(item => item.Device.DeviceKey == device.DeviceKey);
        if (connection != null && connection.OutputHandle != IntPtr.Zero)
            SendShortMessage(connection.OutputHandle, pulsing ? (byte)0x92 : (byte)0x90, (byte)Mathf.Clamp(note, 0, 127), paletteColour);
#endif
    }

    public void SetAllLeds(LaunchpadDevice device, bool playerB)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        Connection connection = connections.Find(item => item.Device.DeviceKey == device.DeviceKey);
        if (connection == null || connection.OutputHandle == IntPtr.Zero)
            return;

        byte colour = playerB ? Red : Blue;
        // Note messages cover grid pads; CC messages cover top/side controls.
        // Launchpad X must be in its lighting/programmer layout for external LED control.
        for (byte control = 0; control < 127; control++)
        {
            SendShortMessage(connection.OutputHandle, 0x90, control, colour);
            SendShortMessage(connection.OutputHandle, 0xB0, control, colour);
        }
#endif
    }

    public void ClearAllLeds()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        foreach (Connection connection in connections)
        {
            if (connection.OutputHandle == IntPtr.Zero)
                continue;

            for (byte control = 0; control < 127; control++)
            {
                SendShortMessage(connection.OutputHandle, 0x90, control, 0);
                SendShortMessage(connection.OutputHandle, 0xB0, control, 0);
            }
        }
#endif
    }

    private void OnEnable()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        midiCallback = OnMidiMessage;
        RefreshConnectedDevices();
#endif
    }

    private void Update()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (Time.unscaledTime >= nextDeviceRefreshAt)
            RefreshConnectedDevices();
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void RefreshConnectedDevices()
    {
        nextDeviceRefreshAt = Time.unscaledTime + DeviceRefreshInterval;
        List<LaunchpadPortRouting.Port> availableInputs = EnumeratePorts(true);
        List<LaunchpadPortRouting.Port> availableOutputs = EnumeratePorts(false);

        for (int index = connections.Count - 1; index >= 0; index--)
        {
            Connection connection = connections[index];
            if (!availableInputs.Exists(port => port.Id == connection.Device.Id && port.DeviceKey == connection.Device.DeviceKey) ||
                (connection.OutputId.HasValue && !availableOutputs.Exists(port => port.Id == connection.OutputId.Value &&
                    connection.OutputKey == port.DeviceKey)))
                CloseConnection(connection);
        }

        foreach (LaunchpadPortRouting.Port input in availableInputs)
            if (!connections.Exists(connection => connection.Device.DeviceKey == input.DeviceKey))
                OpenConnection(input, availableInputs, availableOutputs);
    }

    private void OpenConnection(LaunchpadPortRouting.Port input, List<LaunchpadPortRouting.Port> inputs, List<LaunchpadPortRouting.Port> outputs)
    {
        if (midiInOpen(out IntPtr inputHandle, input.Id, midiCallback, IntPtr.Zero, CALLBACK_FUNCTION) != 0)
            return;

        var claimed = new HashSet<uint>();
        foreach (Connection existing in connections)
            if (existing.OutputId.HasValue) claimed.Add(existing.OutputId.Value);
        LaunchpadPortRouting.Port output = LaunchpadPortRouting.FindOutput(input, inputs, outputs, claimed);
        IntPtr outputHandle = IntPtr.Zero;
        if (output != null && midiOutOpen(out outputHandle, output.Id, IntPtr.Zero, IntPtr.Zero, 0) != 0)
            outputHandle = IntPtr.Zero;

        Connection connection = new Connection
        {
            Device = new LaunchpadDevice(input.Id, input.Name, input.DeviceKey),
            InputHandle = inputHandle,
            OutputHandle = outputHandle,
            OutputId = outputHandle == IntPtr.Zero ? (uint?)null : output.Id,
            OutputKey = output?.DeviceKey
        };
        connections.Add(connection);
        inputLookup.TryAdd(inputHandle, connection);
        if (midiInStart(inputHandle) != 0) { CloseConnection(connection); return; }
        Debug.Log($"Launchpad input {input.Id}: {input.Name}; output: {(connection.OutputId.HasValue ? output.Id + ": " + output.Name : "unavailable")}; key: {input.DeviceKey}");
        if (outputHandle == IntPtr.Zero)
            Debug.LogWarning($"No unambiguous/available LED output for {input.Name}. Check Windows MIDI ports and close other MIDI applications.");
    }

    private void CloseConnection(Connection connection)
    {
        inputLookup.TryRemove(connection.InputHandle, out _);
        if (connection.OutputHandle != IntPtr.Zero)
            ClearLeds(connection.OutputHandle);
        midiInStop(connection.InputHandle);
        midiInReset(connection.InputHandle);
        midiInClose(connection.InputHandle);
        if (connection.OutputHandle != IntPtr.Zero)
            midiOutClose(connection.OutputHandle);
        connections.Remove(connection);
        Debug.Log($"Launchpad disconnected: {connection.Device.Name}");
    }

    private void OnDisable()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        foreach (Connection connection in connections)
        {
            if (connection.OutputHandle != IntPtr.Zero)
                ClearLeds(connection.OutputHandle);
            midiInStop(connection.InputHandle); midiInReset(connection.InputHandle); midiInClose(connection.InputHandle);
            if (connection.OutputHandle != IntPtr.Zero) midiOutClose(connection.OutputHandle);
        }
        connections.Clear();
        inputLookup.Clear();
        while (pressedPads.TryDequeue(out _)) { }
#endif
    }

    private static List<LaunchpadPortRouting.Port> EnumeratePorts(bool input)
    {
        var ports = new List<LaunchpadPortRouting.Port>();
        uint count = input ? midiInGetNumDevs() : midiOutGetNumDevs();
        for (uint id = 0; id < count; id++)
        {
            string name;
            if (input)
            {
                if (midiInGetDevCaps((UIntPtr)id, out MidiInCaps caps, (uint)Marshal.SizeOf<MidiInCaps>()) != 0) continue;
                name = caps.name;
            }
            else
            {
                if (midiOutGetDevCaps((UIntPtr)id, out MidiOutCaps caps, (uint)Marshal.SizeOf<MidiOutCaps>()) != 0) continue;
                name = caps.name;
            }
            if (LaunchpadPortRouting.ModelOf(name).Length == 0) continue;
            ports.Add(new LaunchpadPortRouting.Port { Id = id, Name = name, Interface = QueryInterface(id, input) });
        }
        return LaunchpadPortRouting.SelectPorts(ports);
    }

    // mmddk.h: DRV_RESERVED (0x800) + 13 for size, + 12 for UTF-16 device interface.
    private static string QueryInterface(uint id, bool input)
    {
        IntPtr sizePointer = Marshal.AllocHGlobal(sizeof(int));
        IntPtr buffer = IntPtr.Zero;
        try
        {
            Marshal.WriteInt32(sizePointer, 0);
            uint result = input ? midiInMessage((IntPtr)id, 0x80D, sizePointer, IntPtr.Zero) : midiOutMessage((IntPtr)id, 0x80D, sizePointer, IntPtr.Zero);
            int size = Marshal.ReadInt32(sizePointer);
            if (result != 0 || size < 2 || size > 65536 || size % 2 != 0) return "";
            buffer = Marshal.AllocHGlobal(size);
            for (int offset = 0; offset < size; offset += 2) Marshal.WriteInt16(buffer, offset, 0);
            result = input ? midiInMessage((IntPtr)id, 0x80C, buffer, (IntPtr)size) : midiOutMessage((IntPtr)id, 0x80C, buffer, (IntPtr)size);
            return result == 0 ? (Marshal.PtrToStringUni(buffer, size / 2) ?? "").TrimEnd('\0') : "";
        }
        finally
        {
            Marshal.FreeHGlobal(sizePointer);
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        }
    }

    private void OnMidiMessage(IntPtr handle, uint message, IntPtr instance, uint packedMessage, uint timestamp)
    {
        if (message != MIM_DATA || !inputLookup.TryGetValue(handle, out Connection connection)) return;
        int status = (int)(packedMessage & 0xF0);
        int note = (int)((packedMessage >> 8) & 0x7F);
        int value = (int)((packedMessage >> 16) & 0x7F);
        bool isNoteMessage = status == 0x80 || status == 0x90;
        bool isControlChange = status == 0xB0;
        if (!isNoteMessage && !isControlChange)
            return;

        bool isPressed = status != 0x80 && value > 0;
        pressedPads.Enqueue(new LaunchpadPadPress(connection.Device, note, isControlChange, isPressed));
    }

    private static void SendShortMessage(IntPtr output, byte status, byte data1, byte data2) => midiOutShortMsg(output, (uint)(status | data1 << 8 | data2 << 16));

    private static void ClearLeds(IntPtr output)
    {
        for (byte control = 0; control < 127; control++)
        {
            SendShortMessage(output, 0x90, control, 0);
            SendShortMessage(output, 0xB0, control, 0);
        }
    }

    private delegate void MidiInProc(IntPtr handle, uint message, IntPtr instance, uint param1, uint param2);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private struct MidiInCaps { public ushort manufacturerId, productId; public uint driverVersion; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name; public uint support; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private struct MidiOutCaps { public ushort manufacturerId, productId; public uint driverVersion; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name; public ushort technology, voices, notes, channelMask; public uint support; }
    [DllImport("winmm.dll")] private static extern uint midiInGetNumDevs();
    [DllImport("winmm.dll")] private static extern uint midiInMessage(IntPtr deviceId, uint message, IntPtr param1, IntPtr param2);
    [DllImport("winmm.dll")] private static extern uint midiOutMessage(IntPtr deviceId, uint message, IntPtr param1, IntPtr param2);
    [DllImport("winmm.dll")] private static extern uint midiOutGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Auto)] private static extern uint midiInGetDevCaps(UIntPtr id, out MidiInCaps caps, uint size);
    [DllImport("winmm.dll", CharSet = CharSet.Auto)] private static extern uint midiOutGetDevCaps(UIntPtr id, out MidiOutCaps caps, uint size);
    [DllImport("winmm.dll")] private static extern uint midiInOpen(out IntPtr handle, uint id, MidiInProc callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint midiOutOpen(out IntPtr handle, uint id, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint midiInStart(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint midiInStop(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint midiInReset(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint midiInClose(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint midiOutClose(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint midiOutShortMsg(IntPtr handle, uint message);
#endif
}
