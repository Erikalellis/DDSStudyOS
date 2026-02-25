using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DDSStudyOS.App.Tests;

public sealed class CourseRepositoryFavoritesByProfileTests
{
    [Fact]
    public async Task Favorites_AreIsolatedByProfileKey()
    {
        var profilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DDSStudyOS",
            "config",
            "user-profile.json");

        var hadOriginalProfile = File.Exists(profilePath);
        var originalProfileJson = hadOriginalProfile ? await File.ReadAllTextAsync(profilePath) : null;
        var originalDirectory = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrWhiteSpace(originalDirectory))
        {
            Directory.CreateDirectory(originalDirectory);
        }

        var db = new DatabaseService();
        var repo = new CourseRepository(db);
        await db.EnsureCreatedAsync();

        var uniqueName = $"TEST-FAVORITE-PROFILE-{Guid.NewGuid():N}";
        long createdCourseId = 0;

        try
        {
            var profileA = new UserProfile
            {
                ProfileId = Guid.NewGuid().ToString("N"),
                Name = "Perfil A"
            };
            var profileB = new UserProfile
            {
                ProfileId = Guid.NewGuid().ToString("N"),
                Name = "Perfil B"
            };

            UserProfileService.Save(profileA);
            createdCourseId = await repo.CreateAsync(new Course
            {
                Name = uniqueName,
                Platform = "Teste",
                Url = "http://127.0.0.1/"
            });

            await repo.SetFavoriteAsync(createdCourseId, true);
            var favoritesProfileA = await repo.ListFavoritesAsync();
            Assert.Contains(favoritesProfileA, c => c.Id == createdCourseId);

            UserProfileService.Save(profileB);
            var favoritesProfileB = await repo.ListFavoritesAsync();
            Assert.DoesNotContain(favoritesProfileB, c => c.Id == createdCourseId);

            await repo.SetFavoriteAsync(createdCourseId, true);
            favoritesProfileB = await repo.ListFavoritesAsync();
            Assert.Contains(favoritesProfileB, c => c.Id == createdCourseId);

            UserProfileService.Save(profileA);
            favoritesProfileA = await repo.ListFavoritesAsync();
            Assert.Contains(favoritesProfileA, c => c.Id == createdCourseId);
        }
        finally
        {
            if (createdCourseId > 0)
            {
                try
                {
                    await repo.DeleteAsync(createdCourseId);
                }
                catch
                {
                    // Best effort cleanup for test artifacts.
                }
            }
            else
            {
                try
                {
                    var allCourses = await repo.ListAsync();
                    var leaked = allCourses.FirstOrDefault(c => string.Equals(c.Name, uniqueName, StringComparison.Ordinal));
                    if (leaked is not null)
                    {
                        await repo.DeleteAsync(leaked.Id);
                    }
                }
                catch
                {
                    // Best effort cleanup for test artifacts.
                }
            }

            if (hadOriginalProfile && originalProfileJson is not null)
            {
                await File.WriteAllTextAsync(profilePath, originalProfileJson);
            }
            else if (File.Exists(profilePath))
            {
                File.Delete(profilePath);
            }
        }
    }
}
