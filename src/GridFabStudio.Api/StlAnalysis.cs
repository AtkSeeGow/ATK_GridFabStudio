using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;

internal record StlAnalysis(double X, double Y, double Z, int Triangles, double? VolumeCm3)
{
    public static StlAnalysis Read(byte[] bytes)
    {
        const int maxTriangles = 100_000;
        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        var edges = new Dictionary<(Vector3, Vector3), (int Count, int Balance)>();
        double volume = 0;
        var triangles = 0;
        var degenerate = false;
        Vector3 origin = default;

        void Add(Vector3 a, Vector3 b, Vector3 c)
        {
            foreach (var point in new[] { a, b, c })
            {
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z) ||
                    Vector3.Abs(point) != Vector3.Min(Vector3.Abs(point), new Vector3(1_000_000)))
                    throw new FormatException();
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }
            if (triangles == 0) origin = a;
            if (++triangles > maxTriangles)
                throw new FormatException("模型超過 100,000 個三角面，請簡化後再上傳。");

            degenerate |= Vector3.Cross(b - a, c - a).LengthSquared() == 0;
            volume += Vector3.Dot(a - origin, Vector3.Cross(b - origin, c - origin)) / 6d;
            AddEdge(a, b);
            AddEdge(b, c);
            AddEdge(c, a);
        }

        void AddEdge(Vector3 from, Vector3 to)
        {
            var direction = from.X.CompareTo(to.X);
            if (direction == 0) direction = from.Y.CompareTo(to.Y);
            if (direction == 0) direction = from.Z.CompareTo(to.Z);
            var key = direction <= 0 ? (from, to) : (to, from);
            var entry = edges.GetValueOrDefault(key);
            edges[key] = (entry.Count + 1, entry.Balance + Math.Sign(direction));
        }

        Vector3 BinaryVertex(int offset) => new(
            BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset)),
            BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset + 4)),
            BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset + 8)));

        var count = bytes.Length >= 84 ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(80)) : 0;
        if (bytes.Length >= 84 && bytes.Length == 84 + 50L * count)
        {
            for (var i = 0; i < count; i++)
            {
                var offset = 96 + 50 * i;
                Add(BinaryVertex(offset), BinaryVertex(offset + 12), BinaryVertex(offset + 24));
            }
        }
        else
        {
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
            string? Next()
            {
                string? line;
                do { line = reader.ReadLine()?.Trim(); } while (line == "");
                return line;
            }

            Vector3 Vertex()
            {
                var fields = Next()?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields is not { Length: 4 } || fields[0] != "vertex")
                    throw new FormatException();
                return new Vector3(float.Parse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture),
                    float.Parse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture),
                    float.Parse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture));
            }

            if (Next() is not string start || (start != "solid" && !start.StartsWith("solid ")))
                throw new FormatException();
            while (true)
            {
                var line = Next();
                if (line == "endsolid" || line?.StartsWith("endsolid ") == true) break;
                if (line?.StartsWith("facet normal ") != true || Next() != "outer loop")
                    throw new FormatException();
                Add(Vertex(), Vertex(), Vertex());
                if (Next() != "endloop" || Next() != "endfacet") throw new FormatException();
            }
            if (Next() is not null) throw new FormatException();
        }

        if (triangles == 0) throw new FormatException();
        var size = maximum - minimum;
        var closed = !degenerate && Math.Abs(volume) > 0.001 &&
            edges.Values.All(edge => edge is (2, 0));
        return new StlAnalysis(size.X, size.Y, size.Z, triangles, closed ? Math.Abs(volume) / 1000 : null);
    }
}
