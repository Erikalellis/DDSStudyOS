using DDSStudyOS.App.Models;
using System;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class DownloadAutoRegisterService
{
    private readonly DatabaseService _db;
    private readonly MaterialRepository _materials;

    public DownloadAutoRegisterService(DatabaseService db)
    {
        _db = db;
        _materials = new MaterialRepository(_db);
    }

    public async Task RegisterAsync(string destPath, string fileName, string category)
    {
        await _db.EnsureCreatedAsync();

        // No MVP, registra como material sem vínculo com curso.
        await _materials.CreateAsync(new MaterialItem
        {
            CourseId = null,
            FileName = fileName,
            FilePath = destPath,
            FileType = category
        });
    }
}
