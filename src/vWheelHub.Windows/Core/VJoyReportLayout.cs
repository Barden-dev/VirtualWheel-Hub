namespace SimRacingHub.Core;

// A vJoy driver combines the configured collections into one HID descriptor.
// Report IDs are global within each report type, not local to a device number.
internal static class VJoyReportLayout
{
    internal static HashSet<(int Type, int Id)> Reports(byte[] descriptor)
    {
        var reports=new HashSet<(int,int)>();
        var stack=new Stack<int>();
        int reportId=0;
        for(int offset=0;offset<descriptor.Length;)
        {
            byte prefix=descriptor[offset++];
            if(prefix==0xfe)throw new InvalidDataException("Unsupported long HID item; no vJoy configuration changed.");
            int size=(prefix&3)==3?4:prefix&3;
            if(offset+size>descriptor.Length)throw new InvalidDataException("Truncated HID descriptor; no vJoy configuration changed.");
            int tag=prefix&0xfc;
            if(tag==0x84)
            {
                if(size!=1 || descriptor[offset]==0)throw new InvalidDataException("Invalid HID report ID.");
                reportId=descriptor[offset];
            }
            else if(tag==0xa4)stack.Push(reportId);
            else if(tag==0xb4)reportId=stack.Count>0?stack.Pop():throw new InvalidDataException("Unbalanced HID globals.");
            else if(tag is 0x80 or 0x90 or 0xb0)reports.Add((tag,reportId));
            offset+=size;
        }
        if(stack.Count!=0)throw new InvalidDataException("Unbalanced HID globals.");
        return reports;
    }

    internal static void Validate(VJoyConfigurationSnapshot proposed,IEnumerable<VJoyConfigurationSnapshot> neighbours)
    {
        static byte[]? Descriptor(VJoyConfigurationSnapshot snapshot)=>snapshot.Device?.Values.SingleOrDefault(v=>v.Name.Equals("HidReportDesctiptor",StringComparison.OrdinalIgnoreCase))?.Data;
        var descriptor=Descriptor(proposed);
        if(descriptor==null)return;
        var wanted=Reports(descriptor);
        foreach(var neighbour in neighbours.Where(n=>n.DeviceId!=proposed.DeviceId))
        {
            var other=Descriptor(neighbour);
            if(other!=null && wanted.Overlaps(Reports(other)))
                throw new SimRacingHub.Application.Contracts.VJoyLayoutConflictException((int)proposed.DeviceId, (int)neighbour.DeviceId);
        }
    }
}
