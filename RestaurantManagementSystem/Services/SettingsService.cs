using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // One place to read the Admin > Settings values from any page or service.
    public class SettingsService
    {
        private readonly ApplicationDbContext _db;

        public SettingsService(ApplicationDbContext db)
        {
            _db = db;
        }

        // Returns the saved settings, or the default values if nothing is saved yet
        public async Task<SystemSetting> GetAsync()
        {
            return await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync()
                   ?? new SystemSetting();
        }
    }
}