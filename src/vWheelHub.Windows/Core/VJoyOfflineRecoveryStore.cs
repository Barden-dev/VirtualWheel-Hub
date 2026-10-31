using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SimRacingHub.Core;

// Only the explicit recovery command can use this adapter. An unavailable API
// alone never proves that a device is safe to change.
internal sealed class VJoyOfflineRecoveryStore : IVJoyConfigurationStore
{
    private readonly IVJoyConfigurationStore _native;
    private readonly VJoyChangeRecord _record;
    private readonly VJoyConfigurationSnapshot _original;

    internal VJoyOfflineRecoveryStore(IVJoyConfigurationStore native,VJoyChangeRecord record,VJoyConfigurationSnapshot original)
    {
        if(record.ApprovedAt==null || record.Expected==null || record.DeviceId!=original.DeviceId || record.BeforeHash!=original.Fingerprint())
            throw new InvalidDataException("No exact approved recovery snapshot.");
        _native=native;_record=record;_original=original;
    }

    internal static IVJoyConfigurationStore Create(VJoyChangeRecord record,VJoyConfigurationSnapshot original)
    {
        if(VJoyWrapper.IsDriverEnabled())return new WindowsVJoyConfigurationStore();
        VJoyDriverIdentity Driver()
        {
            if(VJoyWrapper.IsDriverEnabled() || !HasStoppedCode10Instance())throw new IOException("Offline recovery requires a stopped vJoy instance with Code 10.");
            var current=WindowsVJoyConfigurationStore.ReadDriverBinary(original.Driver.Version);
            if(current!=original.Driver)throw new IOException("The installed driver identity changed; offline recovery denied.");
            return current;
        }
        Driver();
        var native=new WindowsVJoyConfigurationStore(Driver,_=>{Driver();return VJoyWrapper.VJD_STAT_MISS;},RegistryHive.LocalMachine,@"SYSTEM\CurrentControlSet\services\vjoy\Parameters");
        return new VJoyOfflineRecoveryStore(native,record,original);
    }

    public VJoyConfigurationSnapshot Read(uint id)
    {
        if(id!=_record.DeviceId)throw new InvalidOperationException("Offline recovery is limited to its journalled device.");
        return _native.Read(id);
    }
    public int GetStatus(uint id)=>id==_record.DeviceId?_native.GetStatus(id):VJoyWrapper.VJD_STAT_UNKN;
    public bool TryReplace(VJoyConfigurationSnapshot before,VJoyConfigurationSnapshot after)=>false;
    public bool TryReplace(VJoyConfigurationSnapshot before,VJoyConfigurationSnapshot after,string id,bool restoring=false)
    {
        if(!restoring || id!=_record.Id || before.Fingerprint()!=_record.Expected!.Fingerprint() || after.Fingerprint()!=_original.Fingerprint() ||
            !_native.HasWriteReceipt(id,_original.Fingerprint(),before.Fingerprint()) ||
            _native.HasWriteReceipt(id,before.Fingerprint(),after.Fingerprint(),true))return false;
        return _native.TryReplace(before,after,id,true);
    }
    public bool HasWriteReceipt(string id,string before,string after,bool restoring=false)=>id==_record.Id && _native.HasWriteReceipt(id,before,after,restoring);

    private static bool HasStoppedCode10Instance()
    {
        using var root=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\ROOT\HIDCLASS");
        if(root==null)return false;
        var matches=new List<string>();
        foreach(string name in root.GetSubKeyNames())
        {
            using var instance=root.OpenSubKey(name);
            if(string.Equals(instance?.GetValue("Service") as string,"vjoy",StringComparison.OrdinalIgnoreCase))matches.Add(@"ROOT\HIDCLASS\"+name);
        }
        if(matches.Count!=1 || CM_Locate_DevNodeW(out uint node,matches[0],0)!=0)return false;
        return CM_Get_DevNode_Status(out uint flags,out uint problem,node,0)==0 && problem==10 && (flags&8)==0;
    }
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]private static extern uint CM_Locate_DevNodeW(out uint node,string id,uint flags);
    [DllImport("cfgmgr32.dll")]private static extern uint CM_Get_DevNode_Status(out uint flags,out uint problem,uint node,uint reserved);
}
