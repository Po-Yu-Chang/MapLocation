using System.ComponentModel.DataAnnotations;
using SQLite;

namespace MapLocationApp.Models;

[Table("Faces")]
public class FaceData
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [SQLite.MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public byte[] EncodedFace { get; set; } = Array.Empty<byte>();

    public string FeatureVectorJson { get; set; } = string.Empty;

    public int BoundingBoxX { get; set; }
    public int BoundingBoxY { get; set; }
    public int BoundingBoxWidth { get; set; }
    public int BoundingBoxHeight { get; set; }

    public float Quality { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public int Version { get; set; } = 1;
    public string ModelVersion { get; set; } = string.Empty;

    [Ignore]
    public float[]? FeatureVector { get; set; }

    [Ignore]
    public Rectangle BoundingBox
    {
        get => new Rectangle(BoundingBoxX, BoundingBoxY, BoundingBoxWidth, BoundingBoxHeight);
        set
        {
            BoundingBoxX = value.X;
            BoundingBoxY = value.Y;
            BoundingBoxWidth = value.Width;
            BoundingBoxHeight = value.Height;
        }
    }
}

public class DetectedFace
{
    public string Name { get; set; } = string.Empty;
    public Rectangle BoundingBox { get; set; }
    public float Confidence { get; set; }
    public float Quality { get; set; }

    public bool IsUnknown => string.IsNullOrEmpty(Name);
    public bool IsHighConfidence => Confidence >= 0.8f;
    public bool IsMediumConfidence => Confidence >= 0.6f && Confidence < 0.8f;
    public bool IsLowConfidence => Confidence < 0.6f;
}

public class RecognitionResult
{
    public List<DetectedFace> DetectedFaces { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public Dictionary<string, object> Metrics { get; set; } = new();

    public bool HasFaces => DetectedFaces.Count > 0;
    public int FaceCount => DetectedFaces.Count;
}

public struct Rectangle
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public Rectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool IsEmpty => X == 0 && Y == 0 && Width == 0 && Height == 0;
    public static Rectangle Empty => new(0, 0, 0, 0);
}