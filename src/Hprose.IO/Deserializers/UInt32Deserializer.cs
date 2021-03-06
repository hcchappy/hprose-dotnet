﻿/*--------------------------------------------------------*\
|                                                          |
|                          hprose                          |
|                                                          |
| Official WebSite: https://hprose.com                     |
|                                                          |
|  UInt32Deserializer.cs                                   |
|                                                          |
|  UInt32Deserializer class for C#.                        |
|                                                          |
|  LastModified: Jan 11, 2019                              |
|  Author: Ma Bingyao <andot@hprose.com>                   |
|                                                          |
\*________________________________________________________*/

namespace Hprose.IO.Deserializers {
    using static Tags;

    internal class UInt32Deserializer : Deserializer<uint> {
        public override uint Read(Reader reader, int tag) {
            if (tag >= '0' && tag <= '9') {
                return (uint)(tag - '0');
            }
            var stream = reader.Stream;
            switch (tag) {
                case TagInteger:
                    return (uint)ValueReader.ReadInt(stream);
                case TagLong:
                    return (uint)ValueReader.ReadLong(stream);
                case TagDouble:
                    return (uint)ValueReader.ReadDouble(stream);
                case TagTrue:
                    return 1;
                case TagFalse:
                case TagEmpty:
                    return 0;
                case TagUTF8Char:
                    return Converter<uint>.Convert(ValueReader.ReadUTF8Char(stream));
                case TagString:
                    return Converter<uint>.Convert(ReferenceReader.ReadString(reader));
                default:
                    return base.Read(reader, tag);
            }
        }
    }
}
