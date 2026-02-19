using System;

namespace DDSStudyOS.App.Models
{
    public sealed class Course
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Platform { get; set; }
        public string? Url { get; set; }
        public string? Username { get; set; }
        public byte[]? PasswordBlob { get; set; }
        public bool IsFavorite { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? DueDate { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; } // Smart Notes Content
        public DateTimeOffset? LastAccessed { get; set; } // Dashboard: Continue Studying
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

        public string FavoriteBadge => IsFavorite ? "★ Favorito" : string.Empty;
    }
}
