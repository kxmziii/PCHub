using System.Text;

namespace PCHub.Helpers;

/// <summary>
/// ตัวอ่านข้อมูลแบบ protobuf อย่างง่าย (ใช้อ่านรายชื่อเซิร์ฟ FiveM)
/// protobuf เก็บข้อมูลเป็นคู่ "เลขช่อง + ค่า" ช่องที่ไม่ต้องใช้ก็ข้ามไป
/// </summary>
public ref struct ProtoReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;
    private int _position;

    public const int Varint = 0, Fixed64 = 1, LengthDelimited = 2, Fixed32 = 5;

    public bool IsEnd => _position >= _data.Length;

    /// <summary>อ่านหัวช่องถัดไป คืนค่า false เมื่อหมดข้อมูล</summary>
    public bool Next(out int field, out int wireType)
    {
        if (IsEnd)
        {
            field = wireType = 0;
            return false;
        }
        var tag = ReadVarint();
        field = (int)(tag >> 3);
        wireType = (int)(tag & 7);
        return true;
    }

    public ulong ReadVarint()
    {
        ulong value = 0;
        for (var shift = 0; shift < 64 && _position < _data.Length; shift += 7)
        {
            var b = _data[_position++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return value;
        }
        throw new FormatException("ข้อมูล protobuf เสีย");
    }

    public ReadOnlySpan<byte> ReadBytes()
    {
        var length = (int)ReadVarint();
        if (length < 0 || _position + length > _data.Length) throw new FormatException("ข้อมูล protobuf เสีย");
        var bytes = _data.Slice(_position, length);
        _position += length;
        return bytes;
    }

    public string ReadString() => Encoding.UTF8.GetString(ReadBytes());

    public void Skip(int wireType)
    {
        switch (wireType)
        {
            case Varint: ReadVarint(); break;
            case Fixed64: _position += 8; break;
            case LengthDelimited: ReadBytes(); break;
            case Fixed32: _position += 4; break;
            default: throw new FormatException($"ไม่รู้จักชนิดข้อมูล {wireType}");
        }
    }
}
