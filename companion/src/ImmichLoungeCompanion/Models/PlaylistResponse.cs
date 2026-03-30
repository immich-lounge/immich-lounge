using System;
using System.Collections.Generic;

namespace ImmichLoungeCompanion.Models;

public class PlaylistResponse
{
    public List<PlaylistEntry> Assets { get; set; } = [];
    public DateTimeOffset? GeneratedAt { get; set; }
    public bool Cached { get; set; }
    public bool Building { get; set; }
    public int Offset { get; set; }
    public int NextOffset { get; set; }
    public int TotalCount { get; set; }
}
