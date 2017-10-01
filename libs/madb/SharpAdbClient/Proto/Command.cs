using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpAdbClient.Proto
{
    public class Command
    {
        public static readonly uint SYNC = GetCommand("SYNC");
        public static readonly uint OPEN = GetCommand("OPEN");
        public static readonly uint CNXN = GetCommand("CNXN");
        public static readonly uint AUTH = GetCommand("AUTH");
        public static readonly uint OKAY = GetCommand("OKAY");
        public static readonly uint CLSE = GetCommand("CLSE");
        public static readonly uint WRTE = GetCommand("WRTE");

        public static uint GetCommand(string name)
        {
            var bytes = Encoding.ASCII.GetBytes(name);
            return Conversion.EndianBitConverter.Little.ToUInt32(bytes, 0);
        }

        public static byte[] CreateConnectCommand()
        {
            var hostData = "host::features=stat_v2,shell_v2,cmd";
            var packet = new AdbPacket(CNXN, 0x01000000, 4096, hostData);
            return packet.ToBuffer();
        }

        public static byte[] CreateOpenCommand(string path, uint id)
        {
            var packet = new AdbPacket(OPEN, id, 0, path);
            return packet.ToBuffer();
        }

        public static byte[] CreateWriteCommand(uint streamId, uint remoteStreamId, byte[] data)
        {
            var packet = new AdbPacket(WRTE, streamId, remoteStreamId, data);
            return packet.ToBuffer();
        }

        public static byte[] CreateOkCommand(uint localId, uint remoteId)
        {
            var packet = new AdbPacket(OKAY, localId, remoteId);
            return packet.ToBuffer();
        }
    }

    public class AdbPacket
    {
        public uint arg1 { get; private set; }
        public uint arg2 { get; private set; }
        public uint Command { get; private set; }
        public byte[] Data { get; set; }
        public uint magic { get; private set; }
        public string CommandName { get; private set; }
        public uint DataLength { get; private set; }
        public uint DataCrc { get; private set; }


        public AdbPacket(uint command, uint arg1, uint arg2)
        {
            CommandName = Encoding.UTF8.GetString(Conversion.EndianBitConverter.Little.GetBytes(command));
            this.Command = command;
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.magic = 0xFFFFFFFF - this.Command;
        }

        public AdbPacket(uint command, uint arg1, uint arg2, string data)
            : this(command, arg1, arg2)
        {
            var str = Encoding.ASCII.GetBytes(data);
            this.Data = new byte[data.Length+1];
            str.CopyTo(this.Data);
        }

        public AdbPacket(uint command, uint arg1, uint arg2, byte[] data)
            : this(command, arg1, arg2)
        { 
            this.Data = new byte[data.Length];
            data.CopyTo(this.Data);
        }

        public byte[] ToBuffer()
        {
            var dataLen = 0;
            if (this.Data != null) dataLen = this.Data.Length;
            var buff = new MemoryStream();
            var binWr = new BinaryWriter(buff);
            binWr.Write(this.Command);
            binWr.Write(this.arg1);
            binWr.Write(this.arg2);
            binWr.Write(dataLen);
            binWr.Write(Crc(this.Data));
            binWr.Write(this.magic);
            if (dataLen > 0)
            {
                binWr.Write(this.Data);
            }
            var output = buff.ToArray();
            buff.Close();
            return output;
        }

        public uint Crc(byte[] data)
        {
            if (data == null) return 0;
            uint res = 0;
            for (var i = 0; i < data.Length; i++)
            {
                res = (res + data[i]) & 0xFFFFFFFF;
            }
            return res;
        }

        public static AdbPacket FromBuffer(byte[] buff)
        { 
            uint command = Conversion.EndianBitConverter.Little.ToUInt32(buff, 0);
            uint arg1 = Conversion.EndianBitConverter.Little.ToUInt32(buff, 4);
            uint arg2 = Conversion.EndianBitConverter.Little.ToUInt32(buff, 8);
            uint dataLength = Conversion.EndianBitConverter.Little.ToUInt32(buff, 12);
            uint dataCRC = Conversion.EndianBitConverter.Little.ToUInt32(buff, 16);
            uint magic = Conversion.EndianBitConverter.Little.ToUInt32(buff, 20);
            var packet = new AdbPacket(command, arg1, arg2);
            packet.DataLength = dataLength;
            packet.DataCrc = dataCRC;
            var c = new byte[4] {buff[0], buff[1], buff[2], buff[3]};
            packet.CommandName = Encoding.ASCII.GetString(c);
            return packet;
        }

        public string DataString()
        {
            return Data==null ? null : Encoding.ASCII.GetString(Data);
        }

        public override string ToString()
        {
            var dataString = DataLength==0 ? "" : DataString();
            dataString = dataString.Trim().TrimEnd().Replace("\0", "");
            return $"{CommandName} {arg1} {arg2} {DataLength} {dataString}";
        }
    }
}
